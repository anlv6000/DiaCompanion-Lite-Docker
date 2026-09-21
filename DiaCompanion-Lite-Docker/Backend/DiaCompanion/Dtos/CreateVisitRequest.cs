using System.ComponentModel.DataAnnotations;
using DiaCompanion.Api.Common;

namespace DiaCompanion.Api.Dtos;

public class CreateVisitRequest
{
    [Required] public int PatientId { get; set; }

    [Range(1, int.MaxValue, ErrorMessage = "Vui lòng chọn bác sĩ phụ trách.")]
    public int DoctorId { get; set; }
}
