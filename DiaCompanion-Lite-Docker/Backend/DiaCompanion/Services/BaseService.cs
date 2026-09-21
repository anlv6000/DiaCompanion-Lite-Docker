using Microsoft.AspNetCore.Mvc;
using DiaCompanion.Api.Common;

namespace DiaCompanion.Api.Services;

/// <summary>Base class chỉ chứa helper nghiệp vụ dùng chung, không truy cập persistence.</summary>
public abstract class BaseService : ControllerBase
{
    /// <summary>User có role Patient, kể cả đồng thời có staff role.</summary>
    protected static bool HasPatientRole(ICurrentUser me) =>
        me.IsInRole(Roles.Patient);

    /// <summary>User có ít nhất một role nhân viên.</summary>
    protected static bool HasStaffRole(ICurrentUser me) =>
        me.IsInRole(Roles.Admin, Roles.Doctor, Roles.Receptionist);

    /// <summary>Chỉ là Patient, không đồng thời là nhân viên.</summary>
    protected static bool IsPatientOnly(ICurrentUser me) =>
        HasPatientRole(me) && !HasStaffRole(me);

    protected int RequireMyPatientId(ICurrentUser me) =>
        me.PatientId ?? throw AppException.Forbidden(
            Msg.Forbidden,
            "Tài khoản của bạn chưa được gắn với hồ sơ bệnh án.");

    /// <summary>
    /// Dùng cho READ.
    /// Patient thuần chỉ được xem hồ sơ của mình.
    /// Nếu đồng thời có staff role thì quyền staff được ưu tiên.
    /// </summary>
    protected void EnsureCanReadPatient(ICurrentUser me, int patientId)
    {
        if (IsPatientOnly(me) && me.PatientId != patientId)
        {
            throw AppException.Forbidden(
                Msg.Forbidden,
                "Bạn không có quyền xem hồ sơ này.");
        }
    }

    /// <summary>
    /// Dùng cho Patient self-service WRITE.
    /// Dù User đồng thời là Doctor/Admin/Receptionist,
    /// endpoint Patient vẫn chỉ được thao tác dữ liệu Patient của chính mình.
    /// </summary>
    protected void EnsureOwnPatient(ICurrentUser me, int patientId)
    {
        var myPatientId = RequireMyPatientId(me);

        if (myPatientId != patientId)
        {
            throw AppException.Forbidden(
                Msg.Forbidden,
                "Bạn chỉ được thao tác trên dữ liệu của chính mình.");
        }
    }

    /// <summary>
    /// Dùng cho API READ có patientId optional.
    ///
    /// Có patientId:
    ///     hiểu là đang chủ động xem Patient đó.
    ///
    /// Không có patientId + có Patient role:
    ///     hiểu là "hồ sơ của tôi".
    ///
    /// Staff thuần không truyền patientId:
    ///     báo thiếu patientId.
    /// </summary>
    protected int ResolvePatientId(
        ICurrentUser me,
        int? requestedPatientId)
    {
        if (requestedPatientId.HasValue)
        {
            EnsureCanReadPatient(me, requestedPatientId.Value);
            return requestedPatientId.Value;
        }

        if (HasPatientRole(me))
        {
            return RequireMyPatientId(me);
        }

        throw AppException.BadRequest(
            Msg.RequiredFields,
            "Cần chỉ định patientId.");
    }
}