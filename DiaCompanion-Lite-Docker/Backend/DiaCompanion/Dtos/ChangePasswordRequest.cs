using System.ComponentModel.DataAnnotations;
using DiaCompanion.Api.Common;

namespace DiaCompanion.Api.Dtos;

public class ChangePasswordRequest
{
      /// <summary>
    /// Bắt buộc khi đổi mật khẩu chủ động.
    /// Được phép bỏ trống khi tài khoản đang ở trạng thái MustChangePassword.
    /// </summary>
    // TUYỆT ĐỐI KHÔNG trim password
    public string? CurrentPassword { get; set; }

    // TUYỆT ĐỐI KHÔNG trim password
    [Required]
    public string NewPassword { get; set; } = "";
}
