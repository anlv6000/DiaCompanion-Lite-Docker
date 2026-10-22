using Microsoft.AspNetCore.Mvc;
using DiaCompanion.Api.Dtos;

namespace DiaCompanion.Api.Services;

public interface IGradingService
{
    Task<BlindGradeDto> SubmitRound1(int imageId, BlindGradeRequest req, CancellationToken ct = default);
    Task<BlindGradeDto?> GetRound1(int imageId, CancellationToken ct = default);
    Task<GradingQueueDto> Queue(byte round, CancellationToken ct = default);
    Task<IActionResult> Content(int imageId, CancellationToken ct = default);
    Task<BlindGradeDto> SubmitGrader(byte round, int imageId, BlindGradeRequest req, CancellationToken ct = default);
    Task<ImageClinicalContextDto> ClinicalContext(int imageId, CancellationToken ct = default);
    Task<GradingProgressDto> Progress(CancellationToken ct = default);

    /// <summary>1.3.1: ảnh người chấm này được chấm ở vòng 2/3 (đã loại ảnh trong gói còn mở).</summary>
    Task<List<int>> EligibleImageIdsFor(byte round, int graderId, CancellationToken ct = default);
}
