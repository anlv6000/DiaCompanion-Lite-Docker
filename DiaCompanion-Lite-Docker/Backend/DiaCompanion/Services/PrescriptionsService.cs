using DiaCompanion.Api.Common;
using DiaCompanion.Api.Dtos;
using DiaCompanion.Api.Entities;
using DiaCompanion.Api.Repositories;
using DiaCompanion.Dtos;
using Microsoft.AspNetCore.Mvc;

namespace DiaCompanion.Api.Services;

/// <summary>UC-36..45 — nghiệp vụ đơn thuốc; toàn bộ LINQ/EF nằm trong Repository.</summary>
public class PrescriptionsService : BaseService, IPrescriptionsService
{
    private readonly IRepository _repository;
    private readonly ICurrentUser _me;
    private readonly IAuditService _audit;
    private readonly IVoidService _void;
    private readonly IAdherenceService _adherence;
    private readonly IClinicClock _clock;

    public PrescriptionsService(
        IRepository repository,
        ICurrentUser me,
        IAuditService audit,
        IVoidService voidSvc,
        IAdherenceService adherence,
        IClinicClock clock)
    {
        _repository = repository;
        _me = me;
        _audit = audit;
        _void = voidSvc;
        _adherence = adherence;
        _clock = clock;
    }

    public async Task<ActionResult<PagedResult<PrescriptionDto>>> List(
        int? patientId,
        string? q,
        DateOnly? from,
        DateOnly? to,
        bool? voided,
        PageQuery page)
    {
        var pid = ResolvePatientId(_me, patientId);
        DateTime? fromUtc = from is DateOnly fromDate
            ? _clock.ToUtc(fromDate.ToDateTime(TimeOnly.MinValue))
            : null;
        DateTime? toExclusiveUtc = to is DateOnly toDate
            ? _clock.ToUtc(toDate.AddDays(1).ToDateTime(TimeOnly.MinValue))
            : null;

        var result = await _repository.GetPrescriptionPageAsync(
            pid, q, fromUtc, toExclusiveUtc, voided, page);
        var stats = await _repository.GetPrescriptionMedicationStatsAsync(result.Items.Select(p => p.Id));
        var items = result.Items.Select(p => Map(p, stats.GetValueOrDefault(p.Id))).ToList();

        return Ok(new PagedResult<PrescriptionDto>
        {
            Items = items,
            Page = page.Page,
            PageSize = page.PageSize,
            TotalItems = result.Total
        });
    }

    public async Task<ActionResult<PrescriptionDto>> Get(int id)
    {
        var prescription = await _repository.GetPrescriptionAsync(id, tracking: false)
            ?? throw AppException.NotFound(Msg.LoadFailed, "Không tìm thấy đơn thuốc.");
        EnsureCanReadPatient(_me, prescription.PatientId);
        var stats = await _repository.GetPrescriptionMedicationStatsAsync(new[] { prescription.Id });
        return Ok(Map(prescription, stats.GetValueOrDefault(prescription.Id)));
    }

    public async Task<ActionResult<PrescriptionDto>> Create(CreatePrescriptionRequest req)
    {
        if (req.Items.Count == 0)
            throw AppException.BadRequest(Msg.EmptyPrescription, "Đơn thuốc phải có ít nhất một dòng thuốc.");
        if (req.VisitId is not int visitId)
            throw AppException.BadRequest(Msg.RequiredFields, "Đơn thuốc phải gắn với một lượt khám.");

        var doctorId = _me.RequireId();
        var visit = await _repository.GetVisitForUpdateAsync(visitId)
            ?? throw AppException.NotFound(Msg.LoadFailed, "Không tìm thấy lượt khám.");
        EnsureAssignedDoctor(visit, doctorId);
        if (visit.MedicalRecord.PatientId != req.PatientId)
            throw AppException.BadRequest(Msg.InvalidData, "Bệnh nhân không thuộc lượt khám đã chọn.");
        if (visit.Status != VisitStatus.InProgress)
            throw AppException.BadRequest(Msg.ApptImmutable, "Không thể tạo đơn mới sau khi lượt khám đã hoàn tất.");

        var prescriptionId = await _repository.ExecuteInTransactionAsync(async () =>
        {
            var prescription = new Prescription
            {
                PatientId = req.PatientId,
                VisitId = visit.Id,
                DoctorId = doctorId,
                IssuedAt = _clock.UtcNow,
                Note = req.Note?.Trim()
            };
            _repository.Add(prescription);
            await _repository.CommitAsync();

            var items = req.Items.Select(i => new PrescriptionItem
            {
                PrescriptionId = prescription.Id,
                DrugName = i.DrugName.Trim(),
                Dose = i.Dose.Trim(),
                TimesPerDay = i.TimesPerDay,
                DurationDays = checked((short)i.DurationDays),
                Instruction = i.Instruction?.Trim(),
                IsActive = true
            }).ToList();
            _repository.AddRange(items);
            await _repository.CommitAsync();

            _adherence.GenerateSchedule(prescription, items);
            await _audit.LogAsync(
                AuditAction.PrescriptionIssue,
                nameof(Prescription),
                prescription.Id,
                null,
                new
                {
                    prescription.PatientId,
                    prescription.VisitId,
                    itemCount = items.Count,
                    drugs = items.Select(i => i.DrugName)
                });
            await _repository.CommitAsync();
            return prescription.Id;
        });

        return Ok(await GetDtoAsync(prescriptionId));
    }

