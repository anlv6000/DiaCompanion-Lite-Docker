using DiaCompanion.Api.Common;
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

    /// <summary>Ảnh còn hiệu lực của một mắt trong lượt khám (1.3.1: mỗi mắt chỉ một ảnh còn hiệu lực).</summary>
    Task<FundusImage?> GetActiveImageForEyeAsync(int visitId, Eye eye, CancellationToken ct = default);
}
