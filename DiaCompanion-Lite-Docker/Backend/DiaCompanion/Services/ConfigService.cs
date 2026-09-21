using DiaCompanion.Api.Common;
using DiaCompanion.Api.Repositories;

namespace DiaCompanion.Api.Services;

public interface IConfigService
{
    Task<string?> GetAsync(string key);
    Task<decimal> GetDecimalAsync(string key, decimal fallback);
    Task<int> GetIntAsync(string key, int fallback);
    Task<byte> GetRecheckMonthsAsync(DrGrade grade);
    Task<TimeOnly> GetTimeAsync(string key, TimeOnly fallback);
}

public class ConfigService : IConfigService
{
    private readonly IRepository _repository;
    public ConfigService(IRepository repository) => _repository = repository;

    public Task<string?> GetAsync(string key) =>
        _repository.GetSystemConfigValueAsync(key);

    public async Task<decimal> GetDecimalAsync(string key, decimal fallback)
    {
        var v = await GetAsync(key);
        return decimal.TryParse(v, System.Globalization.NumberStyles.Any,
            System.Globalization.CultureInfo.InvariantCulture, out var d) ? d : fallback;
    }

    public async Task<int> GetIntAsync(string key, int fallback)
    {
        var v = await GetAsync(key);
        return int.TryParse(v, out var i) ? i : fallback;
    }

    /// <summary>
    /// BR-19: chu kỳ tái tầm soát theo mức DR đã xác nhận.
    /// Giá trị mặc định lấy theo hướng dẫn tầm soát thông dụng; cơ sở triển khai
    /// cần đối chiếu quy định hiện hành của Bộ Y tế trước khi dùng thật.
    /// </summary>
    public async Task<byte> GetRecheckMonthsAsync(DrGrade grade)
    {
        var fallback = grade switch
        {
            DrGrade.Normal or DrGrade.Mild => 12,
            DrGrade.Moderate => 6,
            DrGrade.Severe => 3,
            DrGrade.Pdr => 1,
            _ => 12
        };
        return (byte)await GetIntAsync(ConfigKeys.RecheckMonths((byte)grade), fallback);
    }
    public async Task<TimeOnly> GetTimeAsync(
    string key,
    TimeOnly fallback)
    {
        var value = await GetAsync(key);

        var isValid = TimeOnly.TryParseExact(
            value,
            "HH:mm",
            System.Globalization.CultureInfo.InvariantCulture,
            System.Globalization.DateTimeStyles.None,
            out var parsedTime);

        var result = isValid
            ? parsedTime
            : fallback;

        return result;
    }
}
