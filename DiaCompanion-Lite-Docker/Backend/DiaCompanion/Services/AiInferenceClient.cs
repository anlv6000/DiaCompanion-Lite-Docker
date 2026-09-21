using System.Text.Json.Serialization;
using DiaCompanion.Api.Common;

namespace DiaCompanion.Api.Services;

/// <summary>
/// Cầu nối tới dịch vụ suy luận Python. Một lần chạy dùng ba model độc lập:
///   POST /infer/dr      + đường dẫn DR model
///   POST /infer/lesion  + đường dẫn Lesion model
///   POST /infer/fractal + đường dẫn Fractal model
/// </summary>
public interface IAiInferenceClient
{
    Task<AiInferenceResponse> RunAsync(
        string imageRelativePath,
        string drModelPath,
        string lesionModelPath,
        string fractalModelPath,
        string? eye,
        CancellationToken ct = default);
}

public class AiInferenceResponse
{
    [JsonPropertyName("dr_grade")]          public byte DrGrade { get; set; }
    [JsonPropertyName("confidence")]        public decimal Confidence { get; set; }
    [JsonPropertyName("probabilities")]     public decimal[]? Probabilities { get; set; }
    [JsonPropertyName("lesion_grade")]      public byte? LesionGradeImplied { get; set; }
    [JsonPropertyName("lesion_mask_path")]  public string? LesionMaskPath { get; set; }
    [JsonPropertyName("count_ma")]          public int? CountMA { get; set; }
    [JsonPropertyName("count_he")]          public int? CountHE { get; set; }
    [JsonPropertyName("count_ex")]          public int? CountEX { get; set; }
    [JsonPropertyName("count_se")]          public int? CountSE { get; set; }
    [JsonPropertyName("area_ma")]           public decimal? AreaMA { get; set; }
    [JsonPropertyName("area_he")]           public decimal? AreaHE { get; set; }
    [JsonPropertyName("area_ex")]           public decimal? AreaEX { get; set; }
    [JsonPropertyName("area_se")]           public decimal? AreaSE { get; set; }
    [JsonPropertyName("fractal_dimension")] public decimal? FractalDimension { get; set; }
    [JsonPropertyName("fractal_st")] public decimal? FractalSt { get; set; }
    [JsonPropertyName("fractal_sn")] public decimal? FractalSn { get; set; }
    [JsonPropertyName("fractal_it")] public decimal? FractalIt { get; set; }
    [JsonPropertyName("fractal_in")] public decimal? FractalIn { get; set; }
    [JsonPropertyName("fractal_asymmetry")] public decimal? FractalAsymmetry { get; set; }
    [JsonPropertyName("fractal_tn")] public decimal? FractalTn { get; set; }
    [JsonPropertyName("lacunarity")] public decimal? Lacunarity { get; set; }
    [JsonPropertyName("vessel_mask_path")]  public string? VesselMaskPath { get; set; }
    [JsonPropertyName("fractal_note")]       public string? FractalNote { get; set; }
    [JsonPropertyName("inference_ms")]       public int? InferenceMs { get; set; }
}

file class DrResult
{
    [JsonPropertyName("dr_grade")]      public byte DrGrade { get; set; }
    [JsonPropertyName("confidence")]    public decimal Confidence { get; set; }
    [JsonPropertyName("probabilities")] public decimal[]? Probabilities { get; set; }
    [JsonPropertyName("inference_ms")]  public int? InferenceMs { get; set; }
}

file class LesionResult
{
    [JsonPropertyName("lesion_grade")]     public byte? LesionGrade { get; set; }
    [JsonPropertyName("count_ma")]         public int? CountMA { get; set; }
    [JsonPropertyName("count_he")]         public int? CountHE { get; set; }
    [JsonPropertyName("count_ex")]         public int? CountEX { get; set; }
    [JsonPropertyName("count_se")]         public int? CountSE { get; set; }
    [JsonPropertyName("area_ma")]          public decimal? AreaMA { get; set; }
    [JsonPropertyName("area_he")]          public decimal? AreaHE { get; set; }
    [JsonPropertyName("area_ex")]          public decimal? AreaEX { get; set; }
    [JsonPropertyName("area_se")]          public decimal? AreaSE { get; set; }
    [JsonPropertyName("lesion_mask_path")] public string? LesionMaskPath { get; set; }
    [JsonPropertyName("inference_ms")]     public int? InferenceMs { get; set; }
}

