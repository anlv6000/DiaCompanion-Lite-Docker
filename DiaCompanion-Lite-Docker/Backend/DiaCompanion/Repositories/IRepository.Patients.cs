using DiaCompanion.Api.Common;
using DiaCompanion.Api.Entities;

namespace DiaCompanion.Api.Repositories;

public sealed record PatientSearchRow(
    int Id,
    string Code,
    string FullName,
    byte Gender,
    string Phone,
    DateOnly DateOfBirth,
    byte DiabetesType,
    short? DiabetesDurationYears,
    bool HasAccount,
    byte? LatestDrGrade,
    DateTime? LatestVisitDate);

public sealed record PatientSearchPage(IReadOnlyList<PatientSearchRow> Items, int Total);
public sealed record PatientDetailStats(string? DoctorInCharge, byte? LatestDrGrade, int VisitCount);

public partial interface IRepository
{
    Task<PatientSearchPage> SearchPatientsAsync(
        string? normalizedKeyword,
        string? rawKeyword,
        byte? diabetesType,
        byte? grade,
        PageQuery page,
        CancellationToken ct = default);

    Task<bool> PatientPhoneExistsAsync(string phone, int? exceptPatientId = null, CancellationToken ct = default);
    Task<bool> UserPhoneExistsAsync(string phone, int? exceptUserId = null, CancellationToken ct = default);
    Task<User?> GetUserForUpdateAsync(int userId, CancellationToken ct = default);
    Task<string?> GetLastPatientCodeAsync(string prefix, CancellationToken ct = default);
    Task<PatientDetailStats> GetPatientDetailStatsAsync(int patientId, CancellationToken ct = default);
    Task<bool> EnsureUserRoleActiveAsync(User user, string roleName, int? assignedBy, CancellationToken ct = default);
    Task<bool> SetUserRoleActiveAsync(int userId, string roleName, bool isActive, int? changedBy, CancellationToken ct = default);

    Task<bool> UserHasNonPatientRoleAssignmentAsync(
    int userId,
    CancellationToken ct = default);

    public sealed record AdminPatientData(
    Patient Patient,
    User? User,
    bool? PatientRoleIsActive);

    public sealed record AdminPatientPage(
        IReadOnlyList<AdminPatientData> Items,
        int Total);

    public sealed record PatientAdminTarget(
        Patient Patient,
        User? User,
        UserRole? PatientRole);
}
