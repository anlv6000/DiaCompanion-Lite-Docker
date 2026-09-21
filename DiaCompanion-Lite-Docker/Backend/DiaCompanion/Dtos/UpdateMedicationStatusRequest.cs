using System.ComponentModel.DataAnnotations;
using DiaCompanion.Api.Common;

namespace DiaCompanion.Api.Dtos;

public class UpdateMedicationStatusRequest
{
    [Required]
    public MedicationStatus Status { get; set; }

    [Required]
    public string RowVersion { get; set; } = "";
}
