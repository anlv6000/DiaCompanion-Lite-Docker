using System.ComponentModel.DataAnnotations;
using DiaCompanion.Api.Common;

namespace DiaCompanion.Api.Dtos;

public class ReviewDto
{
    public int Id { get; set; }
    public int AiDiagnosisId { get; set; }
    public byte Action { get; set; }
    public string ActionLabel { get; set; } = "";
    public byte FinalGrade { get; set; }
    public string FinalGradeLabel { get; set; } = "";
    public string? Reason { get; set; }
    public string DoctorName { get; set; } = "";
    public DateTime CreatedAt { get; set; }
    public string RowVersion { get; set; } = "";
}
