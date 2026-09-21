namespace DiaCompanion.Api.Services;

public interface ISmsSender
{
    Task SendAsync(
        string phoneNumber,
        string message,
        string source,
        CancellationToken cancellationToken = default);
}