file class FractalResult
{
    [JsonPropertyName("fractal_dimension")] public decimal? FractalDimension { get; set; }
    [JsonPropertyName("fractal_st")] public decimal? FractalSt { get; set; }
    [JsonPropertyName("fractal_sn")] public decimal? FractalSn { get; set; }
    [JsonPropertyName("fractal_it")] public decimal? FractalIt { get; set; }
    [JsonPropertyName("fractal_in")] public decimal? FractalIn { get; set; }
    [JsonPropertyName("fractal_asymmetry")] public decimal? FractalAsymmetry { get; set; }
    [JsonPropertyName("fractal_tn")] public decimal? FractalTn { get; set; }
    [JsonPropertyName("lacunarity")] public decimal? Lacunarity { get; set; }
    [JsonPropertyName("fractal_note")]      public string? FractalNote { get; set; }
    [JsonPropertyName("vessel_mask_path")]  public string? VesselMaskPath { get; set; }
    [JsonPropertyName("inference_ms")]      public int? InferenceMs { get; set; }
}

public class AiInferenceClient : IAiInferenceClient
{
    private readonly HttpClient _http;
    private readonly ILogger<AiInferenceClient> _log;

    public AiInferenceClient(HttpClient http, ILogger<AiInferenceClient> log)
    {
        _http = http;
        _log = log;
    }

    public async Task<AiInferenceResponse> RunAsync(
        string imageRelativePath,
        string drModelPath,
        string lesionModelPath,
        string fractalModelPath,
        string? eye,
        CancellationToken ct = default)
    {
        try
        {
            var drTask = PostAsync<DrResult>("/infer/dr", new
            {
                image_path = imageRelativePath,
                model_path = drModelPath
            }, ct);

            var lesionTask = PostAsync<LesionResult>("/infer/lesion", new
            {
                image_path = imageRelativePath,
                model_path = lesionModelPath
            }, ct);

            var fractalTask = PostAsync<FractalResult>("/infer/fractal", new
            {
                image_path = imageRelativePath,
                model_path = fractalModelPath,
                eye = eye
            }, ct);

            await Task.WhenAll(drTask, lesionTask, fractalTask);

            var dr = await drTask;
            var lesion = await lesionTask;
            var fractal = await fractalTask;
            var totalMs = (dr.InferenceMs ?? 0) + (lesion.InferenceMs ?? 0) + (fractal.InferenceMs ?? 0);

            return new AiInferenceResponse
            {
                DrGrade = dr.DrGrade,
                Confidence = dr.Confidence,
                Probabilities = dr.Probabilities,

                LesionGradeImplied = lesion.LesionGrade,
                LesionMaskPath = lesion.LesionMaskPath,
                CountMA = lesion.CountMA,
                CountHE = lesion.CountHE,
                CountEX = lesion.CountEX,
                CountSE = lesion.CountSE,
                AreaMA = lesion.AreaMA,
                AreaHE = lesion.AreaHE,
                AreaEX = lesion.AreaEX,
                AreaSE = lesion.AreaSE,

                FractalDimension = fractal.FractalDimension,
                FractalSt = fractal.FractalSt,
                FractalSn = fractal.FractalSn,
                FractalIt = fractal.FractalIt,
                FractalIn = fractal.FractalIn,
                FractalAsymmetry = fractal.FractalAsymmetry,
                FractalTn = fractal.FractalTn,
                Lacunarity = fractal.Lacunarity,
                VesselMaskPath = fractal.VesselMaskPath,
                FractalNote = fractal.FractalNote,
                InferenceMs = totalMs > 0 ? totalMs : null
            };
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException)
        {
            _log.LogError(ex, "Không gọi được dịch vụ suy luận AI cho ảnh {Path}", imageRelativePath);
            throw AppException.BadRequest(Msg.AiUnavailable,
                "Không kết nối được dịch vụ suy luận AI. Vui lòng thử lại.");
        }
    }

    private async Task<T> PostAsync<T>(string route, object payload, CancellationToken ct)
    {
        using var resp = await _http.PostAsJsonAsync(route, payload, ct);
        resp.EnsureSuccessStatusCode();
        return await resp.Content.ReadFromJsonAsync<T>(cancellationToken: ct)
            ?? throw AppException.BadRequest(Msg.AiUnavailable,
                $"Dịch vụ suy luận trả về dữ liệu rỗng ở {route}.");
    }
}
