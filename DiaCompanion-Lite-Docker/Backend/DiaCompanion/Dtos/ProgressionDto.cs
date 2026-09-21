using System.ComponentModel.DataAnnotations;
using DiaCompanion.Api.Common;

namespace DiaCompanion.Api.Dtos;

/* ============================== PROGRESSION ============================= */

/// <summary>UC-29: ghép ba chuỗi để nối biến chứng mắt với mức kiểm soát bệnh gốc.</summary>
public class ProgressionDto
{
    public List<ProgressionPoint> Points { get; set; } = new();
    public string? TrendWarning { get; set; }
}