    public async Task<ActionResult<PrescriptionDto>> Update(int id, UpdatePrescriptionRequest req)
    {
        if (req.Items is null || req.Items.Count == 0)
            throw AppException.BadRequest(Msg.EmptyPrescription, "Đơn thuốc phải có ít nhất một dòng thuốc.");
        if (req.Items.Where(i => i.Id > 0).GroupBy(i => i.Id).Any(g => g.Count() > 1))
            throw AppException.BadRequest(Msg.InvalidData, "Danh sách cập nhật chứa ID dòng thuốc bị lặp.");

        var prescriptionId = await _repository.ExecuteInTransactionAsync(async () =>
        {
            var prescription = await _repository.GetPrescriptionAsync(id, tracking: true)
                ?? throw AppException.NotFound(Msg.LoadFailed, "Không tìm thấy đơn thuốc.");
            var doctorId = _me.RequireId();
            if (prescription.VisitId is not int visitId)
                throw AppException.Conflict(Msg.InvalidData, "Đơn thuốc chưa được liên kết với lượt khám.");

            var visit = await _repository.GetVisitForUpdateAsync(visitId)
                ?? throw AppException.NotFound(Msg.LoadFailed, "Không tìm thấy lượt khám liên quan.");
            EnsureAssignedDoctor(visit, doctorId);
            if (visit.Status != VisitStatus.InProgress)
                throw AppException.Conflict(
                    Msg.ApptImmutable,
                    "Lượt khám đã đóng. Không thể sửa đơn thuốc của lượt khám này.");
            if (visit.MedicalRecord.PatientId != prescription.PatientId)
                throw AppException.Conflict(Msg.InvalidData, "Đơn thuốc không khớp bệnh nhân của lượt khám.");

            _repository.ApplyOriginalRowVersion(prescription, req.RowVersion);
            var before = prescription.Items.Select(i => new
            {
                i.Id,
                i.DrugName,
                i.Dose,
                i.TimesPerDay,
                i.DurationDays,
                i.Instruction,
                i.IsActive
            }).ToList();

            var oldItemsById = prescription.Items.ToDictionary(i => i.Id);
            var requestedExistingIds = req.Items.Where(i => i.Id > 0).Select(i => i.Id).ToHashSet();
            var invalidIds = requestedExistingIds.Where(itemId => !oldItemsById.ContainsKey(itemId)).ToList();
            if (invalidIds.Count > 0)
                throw AppException.BadRequest(Msg.InvalidData,
                    $"Có dòng thuốc không thuộc đơn này: {string.Join(", ", invalidIds)}.");

            var pendingLogs = await _repository.GetPendingMedicationLogsForItemsAsync(oldItemsById.Keys);
            foreach (var log in pendingLogs) log.Status = MedicationStatus.Cancelled;
            foreach (var oldItem in prescription.Items)
                oldItem.IsActive = requestedExistingIds.Contains(oldItem.Id);

            var activeItems = new List<PrescriptionItem>();
            foreach (var requestItem in req.Items)
            {
                PrescriptionItem item;
                if (requestItem.Id > 0)
                {
                    item = oldItemsById[requestItem.Id];
                }
                else
                {
                    item = new PrescriptionItem { PrescriptionId = prescription.Id };
                    _repository.Add(item);
                }

                item.DrugName = requestItem.DrugName.Trim();
                item.Dose = requestItem.Dose.Trim();
                item.TimesPerDay = requestItem.TimesPerDay;
                item.DurationDays = checked((short)requestItem.DurationDays);
                item.Instruction = requestItem.Instruction?.Trim();
                item.IsActive = true;
                activeItems.Add(item);
            }

            prescription.Note = req.Note?.Trim();
            prescription.UpdatedAt = _clock.UtcNow;

            // Commit này vừa kiểm tra RowVersion, vừa cấp Id cho item mới.
            if (!await _repository.TryCommitAsync())
                throw AppException.Conflict(
                    Msg.StaleVersion,
                    "Đơn thuốc đã được người khác cập nhật. Vui lòng tải lại dữ liệu trước khi thử lại.");

            _adherence.GenerateSchedule(prescription, activeItems);
            await _audit.LogAsync(
                AuditAction.PrescriptionUpdate,
                nameof(Prescription),
                prescription.Id,
                new { items = before },
                new
                {
                    items = activeItems.Select(i => new
                    {
                        i.Id,
                        i.DrugName,
                        i.Dose,
                        i.TimesPerDay,
                        i.DurationDays,
                        i.Instruction,
                        i.IsActive
                    }),
                    cancelledPendingLogs = pendingLogs.Count
                });
            await _repository.CommitAsync();
            return prescription.Id;
        });

        return Ok(await GetDtoAsync(prescriptionId));
    }

