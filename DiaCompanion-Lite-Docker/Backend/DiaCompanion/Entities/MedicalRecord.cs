using DiaCompanion.Api.Entities;

namespace DiaCompanion.Entities
{
    public class MedicalRecord
    {
        public int Id { get; set; }

        public int PatientId { get; set; }

        public string RecordCode { get; set; } = null!;

        public DateTime CreatedAt { get; set; }

        public int? CreatedByUserId { get; set; }

        public DateTime? UpdatedAt { get; set; }

        public int? UpdatedByUserId { get; set; }

        public bool IsVoided { get; set; }

        public DateTime? VoidedAt { get; set; }

        public int? VoidedByUserId { get; set; }

        public string? VoidReason { get; set; }

        // SQL:
        // RowVersion ROWVERSION NOT NULL
        public byte[] RowVersion { get; set; } = Array.Empty<byte>();


        // ============================================================
        // Navigation
        // ============================================================

        public Patient Patient { get; set; } = null!;

        public User? CreatedByUser { get; set; }

        public User? UpdatedByUser { get; set; }

        public User? VoidedByUser { get; set; }

        public ICollection<Visit> Visits { get; set; } = new List<Visit>();
    }
}
