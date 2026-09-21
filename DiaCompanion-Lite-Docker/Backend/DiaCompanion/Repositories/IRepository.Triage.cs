using DiaCompanion.Api.Common;
using DiaCompanion.Api.Entities;

namespace DiaCompanion.Api.Repositories;

public sealed record TriageQueueRow(
    int Id, DrGrade DrGrade, decimal? Disagreement, bool IsDeferred,
    DeferReason? DeferReason, DateTime CreatedAt, byte[] RowVer, Eye Eye, int? VisitId,
    int PatientId, string PatientCode, string PatientName, int? DoctorId, string? DoctorName)
{
    public byte? ClinicalRiskScore { get; internal set; }
}

public sealed record TriageQueuePage(IReadOnlyList<TriageQueueRow> Items, bool HasMore);
public sealed record TriageCounts(int Pending, int Deferred);

public partial interface IRepository
{
    Task<TriageQueuePage> GetTriageQueueAsync(
        int? currentDoctorId, int? filterDoctorId, bool? deferredOnly, string? q,
        DateTime? cursorAt, long? cursorId, int size, CancellationToken ct = default);
    Task<TriageCounts> GetTriageCountsAsync(int? currentDoctorId, CancellationToken ct = default);
    Task<AiDiagnosis?> GetDiagnosisForReviewAsync(int diagnosisId, CancellationToken ct = default);
    Task<bool> ReviewExistsForDiagnosisAsync(int diagnosisId, CancellationToken ct = default);
    Task<bool> TryCommitReviewAsync(CancellationToken ct = default);
    Task<DiagnosisReview?> GetReviewAsync(int reviewId, CancellationToken ct = default);
    
}
