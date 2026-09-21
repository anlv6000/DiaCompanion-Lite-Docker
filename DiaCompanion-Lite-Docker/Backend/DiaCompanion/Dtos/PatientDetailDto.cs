using System.ComponentModel.DataAnnotations;
using DiaCompanion.Api.Common;

namespace DiaCompanion.Api.Dtos;

public class PatientDetailDto : PatientListItemDto
{
    public DateOnly DateOfBirth { get; set; }
    public string? Address { get; set; }
    public decimal? BaselineHbA1c { get; set; }
    public string? Note { get; set; }
    public DateTime CreatedAt { get; set; }
    public string? DoctorInCharge { get; set; }
    public int VisitCount { get; set; }

    public string RowVersion { get; set; } = "";
}
