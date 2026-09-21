using DiaCompanion.Api.Common;

namespace DiaCompanion.Dtos
{
    public class MedicalRecordDto
    {
        public int Id { get; set; }

        public int PatientId { get; set; }

        public string PatientCode { get; set; } = string.Empty;

        public string PatientName { get; set; } = string.Empty;

        public string RecordCode { get; set; } = string.Empty;

        public DateTime CreatedAt { get; set; }

        public int? CreatedByUserId { get; set; }

        public string? CreatedByUserName { get; set; }

        public DateTime? UpdatedAt { get; set; }

        public string RowVersion { get; set; } = string.Empty;
    }
}
