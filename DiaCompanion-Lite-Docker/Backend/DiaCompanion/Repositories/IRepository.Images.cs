using DiaCompanion.Api.Entities;

namespace DiaCompanion.Api.Repositories;

public partial interface IRepository
{
    Task<IReadOnlyList<FundusImage>> GetFundusImagesAsync(int? patientId, int? visitId, CancellationToken ct = default);
    Task<Visit?> GetVisitForPatientAsync(int visitId, int patientId, bool tracking = false, CancellationToken ct = default);
    Task<Patient?> GetPatientAsync(int patientId, bool tracking = false, CancellationToken ct = default);
    Task<FundusImage?> GetFundusImageAsync(int id, bool tracking = false, CancellationToken ct = default);
    Task<FundusImage?> GetFundusImageWithVisitForUpdateAsync(int id, CancellationToken ct = default);
    Task<bool> HasDiagnosisForImageAsync(int imageId, CancellationToken ct = default);
}
