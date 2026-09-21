using DiaCompanion.Api.Common;
using DiaCompanion.Api.Repositories;
using DiaCompanion.Api.Entities;

namespace DiaCompanion.Api.Services;

/// <summary>
/// QT-6: void dây chuyền xử lý ở TẦNG ỨNG DỤNG, không dùng ON DELETE CASCADE.
/// Cascade ở tầng CSDL là code chết khi không bao giờ xoá cứng, và nguy hiểm
/// nếu ai đó chạy DELETE thủ công — sẽ âm thầm xoá MedicationLogs, tức mất
/// sạch lịch sử tuân thủ thuốc.
/// </summary>
public interface IVoidService
{
    Task<string> VoidPatientAsync(int id, string reason, string rowVersion);
    Task<string> VoidVisitAsync(int id, string reason, string rowVersion);
    Task<string> VoidImageAsync(int id, string reason, string rowVersion);
    Task<string> VoidDiagnosisAsync(int id, string reason, string rowVersion);
    Task<string> VoidReviewAsync(int id, string reason, string rowVersion);
    Task<string> VoidPrescriptionAsync(int id, string reason, string rowVersion);
}

public class VoidService : IVoidService
{
    private readonly IRepository _repository;
    private readonly ICurrentUser _me;
    private readonly IAuditService _audit;

    public VoidService(IRepository repository, ICurrentUser me, IAuditService audit)
    { _repository = repository; _me = me; _audit = audit; }

    private void Mark(IVoidable e, string reason)
    {
        if (string.IsNullOrWhiteSpace(reason))
            throw AppException.BadRequest(Msg.VoidReason, "Vui lòng nhập lý do trước khi thu hồi bản ghi.");
        e.IsVoided = true;
        e.VoidReason = reason.Trim();
        e.VoidedBy = _me.RequireId();
        e.VoidedAt = DateTime.UtcNow;
    }

    /* ---------------------------------------------------------------------
       Bảng lan void (DATABASE.md QT-6):

       FundusImage   -> AiDiagnoses của ảnh -> DiagnosisReviews của các kết quả
       AiDiagnosis   -> DiagnosisReviews của kết quả đó
       Visit         -> FundusImages (lan tiếp) + Prescriptions của lượt khám
       Prescription  -> huỷ lịch nhắc CHƯA tới hạn, GIỮ MedicationLogs đã ghi
       Patient       -> toàn bộ chuỗi trên
       --------------------------------------------------------------------- */

