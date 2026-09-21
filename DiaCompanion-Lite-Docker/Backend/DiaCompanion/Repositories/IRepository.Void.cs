using DiaCompanion.Api.Entities;
using DiaCompanion.Dtos;

namespace DiaCompanion.Api.Repositories;

public partial interface IRepository
{
    Task<ShiftDoctorInformation> shiftDoctorInformation(int id, CancellationToken ct = default);
    Task<bool> ExistVisitPatientIds(int id, CancellationToken ct = default);
    Task<Patient?> GetPatientForVoidAsync(int id, CancellationToken ct = default);
    Task<Visit?> GetVisitForVoidAsync(int id, CancellationToken ct = default);
    Task<FundusImage?> GetImageForVoidAsync(int id, CancellationToken ct = default);
    Task<AiDiagnosis?> GetDiagnosisForVoidAsync(int id, CancellationToken ct = default);
    Task<DiagnosisReview?> GetReviewForVoidAsync(int id, CancellationToken ct = default);
    Task<Prescription?> GetPrescriptionForVoidAsync(int id, CancellationToken ct = default);

    Task<IReadOnlyList<Visit>> GetActiveVisitsForPatientAsync(int patientId, CancellationToken ct = default);
    Task<IReadOnlyList<FundusImage>> GetActiveOrphanImagesForPatientAsync(int patientId, CancellationToken ct = default);
    Task<IReadOnlyList<FundusImage>> GetActiveImagesForVisitAsync(int visitId, CancellationToken ct = default);
    Task<IReadOnlyList<Prescription>> GetActivePrescriptionsForVisitAsync(int visitId, CancellationToken ct = default);
    Task<IReadOnlyList<AiDiagnosis>> GetActiveDiagnosesForImageAsync(int imageId, CancellationToken ct = default);
    Task<IReadOnlyList<DiagnosisReview>> GetActiveReviewsForDiagnosisAsync(int diagnosisId, CancellationToken ct = default);
    Task<bool> VisitHasFundusImageAsync(int visitId, CancellationToken ct = default);
    Task<bool> VisitHasPrescriptionAsync(int visitId, CancellationToken ct = default);
    Task<bool> IsVisitAssignedToDoctorAsync(int visitId, int doctorId, bool includeVoided = false, CancellationToken ct = default);
    Task<IReadOnlyList<int>> GetPrescriptionItemIdsAsync(int prescriptionId, CancellationToken ct = default);
}
