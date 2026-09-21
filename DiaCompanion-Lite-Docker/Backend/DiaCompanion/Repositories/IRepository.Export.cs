using DiaCompanion.Api.Entities;

namespace DiaCompanion.Api.Repositories;

public partial interface IRepository
{
    Task<Visit?> GetVisitForExportAsync(int visitId, CancellationToken ct = default);
    Task<IReadOnlyList<DiagnosisReview>> GetVisitDiagnosisReviewsForExportAsync(int visitId, CancellationToken ct = default);
    Task<IReadOnlyList<Prescription>> GetVisitPrescriptionsForExportAsync(int visitId, CancellationToken ct = default);
    Task<IReadOnlyList<HealthMetric>> GetVisitHealthMetricsForExportAsync(int visitId, CancellationToken ct = default);
    Task<IReadOnlyList<DiagnosisReview>> GetDiagnosisReviewsForExportAsync(
        int? modelVersionId,
        DateTime? fromUtc,
        DateTime? toExclusiveUtc,
        bool overridesOnly,
        CancellationToken ct = default);
    /// <summary>
    /// Bộ dữ liệu nghiên cứu Gap 2: MỖI DÒNG = một lần chạy AI của một mắt
    /// (kể cả chưa được bác sĩ xác nhận), đã loại bản ghi void. Kèm review nếu có
    /// để lấy phân độ tham chiếu của bác sĩ.
    /// </summary>
    Task<IReadOnlyList<AiDiagnosis>> GetResearchDatasetAsync(
        DateTime? fromUtc,
        DateTime? toExclusiveUtc,
        CancellationToken ct = default);

    /// <summary>Chỉ số sức khỏe (theo lượt khám) của nhiều lượt, để ghép vào dataset nghiên cứu.</summary>
    Task<IReadOnlyList<HealthMetric>> GetHealthMetricsForVisitsAsync(
        IReadOnlyCollection<int> visitIds,
        CancellationToken ct = default);
}
