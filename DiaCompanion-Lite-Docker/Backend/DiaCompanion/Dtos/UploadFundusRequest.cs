using System.ComponentModel.DataAnnotations;
using DiaCompanion.Api.Common;

namespace DiaCompanion.Api.Dtos;

/// <summary>
/// UC-22 — tham số nạp ảnh.
/// Gom vào một lớp thay vì để [FromForm] rời rạc: Swashbuckle không sinh được
/// tài liệu khi IFormFile đứng chung với các tham số [FromForm] đơn lẻ.
/// </summary>
public class UploadFundusRequest
{
    [Required] public IFormFile File { get; set; } = default!;
    [Required] public int PatientId { get; set; }
    [Required] public int VisitId { get; set; }
    [Required] public Eye Eye { get; set; }
}
