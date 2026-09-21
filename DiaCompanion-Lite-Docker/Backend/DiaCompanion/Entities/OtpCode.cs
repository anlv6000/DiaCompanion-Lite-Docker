using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using DiaCompanion.Api.Common;

namespace DiaCompanion.Api.Entities;

public class OtpCode
{
    public long Id { get; set; }
    [Required, MaxLength(20)] public string Phone { get; set; } = "";
    /// <summary>Không lưu OTP dạng thô — nếu CSDL rò rỉ thì mã vẫn không dùng được.</summary>
    [Required, MaxLength(256)] public string CodeHash { get; set; } = "";
    public OtpPurpose Purpose { get; set; } = OtpPurpose.Login;
    public DateTime ExpiresAt { get; set; }
    public DateTime? ConsumedAt { get; set; }
    public byte AttemptCount { get; set; }
    /// <summary>NULL nếu bệnh nhân tự yêu cầu; có giá trị nếu quầy tiếp đón cấp hộ.</summary>
    public int? IssuedBy { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
