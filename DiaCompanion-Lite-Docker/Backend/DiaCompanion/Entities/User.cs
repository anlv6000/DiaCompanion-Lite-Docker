using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace DiaCompanion.Api.Entities;

public class User : IHasRowVersion
{
    public int Id { get; set; }                       // QT-1: INT IDENTITY, không dùng GUID clustered
    public Guid PublicId { get; set; } = Guid.NewGuid();

    /// <summary>Định danh đăng nhập của bệnh nhân (LI-6). Unique CÓ ĐIỀU KIỆN.</summary>
    /// 
    [MaxLength(20)]
    public string? Phone { get; set; }
    [MaxLength(256)]
    public string? Email { get; set; }

    [Required, MaxLength(256)] public string PasswordHash { get; set; } = "";

    [MaxLength(70)]
    public string FullName { get; set; } = "";

    /// <summary>BR-10: bắt buộc khi user được gán role Doctor; kiểm ở tầng nghiệp vụ vì Users không còn cột Role.</summary>
    [MaxLength(50, ErrorMessage = "License number must not exceed 50 characters")]
    public string? LicenseNo { get; set; }
    /// <summary>Mật khẩu tạm in ra phiếu phải đổi ở lần đăng nhập đầu.</summary>
    public bool MustChangePassword { get; set; }

    public DateTime? LastLoginAt { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? UpdatedAt { get; set; }
    public bool IsVoided { get; set; }
    public ICollection<Patient> Patients { get; set; }
    = new List<Patient>();
    public ICollection<UserRole> UserRoles { get; set; } = new List<UserRole>();

    public byte[] RowVer { get; set; } = Array.Empty<byte>();
}