    public async Task<IActionResult> Void(int id, VoidRequest req)
    {
        var rowVersion = await _void.VoidPrescriptionAsync(id, req.Reason, req.RowVersion);
        return Ok(new
        {
            message = "Đã thu hồi đơn thuốc. Lịch nhắc chưa tới hạn đã hủy; các liều đã xử lý được giữ lại.",
            rowVersion
        });
    }

    public async Task<IActionResult> Adherence(
        int patientId,
        int days = 30,
        int? prescriptionId = null,
        DateOnly? from = null,
        DateOnly? to = null)
    {
        EnsureCanReadPatient(_me, patientId);
        var summary = await _adherence.GetAsync(patientId, days, prescriptionId, from, to);
        return Ok(new
        {
            summary.Total,
            summary.Taken,
            summary.Missed,
            summary.Skipped,
            summary.Pending,
            summary.Rate,
            days,
            prescriptionId,
            from,
            to,
            note = "Tỉ lệ tính trên các liều đã tới hạn; Skipped và Missed đều không được tính là tuân thủ."
        });
    }

    private async Task<PrescriptionDto> GetDtoAsync(int id)
    {
        var prescription = await _repository.GetPrescriptionAsync(id, tracking: false)
            ?? throw AppException.NotFound(Msg.LoadFailed, "Không tìm thấy đơn thuốc.");
        var stats = await _repository.GetPrescriptionMedicationStatsAsync(new[] { id });
        return Map(prescription, stats.GetValueOrDefault(id));
    }

    private PrescriptionDto Map(Prescription prescription, PrescriptionMedicationStats? stats)
    {
        stats ??= new PrescriptionMedicationStats(0, 0, 0, 0);
        var due = stats.Taken + stats.Missed + stats.Skipped;
        var rate = due == 0 ? 0m : Math.Round(stats.Taken * 100m / due, 1);
        return new PrescriptionDto
        {
            Id = prescription.Id,
            PatientId = prescription.PatientId,
            VisitId = prescription.VisitId,
            DoctorId = prescription.DoctorId,
            DoctorName = prescription.Doctor?.FullName ?? "",
            IssuedAt = _clock.ToLocal(prescription.IssuedAt)!.Value,
            UpdatedAt = _clock.ToLocal(prescription.UpdatedAt),
            Note = prescription.Note,
            IsVoided = prescription.IsVoided,
            VoidReason = prescription.VoidReason,
            VoidedAt = _clock.ToLocal(prescription.VoidedAt),
            ScheduledDoses = stats.Total,
            TakenDoses = stats.Taken,
            MissedDoses = stats.Missed,
            SkippedDoses = stats.Skipped,
            AdherenceRate = rate,
            RowVersion = prescription.ToRowVersion(),
            Items = prescription.Items
                .OrderByDescending(i => i.IsActive)
                .ThenBy(i => i.Id)
                .Select(i => new PrescriptionItemDto
                {
                    Id = i.Id,
                    DrugName = i.DrugName,
                    Dose = i.Dose,
                    TimesPerDay = i.TimesPerDay,
                    DurationDays = i.DurationDays,
                    Instruction = i.Instruction,
                    IsActive = i.IsActive
                }).ToList()
        };
    }

    private static void EnsureAssignedDoctor(Visit visit, int doctorId)
    {
        if (visit.DoctorId != doctorId)
            throw AppException.Forbidden(Msg.Forbidden,
                "Bác sĩ chỉ được thao tác đơn thuốc của lượt khám do mình phụ trách.");
    }
}
