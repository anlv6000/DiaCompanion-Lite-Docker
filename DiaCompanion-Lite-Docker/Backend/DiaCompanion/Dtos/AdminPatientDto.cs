using DiaCompanion.Api.Common;
namespace DiaCompanion.Dtos
{
   

    public class AdminPatientDto
    {
        public int Id { get; set; }

        public int? UserId { get; set; }

        public string Code { get; set; } = string.Empty;

        public string FullName { get; set; } = string.Empty;

        public byte Gender { get; set; }

        public string Phone { get; set; } = string.Empty;

        public string? Address { get; set; }

        public bool HasAccount { get; set; }

        // Lấy từ UserRoles.IsActive của role Patient.
        // null = Patient chưa có User account.
        public bool? IsActive { get; set; }

        // RowVersion Patient dùng khi sửa thông tin.
        public string PatientRowVersion { get; set; } = string.Empty;

        // RowVersion User dùng khi khóa/mở Patient role.
        public string? AccountRowVersion { get; set; }
    }
}