    public async Task<string> VoidPatientAsync(
        int id,
        string reason,
        string rowVersion)
    {
        // E1 - Bắt buộc có lý do
        if (string.IsNullOrWhiteSpace(reason))
        {
            throw AppException.BadRequest(
                Msg.VoidReason,
                "Vui lòng nhập lý do thu hồi hồ sơ bệnh nhân.");
        }

        var currentUserId = _me.RequireId();
        var currentRoles = string.Join(",", _me.Roles);

        // Chỉ Admin được thu hồi toàn bộ hồ sơ bệnh nhân
        if (!_me.IsInRole(Roles.Receptionist))
        {
            throw AppException.Forbidden(
                Msg.Forbidden,
                "Chỉ lễ tân được thu hồi hồ sơ bệnh nhân.");
        }



        var existPatientVisit = await _repository.ExistVisitPatientIds(id);

        var phoneDoctorsift = await _repository.shiftDoctorInformation(id);
        if (existPatientVisit) throw AppException.Forbidden(
                Msg.VoidReason,
                "không thu hồi được do đã tồn tại ca khám này, liên hệ bác sĩ để thu hồi ca khám "+phoneDoctorsift.phone +" | "+phoneDoctorsift.name);



        var patient = await _repository.GetPatientForVoidAsync(id)
            ?? throw AppException.NotFound(
                Msg.PatientNotFound,
                "Không tìm thấy hồ sơ bệnh nhân.");

        // Không cho void lại
        if (patient.IsVoided)
        {
            throw AppException.Conflict(
                "Hồ sơ đã được thu hồi",
                "Hồ sơ bệnh nhân này đã được thu hồi trước đó.");
        }

        var normalizedReason = reason.Trim();

        // RowVersion client nhận khi đọc Patient
        _repository.ApplyOriginalRowVersion(
            patient,
            rowVersion);

        var oldValue = new
        {
            patient.Code,
            patient.FullName,
            patient.UserId,
            isVoided = patient.IsVoided
        };

        Mark(patient, normalizedReason);

        /*
         * Void các lượt khám.
         * VoidVisitInternalAsync tiếp tục lan xuống:
         * FundusImage -> AiDiagnosis -> DiagnosisReview
         * Prescription -> MedicationLogs Pending
         */
        var visits = await _repository.GetActiveVisitsForPatientAsync(patient.Id);

        foreach (var visit in visits)
        {
            await VoidVisitInternalAsync(
                visit,
                $"Thu hồi theo hồ sơ bệnh nhân: {normalizedReason}");
        }

        /*
         * Ảnh không gắn với Visit sẽ không được xử lý qua danh sách visits,
         * nên phải void riêng.
         */
        var orphanImages = await _repository.GetActiveOrphanImagesForPatientAsync(patient.Id);

        foreach (var image in orphanImages)
        {
            await VoidImageInternalAsync(
                image,
                $"Thu hồi theo hồ sơ bệnh nhân: {normalizedReason}");
        }

        // Thu hồi riêng quyền Patient; các role khác của cùng User vẫn giữ nguyên.
        if (patient.UserId is int userId)
        {
            // User active đang được Patient này sử dụng.
            var user = await _repository.GetUserForUpdateAsync(userId)
                ?? throw AppException.Conflict(
                    Msg.InvalidData,
                    "Không tìm thấy tài khoản đang liên kết với hồ sơ bệnh nhân.");

            // Kiểm tra TRƯỚC xem User có từng mang role nào ngoài Patient không.
            //
            // Nếu có Doctor/Receptionist/Admin/... thì đây là tài khoản dùng chung,
            // không được void toàn bộ User.
            var hasNonPatientRole =
                await _repository.UserHasNonPatientRoleAssignmentAsync(userId);

            // Luôn tắt riêng Patient role khi hồ sơ Patient bị void.
            var patientRoleDisabled =
                await _repository.SetUserRoleActiveAsync(
                    userId,
                    Roles.Patient,
                    isActive: false,
                    changedBy: currentUserId);

            if (!patientRoleDisabled)
            {
                throw AppException.Conflict(
                    Msg.InvalidData,
                    "Tài khoản liên kết không có vai trò Patient hợp lệ.");
            }

            // Nếu User CHỈ là Patient:
            // void luôn User.
            if (!hasNonPatientRole)
            {
                user.IsVoided = true;
                user.UpdatedAt = DateTime.UtcNow;

                if (!string.IsNullOrWhiteSpace(user.Phone))
                {
                    await _repository
                        .InvalidateUnconsumedOtpCodesForPhoneAsync(user.Phone);
                }
                await _audit.LogAsync(
                    AuditAction.Void,
                    nameof(User),
                    user.Id,
                    new
                    {
                        user.FullName,
                        user.Phone,
                        isVoided = false
                    },
                    new
                    {
                        user.FullName,
                        user.Phone,
                        isVoided = true
                    },
                    $"Thu hồi tài khoản theo hồ sơ bệnh nhân {patient.Code}");
            }
        }

        await _audit.LogAsync(
            AuditAction.Void,
            nameof(Patient),
            patient.Id,
            oldValue,
            new
            {
                patient.Code,
                patient.FullName,
                patient.UserId,
                isVoided = patient.IsVoided,
                voidedBy = currentUserId,
                role = currentRoles
            },
            normalizedReason);

        if (!await _repository.TryCommitAsync())
            throw AppException.Conflict(
                Msg.StaleVersion,
                "Hồ sơ bệnh nhân hoặc dữ liệu liên quan đã được người khác cập nhật. Vui lòng tải lại trước khi thử lại.");

        return Convert.ToBase64String(patient.RowVer);
    }

