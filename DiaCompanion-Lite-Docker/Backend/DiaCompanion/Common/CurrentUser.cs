using System.Security.Claims;

namespace DiaCompanion.Api.Common;

public interface ICurrentUser
{
    int? Id { get; }
    string? FullName { get; }
    IReadOnlyCollection<string> Roles { get; }
    int? PatientId { get; }
    string? Ip { get; }
    bool IsInRole(params string[] roles);
    int RequireId();
}

public class CurrentUser : ICurrentUser
{
    private readonly IHttpContextAccessor _http;
    public CurrentUser(IHttpContextAccessor http) => _http = http;

    private ClaimsPrincipal? P => _http.HttpContext?.User;

    public int? Id => int.TryParse(P?.FindFirst(ClaimTypes.NameIdentifier)?.Value, out var i) ? i : null;
    public string? FullName => P?.FindFirst("fullName")?.Value;
    public IReadOnlyCollection<string> Roles => P?.FindAll(ClaimTypes.Role)
        .Select(c => c.Value)
        .Where(v => !string.IsNullOrWhiteSpace(v))
        .Distinct(StringComparer.OrdinalIgnoreCase)
        .ToArray() ?? Array.Empty<string>();
    public int? PatientId => int.TryParse(P?.FindFirst("patientId")?.Value, out var i) ? i : null;
    public string? Ip => _http.HttpContext?.Connection.RemoteIpAddress?.ToString();

    public bool IsInRole(params string[] roles)
    {
        if (roles.Length == 0) return false;
        var current = Roles;
        return roles.Any(expected => current.Any(actual =>
            string.Equals(actual, expected, StringComparison.OrdinalIgnoreCase)));
    }

    public int RequireId() => Id ?? throw AppException.Unauthorized(Msg.SessionExpired, "Phiên đăng nhập đã hết hạn.");
}
