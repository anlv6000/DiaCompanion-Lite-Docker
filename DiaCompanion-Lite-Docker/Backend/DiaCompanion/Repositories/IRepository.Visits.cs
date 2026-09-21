using DiaCompanion.Api.Common;
using DiaCompanion.Api.Dtos;
using DiaCompanion.Api.Entities;

namespace DiaCompanion.Api.Repositories;

public sealed record VisitPage(IReadOnlyList<VisitDto> Items, int Total);
public sealed record VisitCloseData(int PendingImages, int ImagesWithoutAi, int TotalAi, int ReviewedAi, byte? WorstGrade);

public partial interface IRepository
{
    Task<VisitPage> GetVisitPageAsync(int? patientId, int? doctorId, byte? status,
        DateTime? fromUtc, DateTime? toExclusiveUtc, PageQuery page, CancellationToken ct = default);
    Task<VisitDto?> GetVisitDtoAsync(int id, CancellationToken ct = default);
    Task<bool> PatientExistsAsync(int patientId, CancellationToken ct = default);
    Task<bool> HasOpenVisitAsync(int patientId, CancellationToken ct = default);
    Task<bool> IsDoctorOnDutyAsync(int doctorId, byte dayOfWeek, CancellationToken ct = default);
    Task<string?> GetPatientNameAsync(int patientId, CancellationToken ct = default);
    Task<Visit?> GetVisitForUpdateAsync(int id, CancellationToken ct = default);
    Task<VisitCloseData> GetVisitCloseDataAsync(int visitId, CancellationToken ct = default);
    Task<bool> VisitHasClinicalDataAsync(int visitId, CancellationToken ct = default);
    Task<VisitPage> GetCompletedVisitsForPatientAsync(int patientId, PageQuery page, CancellationToken ct = default);
    Task<VisitDto?> GetCompletedVisitForPatientAsync(int patientId, int visitId, CancellationToken ct = default);
    Task<IReadOnlyList<HealthMetric>> GetVisitHealthMetricsAsync(int visitId, bool tracking = false, CancellationToken ct = default);
}