    public async Task<string> VoidVisitAsync(
      int id,
      string reason,
      string rowVersion)
    {
        // E1 - Missing reason
        if (string.IsNullOrWhiteSpace(reason))
        {
            throw AppException.BadRequest(
                Msg.VoidReason,
                "Vui lòng nhập lý do thu hồi lượt khám.");
        }

        var visit = await _repository.GetVisitForVoidAsync(id)
            ?? throw AppException.NotFound(
                Msg.LoadFailed,
                "Không tìm thấy lượt khám.");

        // Preconditions: bản ghi chưa bị void
        if (visit.IsVoided)
        {
            throw AppException.Conflict(
                "Lượt khám đã được thu hồi",
                "Lượt khám này đã được thu hồi trước đó.");
        }

        if (visit.Status == VisitStatus.Completed)
        {
            throw AppException.Conflict(
                Msg.ApptImmutable,
                "Lượt khám đã được đóng. Không thể thu hồi hoặc thay đổi dữ liệu của lượt khám này.");
        }

        var currentUserId = _me.RequireId();
        var currentRoles = string.Join(",", _me.Roles);

        if (_me.IsInRole(Roles.Doctor))
        {
                    // E4 - Bác sĩ phải là bác sĩ được phân công
                    if (visit.DoctorId != currentUserId)
                    {
                        throw AppException.Forbidden(
                            Msg.Forbidden,
                            "Bác sĩ chỉ được thu hồi lượt khám do mình phụ trách.");
                    }

        }
        else if (_me.IsInRole(Roles.Receptionist))
        {
                    // E4 - Lễ tân chỉ được void lượt khám đang mở
                    if (visit.Status != VisitStatus.InProgress)
                    {
                        throw AppException.Forbidden(
                            Msg.Forbidden,
                            "Lễ tân chỉ được thu hồi lượt khám đang mở.");
                    }

                    // Không được tồn tại bất kỳ ảnh nào của lượt khám
                    var hasFundusImage = await _repository.VisitHasFundusImageAsync(visit.Id);

                    // Không được tồn tại bất kỳ đơn thuốc nào của lượt khám
                    var hasPrescription = await _repository.VisitHasPrescriptionAsync(visit.Id);

                    if (hasFundusImage || hasPrescription)
                    {
                        throw AppException.Forbidden(
                            Msg.Forbidden,
                            "Lễ tân không được thu hồi lượt khám đã có ảnh đáy mắt hoặc dữ liệu đơn thuốc.");
                    }

        }
        else
        {
            throw AppException.Forbidden(Msg.Forbidden, "Bạn không có quyền thu hồi lượt khám.");
        }

        /*
         * Client gửi RowVersion của lúc nó tải lượt khám.
         * EF dùng giá trị này trong điều kiện UPDATE.
         */
        _repository.ApplyOriginalRowVersion(
            visit,
            rowVersion);

        var normalizedReason = reason.Trim();

        await VoidVisitInternalAsync(
            visit,
            normalizedReason);

        await _audit.LogAsync(
            AuditAction.Void,
            nameof(Visit),
            visit.Id,
            new
            {
                visit.Status,
                visit.DoctorId,
                isVoided = false
            },
            new
            {
                isVoided = true,
                voidedBy = currentUserId,
                roles = currentRoles
            },
            normalizedReason);

        if (!await _repository.TryCommitAsync())
            throw AppException.Conflict(
                Msg.StaleVersion,
                "Lượt khám đã được người khác cập nhật. Vui lòng tải lại dữ liệu trước khi thử lại.");

        // Sau SaveChanges, SQL Server đã tạo RowVer mới
        return Convert.ToBase64String(visit.RowVer);
    }

