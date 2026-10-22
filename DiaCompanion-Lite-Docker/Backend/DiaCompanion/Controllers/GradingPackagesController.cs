using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using DiaCompanion.Api.Common;
using DiaCompanion.Api.Dtos;
using DiaCompanion.Api.Services;

namespace DiaCompanion.Api.Controllers;

/// <summary>
/// 1.3.1: gói chấm mù cho máy khác (chấm offline). Chỉ vai trò Research hoặc Admin (người quản lý nghiên cứu).
/// Bác sĩ khám và người chấm không tạo/nhập gói, để người chấm không tự nhập kết quả của chính mình
/// và bác sĩ không thấy ai chấm ảnh nào.
/// </summary>
[Route("api/grading-packages")]
[Authorize(Roles = Roles.ResearchExport)]
public class GradingPackagesController : BaseApiController
{
    private readonly IGradingPackageService _service;

    public GradingPackagesController(IGradingPackageService service) => _service = service;

    [HttpGet]
    public Task<List<GradingPackageDto>> List(CancellationToken ct) => _service.List(ct);

    /// <summary>Tài khoản người chấm (vai trò Grader) kèm số ảnh còn chấm được ở vòng 2 / 3.</summary>
    [HttpGet("graders")]
    public Task<List<GraderAccountDto>> Graders(CancellationToken ct) => _service.Graders(ct);

    [HttpPost]
    public Task<GradingPackageDto> Create(CreateGradingPackageRequest req, CancellationToken ct) => _service.Create(req, ct);

    /// <summary>Tệp zip của gói (tải lại được nhiều lần; nội dung giống nhau).</summary>
    [HttpGet("{id:int}/download")]
    public Task<IActionResult> Download(int id, CancellationToken ct) => _service.Download(id, ct);

    [HttpPost("{id:int}/cancel")]
    public Task<GradingPackageDto> Cancel(int id, CancellationToken ct) => _service.Cancel(id, ct);

    /// <summary>Nhập tệp kết quả KET-QUA-….json. Nhập lại cùng tệp không ghi trùng.</summary>
    [HttpPost("import")]
    [RequestSizeLimit(12 * 1024 * 1024)]
    [Consumes("multipart/form-data")]
    public Task<GradingImportResultDto> Import(IFormFile file, CancellationToken ct) => _service.Import(file, ct);
}
