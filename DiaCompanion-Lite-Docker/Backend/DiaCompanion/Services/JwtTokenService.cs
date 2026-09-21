using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Microsoft.IdentityModel.Tokens;
using DiaCompanion.Api.Entities;

namespace DiaCompanion.Api.Services;

public interface IJwtTokenService
{
    string CreateAccessToken(User user, IReadOnlyCollection<string> roles, int? patientId, out DateTime expiresAt);
    string CreateRefreshToken(int userId, out DateTime expiresAt);
    int? ValidateRefreshToken(string token);
    SymmetricSecurityKey SigningKey { get; }
}

public class JwtTokenService : IJwtTokenService
{
    private readonly IConfiguration _cfg;
    public SymmetricSecurityKey SigningKey { get; }

    public JwtTokenService(IConfiguration cfg)
    {
        _cfg = cfg;
        var key = cfg["Jwt:SigningKey"];
        if (string.IsNullOrWhiteSpace(key))
        {
            if (Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT") == "Development")
                key = "dev-only-signing-key-do-not-use-in-production-0123456789";
            else
                throw new InvalidOperationException(
                    "Thiếu Jwt:SigningKey. Đặt biến môi trường JWT__SIGNINGKEY trước khi chạy.");
        }
        SigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(key));
    }

    public string CreateAccessToken(User user, IReadOnlyCollection<string> roles, int? patientId, out DateTime expiresAt)
    {
        var minutes = int.TryParse(_cfg["Jwt:ExpiryMinutes"], out var m) ? m : 480;
        expiresAt = DateTime.UtcNow.AddMinutes(minutes);

        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, user.Id.ToString()),
            new("fullName", user.FullName),
            new("token_type", "access"),
            new(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString())
        };
        foreach (var role in roles.Distinct(StringComparer.OrdinalIgnoreCase))
            claims.Add(new Claim(ClaimTypes.Role, role));
        //Ex: claims.Add(new Claim(ClaimTypes.Role, "Admin"));
       //        claims.Add(new Claim(ClaimTypes.Role, "Doctor"));
        if (patientId is int pid) claims.Add(new Claim("patientId", pid.ToString()));
        if (user.MustChangePassword) claims.Add(new Claim("mustChangePassword", "true"));

        return WriteToken(claims, expiresAt);
    }

    public string CreateRefreshToken(int userId, out DateTime expiresAt)
    {
        var days = int.TryParse(_cfg["Jwt:RefreshExpiryDays"], out var d) ? d : 30;
        expiresAt = DateTime.UtcNow.AddDays(days);
        return WriteToken(new[]
        {
            new Claim(ClaimTypes.NameIdentifier, userId.ToString()),
            new Claim("token_type", "refresh"),
            new Claim(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString())
        }, expiresAt);
    }

    public int? ValidateRefreshToken(string token)
    {
        if (string.IsNullOrWhiteSpace(token)) return null;
        try
        {
            var principal = new JwtSecurityTokenHandler().ValidateToken(token, new TokenValidationParameters
            {
                ValidateIssuer = true,
                ValidateAudience = true,
                ValidateLifetime = true,
                ValidateIssuerSigningKey = true,
                ValidIssuer = _cfg["Jwt:Issuer"],
                ValidAudience = _cfg["Jwt:Audience"],
                IssuerSigningKey = SigningKey,
                ClockSkew = TimeSpan.FromSeconds(30)
            }, out _);

            if (!string.Equals(principal.FindFirst("token_type")?.Value, "refresh", StringComparison.Ordinal))
                return null;
            return int.TryParse(principal.FindFirst(ClaimTypes.NameIdentifier)?.Value, out var id) ? id : null;
        }
        catch (SecurityTokenException) { return null; }
        catch (ArgumentException) { return null; }
    }

    private string WriteToken(IEnumerable<Claim> claims, DateTime expiresAt)
    {
        var token = new JwtSecurityToken(
            issuer: _cfg["Jwt:Issuer"],
            audience: _cfg["Jwt:Audience"],
            claims: claims,
            expires: expiresAt,
            signingCredentials: new SigningCredentials(SigningKey, SecurityAlgorithms.HmacSha256));
        return new JwtSecurityTokenHandler().WriteToken(token);
    }
}