    private async Task VoidVisitInternalAsync(Visit v, string reason)
    {
        if (v.IsVoided)
            return;

        Mark(v, reason);

        var images = await _repository.GetActiveImagesForVisitAsync(v.Id);

        foreach (var f in images)
        {
            await VoidImageInternalAsync(
                f,
                $"Thu hồi theo lượt khám: {reason}");
        }

        var prescriptions = await _repository.GetActivePrescriptionsForVisitAsync(v.Id);

        foreach (var p in prescriptions)
        {
            await VoidPrescriptionInternalAsync(
                p,
                $"Thu hồi theo lượt khám: {reason}");
        }

        // Chỉ số được bác sĩ ghi trong visit là dữ liệu lâm sàng của visit.
        // Khi visit bị thu hồi, soft-delete chúng để không còn xuất hiện trong monitoring/report.
        var metrics = await _repository.GetVisitHealthMetricsAsync(v.Id, tracking: true);
        foreach (var metric in metrics)
        {
            metric.IsDeleted = true;
            metric.DeletedAt = DateTime.UtcNow;
        }
    }
    public async Task<string> VoidImageAsync(
        int id,
        string reason,
        string rowVersion)
    {
        if (string.IsNullOrWhiteSpace(reason))
        {
            throw AppException.BadRequest(
                Msg.VoidReason,
                "Vui lòng nhập lý do thu hồi ảnh đáy mắt.");
        }

        var currentUserId = _me.RequireId();
        var currentRoles = string.Join(",", _me.Roles);

        // Chỉ Admin và Doctor được void ảnh
        if (!_me.IsInRole(Roles.Admin, Roles.Doctor))
        {
            throw AppException.Forbidden(
                Msg.Forbidden,
                "Chỉ quản trị viên hoặc bác sĩ được thu hồi ảnh đáy mắt.");
        }

        var image = await _repository.GetImageForVoidAsync(id)
            ?? throw AppException.NotFound(
                Msg.LoadFailed,
                "Không tìm thấy ảnh đáy mắt.");

        if (image.IsVoided)
        {
            throw AppException.Conflict(
                "Ảnh đã được thu hồi",
                "Ảnh đáy mắt này đã được thu hồi trước đó.");
        }

        if (image.VisitId is int imageVisitId)
        {
            var imageVisit = await _repository.GetVisitForVoidAsync(imageVisitId)
                ?? throw AppException.Conflict(
                    Msg.LoadFailed,
                    "Không tìm thấy lượt khám liên quan đến ảnh.");

            if (imageVisit.Status == VisitStatus.Completed)
                throw AppException.Conflict(
                    Msg.ApptImmutable,
                    "Lượt khám đã đóng. Ảnh của lượt khám chỉ được xem và không thể thu hồi.");
        }

        /*
         * Doctor chỉ được void ảnh thuộc lượt khám
         * mà chính Doctor đó được phân công.
         *
         * Ảnh chưa gắn Visit không xác định được bác sĩ phụ trách,
         * nên chỉ Admin được void.
         */
        if (_me.IsInRole(Roles.Doctor) && !_me.IsInRole(Roles.Admin))
        {
            if (image.VisitId is not int visitId)
            {
                throw AppException.Forbidden(
                    Msg.Forbidden,
                    "Bác sĩ không được thu hồi ảnh chưa gắn với lượt khám.");
            }

            var isAssignedDoctor = await _repository.IsVisitAssignedToDoctorAsync(visitId, currentUserId);

            if (!isAssignedDoctor)
            {
                throw AppException.Forbidden(
                    Msg.Forbidden,
                    "Bác sĩ chỉ được thu hồi ảnh thuộc lượt khám do mình phụ trách.");
            }
        }

        var normalizedReason = reason.Trim();

        _repository.ApplyOriginalRowVersion(
            image,
            rowVersion);

        var oldValue = new
        {
            image.PatientId,
            image.VisitId,
            isVoided = image.IsVoided
        };

        await VoidImageInternalAsync(
            image,
            normalizedReason);

        await _audit.LogAsync(
            AuditAction.Void,
            nameof(FundusImage),
            image.Id,
            oldValue,
            new
            {
                image.PatientId,
                image.VisitId,
                isVoided = image.IsVoided,
                voidedBy = currentUserId,
                role = currentRoles
            },
            normalizedReason);

        if (!await _repository.TryCommitAsync())
            throw AppException.Conflict(
                Msg.StaleVersion,
                "Ảnh đáy mắt đã được người khác cập nhật. Vui lòng tải lại dữ liệu trước khi thử lại.");

        return Convert.ToBase64String(image.RowVer);
    }

