using DiaCompanion.Api.Common;


namespace DiaCompanion.Api.Dtos;

public class LoginResponse
{
    public string Token { get; set; } = "";
    public DateTime ExpiresAt { get; set; }
    public string RefreshToken { get; set; } = "";
    public DateTime RefreshTokenExpiresAt { get; set; }
    public int UserId { get; set; }
    public string FullName { get; set; } = "";

    /// <summary>Giữ tương thích client cũ: vai trò ưu tiên theo business.</summary>
    public string Role { get; set; } = "";
    public List<string> Roles { get; set; } = new();

    public int? PatientId { get; set; }
    public bool MustChangePassword { get; set; }
    public string DefaultRoute { get; set; } = "";
}
