using System.ComponentModel.DataAnnotations;
using DiaCompanion.Api.Common;

namespace DiaCompanion.Dtos
{
    public class CreateMedicalRecordRequest
    {
        [Range(1, int.MaxValue)]
        public int PatientId { get; set; }
    }
}