    private async Task VoidImageInternalAsync(
        FundusImage image,
        string reason)
    {
        if (image.IsVoided)
            return;

        Mark(image, reason);

        var diagnoses = await _repository.GetActiveDiagnosesForImageAsync(image.Id);

        foreach (var diagnosis in diagnoses)
        {
            await VoidDiagnosisInternalAsync(
                diagnosis,
                $"Thu hồi theo ảnh: {reason}");
        }
    }

    public async Task<string> VoidDiagnosisAsync(
        int id,
        string reason,
        string rowVersion)
    {
        // E1 - Missing reason
        if (string.IsNullOrWhiteSpace(reason))
        {
            throw AppException.BadRequest(
                Msg.VoidReason,
                "Vui lòng nhập lý do thu hồi kết quả AI.");
        }

        var currentUserId = _me.RequireId();
        var currentRoles = string.Join(",", _me.Roles);

        // Chỉ Admin và Doctor được thực hiện
        if (!_me.IsInRole(Roles.Admin, Roles.Doctor))
        {
            throw AppException.Forbidden(
                Msg.Forbidden,
                "Chỉ quản trị viên hoặc bác sĩ được thu hồi kết quả AI.");
        }

        var diagnosis = await _repository.GetDiagnosisForVoidAsync(id)
            ?? throw AppException.NotFound(
                Msg.LoadFailed,
                "Không tìm thấy kết quả AI.");

        // Bản ghi đã bị void trước đó
        if (diagnosis.IsVoided)
        {
            throw AppException.Conflict(
                "Kết quả AI đã được thu hồi",
                "Kết quả AI này đã được thu hồi trước đó.");
        }

        var image = await _repository.GetImageForVoidAsync(diagnosis.FundusImageId)
            ?? throw AppException.Conflict(
                Msg.LoadFailed,
                "Không tìm thấy ảnh đáy mắt liên quan.");

        /*
         * Nếu ảnh đã bị void thì thông thường kết quả AI cũng phải được
         * void theo. Trường hợp kết quả vẫn hoạt động là dữ liệu không nhất quán.
         */
        if (image.IsVoided)
        {
            throw AppException.Conflict(
                "Ảnh đã được thu hồi",
                "Không thể thao tác trên kết quả AI của ảnh đã bị thu hồi.");
        }

        if (image.VisitId is int diagnosisVisitId)
        {
            var diagnosisVisit = await _repository.GetVisitForVoidAsync(diagnosisVisitId)
                ?? throw AppException.Conflict(
                    Msg.LoadFailed,
                    "Không tìm thấy lượt khám liên quan đến kết quả AI.");

            if (diagnosisVisit.Status == VisitStatus.Completed)
                throw AppException.Conflict(
                    Msg.ApptImmutable,
                    "Lượt khám đã đóng. Kết quả AI chỉ được xem và không thể thu hồi.");
        }

        /*
         * Doctor chỉ được void kết quả AI thuộc lượt khám
         * mà chính Doctor đó được phân công.
         *
         * Admin không bị giới hạn bởi DoctorId.
         */
        if (_me.IsInRole(Roles.Doctor) && !_me.IsInRole(Roles.Admin))
        {
            if (image.VisitId is not int visitId)
            {
                throw AppException.Forbidden(
                    Msg.Forbidden,
                    "Bác sĩ không được thu hồi kết quả AI chưa gắn với lượt khám.");
            }

            var isAssignedDoctor = await _repository.IsVisitAssignedToDoctorAsync(visitId, currentUserId);

            if (!isAssignedDoctor)
            {
                throw AppException.Forbidden(
                    Msg.Forbidden,
                    "Bác sĩ chỉ được thu hồi kết quả AI thuộc lượt khám do mình phụ trách.");
            }
        }

        var normalizedReason = reason.Trim();

        /*
         * Client gửi RowVersion của lúc đọc kết quả AI.
         * EF sử dụng giá trị này trong điều kiện UPDATE.
         */
        _repository.ApplyOriginalRowVersion(
            diagnosis,
            rowVersion);

        var oldValue = new
        {
            diagnosis.FundusImageId,
            isVoided = diagnosis.IsVoided
            // Có thể thêm các trường kết quả AI:
            // diagnosis.PredictedGrade,
            // diagnosis.Confidence
        };

        /*
         * Void AiDiagnosis và các DiagnosisReview phụ thuộc.
         * Không void FundusImage.
         */
        await VoidDiagnosisInternalAsync(
            diagnosis,
            normalizedReason);

        await _audit.LogAsync(
            AuditAction.Void,
            nameof(AiDiagnosis),
            diagnosis.Id,
            oldValue,
            new
            {
                diagnosis.FundusImageId,
                isVoided = diagnosis.IsVoided,
                voidedBy = currentUserId,
                role = currentRoles
            },
            normalizedReason);

        if (!await _repository.TryCommitAsync())
            throw AppException.Conflict(
                Msg.StaleVersion,
                "Kết quả AI đã được người khác cập nhật. Vui lòng tải lại dữ liệu trước khi thử lại.");

        return Convert.ToBase64String(diagnosis.RowVer);
    }

