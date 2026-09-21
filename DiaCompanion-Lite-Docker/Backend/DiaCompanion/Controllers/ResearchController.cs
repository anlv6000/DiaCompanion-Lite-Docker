using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using DiaCompanion.Api.Common;
using DiaCompanion.Api.Services;

namespace DiaCompanion.Api.Controllers;

/// <summary>
/// Kết xuất dữ liệu nghiên cứu cho Gap 2 (đánh giá cơ chế deferral).
/// Chỉ vai trò Research hoặc Admin. Vai trò Research KHÔNG có quyền lâm sàng nào khác.
/// Dữ liệu đã bỏ định danh; mỗi lần kết xuất được ghi vào nhật ký kiểm toán.
/// </summary>
[Route("api/research")]
[Authorize(Roles = Roles.ResearchExport)]
public class ResearchController : BaseApiController
{
    private readonly IExportService _export;

    public ResearchController(IExportService export) => _export = export;

    /// <summary>
    /// Toàn bộ dataset nghiên cứu ở dạng CSV: mỗi dòng là một lần chạy AI của một
    /// mắt (kể cả chưa xác nhận), kèm biến lâm sàng, nội bộ deferral và phân độ
    /// tham chiếu của bác sĩ. Lọc theo khoảng ngày (giờ địa phương) nếu cần.
    /// </summary>
    [HttpGet("dataset.csv")]
    public Task<IActionResult> Dataset([FromQuery] DateOnly? from, [FromQuery] DateOnly? to)
        => _export.ResearchDatasetCsv(from, to);
}
