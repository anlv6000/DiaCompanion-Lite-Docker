using System.Security.Cryptography;
using DiaCompanion.Api.Common;

namespace DiaCompanion.Api.Services;

/// <summary>
/// QT-3: PBKDF2-HMAC-SHA256, 100.000 vòng — đúng cam kết QA-07 trong Report 3.
/// Định dạng lưu: PBKDF2$&lt;iterations&gt;$&lt;base64 salt&gt;$&lt;base64 hash&gt;
/// Salt nằm trong chuỗi nên KHÔNG cần cột salt riêng.
/// </summary>
public interface IPasswordHasher
{
    string Hash(string password);
    bool Verify(string password, string stored);
    void EnsureStrong(string password);
    string GenerateTempPassword();
}

public class PasswordHasher : IPasswordHasher
{
    private const int Iterations = 100_000;
    private const int SaltSize = 16;
    private const int KeySize = 32;

    public string Hash(string password)
    {
        var salt = RandomNumberGenerator.GetBytes(SaltSize);
        var key = Rfc2898DeriveBytes.Pbkdf2(password, salt, Iterations, HashAlgorithmName.SHA256, KeySize);
        return $"PBKDF2${Iterations}${Convert.ToBase64String(salt)}${Convert.ToBase64String(key)}";
    }

    public bool Verify(string password, string stored)
    {
        var parts = stored.Split('$');
        if (parts.Length != 4 || parts[0] != "PBKDF2") return false;
        if (!int.TryParse(parts[1], out var iter)) return false;

        var salt = Convert.FromBase64String(parts[2]);
        var expected = Convert.FromBase64String(parts[3]);
        var actual = Rfc2898DeriveBytes.Pbkdf2(password, salt, iter, HashAlgorithmName.SHA256, expected.Length);

        // So sánh thời gian hằng số để không rò rỉ thông tin qua thời gian phản hồi
        return CryptographicOperations.FixedTimeEquals(actual, expected);
    }

    public void EnsureStrong(string password)
    {
        if (password.Length < 8 || !password.Any(char.IsLetter) || !password.Any(char.IsDigit))
            throw AppException.BadRequest(Msg.WeakPassword,
                "Mật khẩu phải có tối thiểu 8 ký tự, gồm cả chữ và số.");
    }

    /// <summary>
    /// Mật khẩu tạm 6 số để nhân viên in ra phiếu đưa bệnh nhân.
    /// Kèm MustChangePassword = true — nếu không, ai nhặt được phiếu khám
    /// cũng vào được hồ sơ bệnh án.
    /// </summary>
    public string GenerateTempPassword() => RandomNumberGenerator.GetInt32(100_000, 999_999).ToString();
}