    private async Task VoidDiagnosisInternalAsync(
        AiDiagnosis diagnosis,
        string reason)
    {
        if (diagnosis.IsVoided)
            return;

        Mark(diagnosis, reason);

        var reviews = await _repository.GetActiveReviewsForDiagnosisAsync(diagnosis.Id);

        foreach (var review in reviews)
        {
            Mark(
                review,
                $"Thu hồi theo kết quả AI {diagnosis.Id}: {reason}");
        }
    }

    public async Task<string> VoidReviewAsync(
        int id,
        string reason,
        string rowVersion)
    {
        // E1 - MSG-17: bắt buộc nhập lý do
        if (string.IsNullOrWhiteSpace(reason))
        {
            throw AppException.BadRequest(
                Msg.VoidReason,
                "Vui lòng nhập lý do thu hồi bản duyệt chẩn đoán.");
        }

        var currentUserId = _me.RequireId();
        var currentRoles = string.Join(",", _me.Roles);

        // Primary Actor chỉ có Doctor
        if (!_me.IsInRole(Roles.Doctor))
        {
            throw AppException.Forbidden(
                Msg.Forbidden,
                "Chỉ bác sĩ phụ trách mới được thu hồi bản duyệt chẩn đoán.");
        }

        var review = await _repository.GetReviewForVoidAsync(id)
            ?? throw AppException.NotFound(
                Msg.LoadFailed,
                "Không tìm thấy bản duyệt chẩn đoán.");

        // Preconditions: review chưa bị void
        if (review.IsVoided)
        {
            throw AppException.Conflict(
                Msg.InvalidData,
                "Bản duyệt chẩn đoán này đã được thu hồi trước đó.");
        }

        /*
         * Kiểm tra chuỗi liên kết:
         *
         * DiagnosisReview
         *      -> AiDiagnosis
         *      -> FundusImage
         *      -> Visit
         *      -> DoctorId
         */

        var diagnosis = await _repository.GetDiagnosisForVoidAsync(review.AiDiagnosisId)
            ?? throw AppException.Conflict(
                Msg.LoadFailed,
                "Không tìm thấy kết quả AI liên quan đến bản duyệt.");

        if (diagnosis.IsVoided)
        {
            throw AppException.Conflict(
                Msg.InvalidData,
                "Không thể thay đổi bản duyệt của một kết quả AI đã bị thu hồi.");
        }

        var image = await _repository.GetImageForVoidAsync(diagnosis.FundusImageId)
            ?? throw AppException.Conflict(
                Msg.LoadFailed,
                "Không tìm thấy ảnh đáy mắt liên quan.");

        if (image.IsVoided)
        {
            throw AppException.Conflict(
                Msg.InvalidData,
                "Không thể thay đổi bản duyệt của một ảnh đã bị thu hồi.");
        }

        if (image.VisitId is not int visitId)
        {
            throw AppException.Conflict(
                Msg.LoadFailed,
                "Ảnh đáy mắt chưa được liên kết với lượt khám.");
        }

        var visit = await _repository.GetVisitForVoidAsync(visitId)
            ?? throw AppException.Conflict(
                Msg.LoadFailed,
                "Không tìm thấy lượt khám liên quan.");

        if (visit.IsVoided)
        {
            throw AppException.Conflict(
                Msg.InvalidData,
                "Không thể thay đổi bản duyệt của lượt khám đã bị thu hồi.");
        }

        if (visit.Status == VisitStatus.Completed)
        {
            throw AppException.Conflict(
                Msg.ApptImmutable,
                "Lượt khám đã đóng. Bản duyệt chẩn đoán chỉ được xem và không thể thu hồi.");
        }

        // E4 - Doctor phải là bác sĩ được phân công cho Visit
        if (visit.DoctorId != currentUserId)
        {
            throw AppException.Forbidden(
                Msg.Forbidden,
                "Bác sĩ chỉ được thu hồi bản duyệt thuộc lượt khám do mình phụ trách.");
        }

        var normalizedReason = reason.Trim();

        /*
         * Dùng rowVersion mà client nhận khi đọc review.
         * EF sẽ đưa giá trị này vào điều kiện UPDATE.
         */
        _repository.ApplyOriginalRowVersion(
            review,
            rowVersion);

        // Chụp dữ liệu trước khi thay đổi để ghi audit
        var oldValue = new
        {
            review.Action,
            review.FinalGrade,
            review.AiDiagnosisId,
            isVoided = review.IsVoided
        };

        /*
         * Chỉ void DiagnosisReview.
         * Không void AiDiagnosis vì kết quả AI vẫn còn hợp lệ
         * và cần quay lại hàng đợi triage.
         */
        Mark(review, normalizedReason);

        await _audit.LogAsync(
            AuditAction.Void,
            nameof(DiagnosisReview),
            review.Id,
            oldValue,
            new
            {
                review.Action,
                review.FinalGrade,
                review.AiDiagnosisId,
                isVoided = review.IsVoided,
                voidedBy = currentUserId,
                role = currentRoles
            },
            normalizedReason);

        if (!await _repository.TryCommitAsync())
            throw AppException.Conflict(
                Msg.StaleVersion,
                "Bản duyệt đã được người khác cập nhật. Vui lòng tải lại dữ liệu trước khi thử lại.");

        // SQL Server đã sinh RowVer mới sau UPDATE
        return Convert.ToBase64String(review.RowVer);
    }

