using DiaCompanion.Api.Common;

namespace DiaCompanion.Api.Dtos;

public class FeedbackDto
{
    public int Id { get; set; }
    public int PatientId { get; set; }
    public string PatientCode { get; set; } = "";
    public string PatientName { get; set; } = "";
    public int? VisitId { get; set; }
    public byte Rating { get; set; }
    public string? Tags { get; set; }
    public string? Comment { get; set; }
    public DateTime CreatedAt { get; set; }
}
