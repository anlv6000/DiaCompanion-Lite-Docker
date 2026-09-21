using DiaCompanion.Api.Common;
using DiaCompanion.Api.Repositories;
using DiaCompanion.Api.Entities;

namespace DiaCompanion.Api.Services;

/// <summary>
/// Sinh OTP, lưu hash, xác minh OTP và chuyển nội dung OTP sang SMS Gateway.
///
/// DiaCompanion Backend chịu trách nhiệm:
/// - sinh OTP;
/// - hash OTP;
/// - thời hạn OTP;
/// - giới hạn số lần thử;
/// - vô hiệu OTP cũ;
/// - xác minh OTP.
///
/// SMS Gateway chỉ chịu trách nhiệm vận chuyển SMS.
/// </summary>
public interface IOtpService
{
    Task<string> IssueAsync(
        string phone,
        OtpPurpose purpose,
        int? issuedBy);

    Task<bool> VerifyAsync(
        string phone,
        string code,
        OtpPurpose purpose);
}

public class OtpService : IOtpService
{
    private readonly IRepository _repository;
    private readonly IPasswordHasher _hasher;
    private readonly IConfigService _cfg;
    private readonly ISmsSender _smsSender;
    private readonly ILogger<OtpService> _logger;

    public OtpService(
        IRepository repository,
        IPasswordHasher hasher,
        IConfigService cfg,
        ISmsSender smsSender,
        ILogger<OtpService> logger)
    {
        _repository = repository;
        _hasher = hasher;
        _cfg = cfg;
        _smsSender = smsSender;
        _logger = logger;
    }

    public async Task<string> IssueAsync(
        string phone,
        OtpPurpose purpose,
        int? issuedBy)
    {
        if (string.IsNullOrWhiteSpace(phone))
            throw new ArgumentException(
                "Số điện thoại không được để trống.",
                nameof(phone));

        phone = phone.Trim();

        var ttl = await _cfg.GetIntAsync(
            ConfigKeys.OtpTtlSeconds,
            300);

        // Vô hiệu hóa các OTP cũ chưa sử dụng.
        // Chỉ một OTP mới nhất được phép còn hiệu lực.
        var oldCodes =
            await _repository.GetUnconsumedOtpCodesAsync(
                phone,
                purpose);

        foreach (var oldCode in oldCodes)
        {
            oldCode.ConsumedAt = DateTime.UtcNow;
        }

        // Sinh mã 6 chữ số bằng RNG bảo mật.
        var code =
            System.Security.Cryptography.RandomNumberGenerator
                .GetInt32(100_000, 1_000_000)
                .ToString();

        var otp = new OtpCode
        {
            Phone = phone,
            CodeHash = _hasher.Hash(code),
            Purpose = purpose,
            ExpiresAt = DateTime.UtcNow.AddSeconds(ttl),
            IssuedBy = issuedBy
        };

        _repository.Add(otp);

        // Commit OTP trước khi gửi SMS.
        // Nếu transport SMS lỗi thì OTP record vẫn nhất quán trong DB.
        await _repository.CommitAsync();

        var ttlMinutes = Math.Max(
            1,
            (int)Math.Ceiling(ttl / 60d));

        var message =
            $"DiaCompanion: Ma OTP cua ban la {code}. " +
            $"Ma co hieu luc trong {ttlMinutes} phut.";

        var source =
            $"otp-{purpose.ToString().ToLowerInvariant()}";

        try
        {
            await _smsSender.SendAsync(
                phone,
                message,
                source);

            _logger.LogInformation(
                "OTP SMS đã được enqueue cho {Phone}, Purpose={Purpose}",
                phone,
                purpose);
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "Không enqueue được OTP SMS cho {Phone}, Purpose={Purpose}",
                phone,
                purpose);

            // Hiện tại giữ behavior throw để caller biết việc gửi SMS thất bại.
            // OTP đã được lưu trước đó nên dữ liệu không bị partial transaction.
            throw;
        }

        return code;
    }

    public async Task<bool> VerifyAsync(
        string phone,
        string code,
        OtpPurpose purpose)
    {
        if (string.IsNullOrWhiteSpace(phone)
            || string.IsNullOrWhiteSpace(code))
        {
            return false;
        }

        phone = phone.Trim();
        code = code.Trim();

        var maxAttempts = await _cfg.GetIntAsync(
            ConfigKeys.OtpMaxAttempts,
            5);

        var otp =
            await _repository.GetLatestUnconsumedOtpAsync(
                phone,
                purpose);

        if (otp is null)
            return false;

        if (otp.ExpiresAt < DateTime.UtcNow)
        {
            otp.ConsumedAt = DateTime.UtcNow;

            await _repository.CommitAsync();

            return false;
        }

        if (otp.AttemptCount >= maxAttempts)
        {
            otp.ConsumedAt = DateTime.UtcNow;

            await _repository.CommitAsync();

            return false;
        }

        otp.AttemptCount++;

        var isValid =
            _hasher.Verify(
                code,
                otp.CodeHash);

        if (isValid)
        {
            // OTP single-use.
            otp.ConsumedAt = DateTime.UtcNow;
        }

        await _repository.CommitAsync();

        return isValid;
    }
}