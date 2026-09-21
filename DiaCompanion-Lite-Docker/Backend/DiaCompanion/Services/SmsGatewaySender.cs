using System.Net.Http.Json;

namespace DiaCompanion.Api.Services;

public sealed class SmsGatewaySender : ISmsSender
{
    private readonly HttpClient _http;
    private readonly IConfiguration _configuration;
    private readonly ILogger<SmsGatewaySender> _logger;

    public SmsGatewaySender(
        HttpClient http,
        IConfiguration configuration,
        ILogger<SmsGatewaySender> logger)
    {
        _http = http;
        _configuration = configuration;
        _logger = logger;
    }

    public async Task SendAsync(
        string phoneNumber,
        string message,
        string source,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(phoneNumber))
            throw new ArgumentException(
                "Phone number không được để trống.",
                nameof(phoneNumber));

        if (string.IsNullOrWhiteSpace(message))
            throw new ArgumentException(
                "Nội dung SMS không được để trống.",
                nameof(message));

        var apiKey = _configuration["SmsGateway:ApiKey"];

        if (string.IsNullOrWhiteSpace(apiKey))
        {
            throw new InvalidOperationException(
                "SmsGateway:ApiKey chưa được cấu hình.");
        }

        using var request = new HttpRequestMessage(
            HttpMethod.Post,
            "api/sms/enqueue");

        request.Headers.TryAddWithoutValidation(
            "X-MAIN-BACKEND-KEY",
            apiKey);

        request.Content = JsonContent.Create(new
        {
            phoneNumber = phoneNumber.Trim(),
            message,
            source
        });

        _logger.LogInformation(
            "Đang enqueue SMS cho {PhoneNumber}, source={Source}",
            phoneNumber,
            source);

        using var response = await _http.SendAsync(
            request,
            cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            var body = await response.Content.ReadAsStringAsync(
                cancellationToken);

            _logger.LogError(
                "SMS Gateway lỗi HTTP {StatusCode}. Body={Body}",
                (int)response.StatusCode,
                body);

            throw new HttpRequestException(
                $"SMS Gateway trả về HTTP {(int)response.StatusCode}: {body}");
        }

        _logger.LogInformation(
            "Đã enqueue SMS thành công cho {PhoneNumber}",
            phoneNumber);
    }
}