using DiaCompanion.Api.Common;
using DiaCompanion.Api.Entities;

namespace DiaCompanion.Api.Repositories;

public sealed record ConfirmedProgressionRow(DateTime CreatedAt, int? VisitId, DrGrade Grade, decimal? FractalDimension);
public sealed record Hba1cProgressionRow(DateTime RecordedAtUtc, decimal Value);

public partial interface IRepository
{
    Task<bool> IsImageReviewedAsync(int imageId, CancellationToken ct = default);
    Task<IReadOnlyList<ModelVersion>> GetActiveModelVersionsAsync(CancellationToken ct = default);
    Task<IReadOnlyList<AiDiagnosis>> GetDiagnosesForImageForUpdateAsync(int imageId, CancellationToken ct = default);
    Task<IReadOnlyList<int>> GetDiagnosisIdsByImageAsync(int imageId, CancellationToken ct = default);
    Task<AiDiagnosis?> GetDiagnosisWithImageAsync(int id, CancellationToken ct = default);
    Task<IReadOnlyList<ConfirmedProgressionRow>> GetConfirmedProgressionAsync(int patientId, DateTime from, CancellationToken ct = default);
    Task<IReadOnlyList<Hba1cProgressionRow>> GetHba1cProgressionAsync(int patientId, DateTime from, CancellationToken ct = default);
    Task<AiDiagnosis?> GetDiagnosisDetailAsync(int id, CancellationToken ct = default);
    Task<DiagnosisReview?> GetReviewByDiagnosisAsync(int diagnosisId, CancellationToken ct = default);
}
