using DiaCompanion.Api.Common;
using DiaCompanion.Api.Entities;

namespace DiaCompanion.Api.Repositories;

/// <summary>Một ảnh trong hàng chờ chấm mù (không mang thông tin bệnh nhân hay AI).</summary>
public sealed record GradingCandidate(int ImageId, DateTime UploadedAt);

/// <summary>Ảnh + bệnh nhân + chỉ số của đúng lượt khám chứa ảnh (cho khung thông tin lâm sàng khi xem ảnh).</summary>
public sealed record ImageClinicalContext(
    FundusImage Image,
    Patient Patient,
    DateTime? VisitDate,
    IReadOnlyList<HealthMetric> Metrics);

/// <summary>Một dòng của bảng chấm mù để kết xuất: mỗi ảnh một dòng.</summary>
public sealed record GradingExportRow(
    int ImageId,
    string? PatientCode,
    int? VisitId,
    Eye Eye,
    QualityStatus QualityStatus,
    DateTime UploadedAt,
    bool HasAi,
    IReadOnlyList<BlindGrade> Grades,
    string? Sha256 = null);

public partial interface IRepository
{
    Task<BlindGrade?> GetBlindGradeAsync(int imageId, byte round, CancellationToken ct = default);
    Task<IReadOnlyList<BlindGrade>> GetBlindGradesForImageAsync(int imageId, CancellationToken ct = default);
    Task<IReadOnlyList<BlindGrade>> GetBlindGradesForImagesAsync(IReadOnlyCollection<int> imageIds, CancellationToken ct = default);
    Task<bool> ImageHasAiDiagnosisAsync(int imageId, CancellationToken ct = default);

    /// <summary>Ảnh đã có vòng 1, chưa có vòng 2, và người chấm này chưa chấm ảnh đó ở vòng nào.</summary>
    Task<IReadOnlyList<GradingCandidate>> GetRound2CandidatesAsync(int graderId, CancellationToken ct = default);

    /// <summary>Ảnh có đủ vòng 1 và 2, chưa có vòng 3, người chấm này không tham gia vòng 1/2 của ảnh.
    /// Việc lọc "bất đồng" do service quyết định.</summary>
    Task<IReadOnlyList<(GradingCandidate Image, BlindGrade R1, BlindGrade R2)>> GetRound3PoolAsync(int graderId, CancellationToken ct = default);

    Task<IReadOnlyList<GradingExportRow>> GetGradingExportRowsAsync(CancellationToken ct = default);
    Task<ImageClinicalContext?> GetImageClinicalContextAsync(int imageId, CancellationToken ct = default);
}