    public async Task<string> VoidPrescriptionAsync(
        int id,
        string reason,
        string rowVersion)
    {
        // E1 - MSG-17: bắt buộc nhập lý do
        if (string.IsNullOrWhiteSpace(reason))
        {
            throw AppException.BadRequest(
                Msg.VoidReason,
                "Vui lòng nhập lý do thu hồi đơn thuốc.");
        }

        var currentUserId = _me.RequireId();
        var currentRoles = string.Join(",", _me.Roles);

        // E2 - Chỉ Doctor được thực hiện
        if (!_me.IsInRole(Roles.Doctor))
        {
            throw AppException.Forbidden(
                Msg.Forbidden,
                "Chỉ bác sĩ phụ trách mới được thu hồi đơn thuốc.");
        }

        var prescription = await _repository.GetPrescriptionForVoidAsync(id)
            ?? throw AppException.NotFound(
                Msg.LoadFailed,
                "Không tìm thấy đơn thuốc.");

        // Preconditions: đơn thuốc chưa bị void
        if (prescription.IsVoided)
        {
            throw AppException.Conflict(
                Msg.InvalidData,
                "Đơn thuốc này đã được thu hồi trước đó.");
        }

        // Tìm lượt khám liên quan
        var visit = (prescription.VisitId is int visitId
            ? await _repository.GetVisitForVoidAsync(visitId)
            : null)
            ?? throw AppException.Conflict(
                Msg.LoadFailed,
                "Không tìm thấy lượt khám liên quan đến đơn thuốc.");

        // Không thao tác trực tiếp trên dữ liệu thuộc lượt khám đã void
        if (visit.IsVoided)
        {
            throw AppException.Conflict(
                Msg.InvalidData,
                "Không thể thu hồi riêng đơn thuốc của lượt khám đã được thu hồi.");
        }

        if (visit.Status == VisitStatus.Completed)
        {
            throw AppException.Conflict(
                Msg.ApptImmutable,
                "Lượt khám đã đóng. Đơn thuốc của lượt khám chỉ được xem và không thể thu hồi.");
        }

        // E4 - Doctor phải là bác sĩ được phân công
        if (visit.DoctorId != currentUserId)
        {
            throw AppException.Forbidden(
                Msg.Forbidden,
                "Bác sĩ chỉ được thu hồi đơn thuốc thuộc lượt khám do mình phụ trách.");
        }

        var normalizedReason = reason.Trim();

        /*
         * Dùng RowVersion client nhận khi đọc Prescription.
         * EF Core đưa giá trị này vào điều kiện UPDATE.
         */
        _repository.ApplyOriginalRowVersion(
            prescription,
            rowVersion);

        var oldValue = new
        {
            prescription.VisitId,
            isVoided = prescription.IsVoided
        };

        /*
         * Void Prescription và hủy các MedicationLog đang Pending.
         * Các log đã ghi nhận uống/bỏ liều vẫn được giữ lại.
         */
        var cancelledPendingLogs = await VoidPrescriptionInternalAsync(
            prescription,
            normalizedReason);

        await _audit.LogAsync(
            AuditAction.Void,
            nameof(Prescription),
            prescription.Id,
            oldValue,
            new
            {
                prescription.VisitId,
                isVoided = prescription.IsVoided,
                voidedBy = currentUserId,
                role = currentRoles,
                cancelledPendingMedicationLogs = cancelledPendingLogs
            },
            normalizedReason);

        if (!await _repository.TryCommitAsync())
            throw AppException.Conflict(
                Msg.StaleVersion,
                "Đơn thuốc hoặc lịch dùng thuốc đã được người khác cập nhật. Vui lòng tải lại dữ liệu trước khi thử lại.");

        return Convert.ToBase64String(prescription.RowVer);
    }

    private async Task<int> VoidPrescriptionInternalAsync(
        Prescription prescription,
        string reason)
    {
        if (prescription.IsVoided)
            return 0;

        Mark(prescription, reason);

        var itemIds = await _repository.GetPrescriptionItemIdsAsync(prescription.Id);

        if (itemIds.Count == 0)
            return 0;

        /*
         * Chỉ hủy các liều chưa được xử lý.
         *
         * Giữ nguyên:
         * - Taken: bệnh nhân đã xác nhận uống
         * - Missed/Skipped: sự kiện thực tế đã xảy ra
         * - Các trạng thái lịch sử khác
         */
        var pendingLogs = await _repository.GetPendingMedicationLogsForItemsAsync(itemIds);

        foreach (var log in pendingLogs)
        {
            log.Status = MedicationStatus.Cancelled;
        }

        return pendingLogs.Count;
    }
}
