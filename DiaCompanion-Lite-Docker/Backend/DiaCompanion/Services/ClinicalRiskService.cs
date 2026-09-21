using DiaCompanion.Api.Common;
using DiaCompanion.Api.Repositories;

namespace DiaCompanion.Api.Services;

/// <summary>
/// Điểm nguy cơ nền theo loại đái tháo đường (Gap 2 — lớp clinical-risk).
///
/// PHẠM VI: điểm này CHỈ dùng để hạ ngưỡng bất đồng và sắp thứ tự hàng đợi.
/// Nó KHÔNG đổi DR grade, KHÔNG tạo chẩn đoán, KHÔNG dự đoán tiến triển.
/// Mọi ca vẫn qua Doctor confirmation (BR-02). Vì phạm vi hẹp như vậy nên các
/// trọng số guideline-anchored (chưa hiệu chỉnh trên dữ liệu) là an toàn.
///
/// Cơ sở y văn: ADA Standards of Care (R1), DCCT (R3), UKPDS 35/38 (R5/R6);
/// việc dùng chính các yếu tố này để ưu tiên sàng lọc đã validate ở
/// RetinaRisk/Aspelund (R8) và ISDR/Liverpool (R9). Thang điểm nguyên theo
/// phương pháp Sullivan/Framingham (R16).
///
/// FAIL-SAFE: thiếu dữ liệu ở yếu tố nào thì cộng 0 ở yếu tố đó (không phạt).
/// </summary>
public record ClinicalRiskResult(int Score, IReadOnlyList<string> Factors);

public interface IClinicalRiskService
{
    Task<ClinicalRiskResult> EvaluateAsync(int patientId, CancellationToken ct = default);
}

public class ClinicalRiskService : IClinicalRiskService
{
    /// <summary>Cần đủ 3 lần đo huyết áp mới đủ cơ sở kết luận kiểm soát.</summary>
    private const int SystolicSampleSize = 3;

    private const byte TypeOne = 1;

    private readonly IRepository _repository;
    private readonly IAdherenceService _adherence;
    private readonly IClinicClock _clock;

    public ClinicalRiskService(IRepository repository, IAdherenceService adherence, IClinicClock clock)
    {
        _repository = repository;
        _adherence = adherence;
        _clock = clock;
    }

    public async Task<ClinicalRiskResult> EvaluateAsync(int patientId, CancellationToken ct = default)
    {
        var factors = new List<string>();

        var patient = await _repository.GetPatientForRiskAsync(patientId, ct);
        if (patient is null)
            return new ClinicalRiskResult(0, factors);   // không có bệnh nhân -> không dữ liệu -> 0

        var score = 0;

        // ---- HbA1c: chung cho cả hai loại, quan hệ liên tục không ngưỡng (R3, R5) ----
        var hba1c = await _repository.GetLatestHba1cAsync(patientId, ct) ?? patient.BaselineHbA1c;
        if (hba1c is decimal h)
        {
            if (h >= 9.0m) { score += 2; factors.Add($"HbA1c {h:0.#}% \u2265 9,0 (+2)"); }
            else if (h >= 8.0m) { score += 1; factors.Add($"HbA1c {h:0.#}% \u2265 8,0 (+1)"); }
            // < 8,0 -> +0
        }

        var years = patient.DiabetesDurationYears;

        if (patient.DiabetesType == TypeOne)
        {
            // ---- Type 1: SÀN 5 NĂM (R1) — dưới 5 năm chưa tới lịch sàng lọc đầu ----
            if (years is short y1)
            {
                if (y1 >= 15) { score += 2; factors.Add($"Type 1, m\u1eafc {y1} n\u0103m \u2265 15 (+2)"); }
                else if (y1 >= 5) { score += 1; factors.Add($"Type 1, m\u1eafc {y1} n\u0103m 5\u201314 (+1)"); }
                // < 5 năm -> +0 (dưới sàn)
            }
        }
        else
        {
            // ---- Type 2 (và mặc định): KHÔNG sàn, có kênh huyết áp ----
            // Trọng số thời gian thấp hơn type 1 vì đo từ ngày CHẨN ĐOÁN (R1).
            if (years is short y2 && y2 >= 10)
            {
                score += 1;
                factors.Add($"Type 2, m\u1eafc {y2} n\u0103m \u2265 10 (+1)");
            }

            // Huyết áp CHỈ tính cho type 2: bằng chứng RCT (UKPDS 38, R6) ở quần thể type 2.
            var sbp = await _repository.GetRecentSystolicAverageAsync(patientId, SystolicSampleSize, ct);
            if (sbp is decimal s && s >= 140m)
            {
                score += 1;
                factors.Add($"HA t\u00e2m thu TB {s:0} \u2265 140 (+1)");
            }
        }

        // ---- Yếu tố vận hành (chung) — KHÔNG phải nguy cơ dịch tễ ----
        // Tuân thủ 30 ngày < 60%. Chỉ tính khi có liều đến hạn (thiếu dữ liệu -> không phạt).
        var adh = await _adherence.GetAsync(patientId, 30);
        var dueDoses = adh.Taken + adh.Missed + adh.Skipped;
        if (dueDoses > 0 && adh.Rate < 60m)
        {
            score += 1;
            factors.Add($"Tu\u00e2n th\u1ee7 30 ng\u00e0y {adh.Rate:0}% < 60 (+1)");
        }

        // Quá hạn tái khám. Dùng lại candidate của RecheckService để công thức không lệch.
        var candidate = await _repository.GetRecheckCandidateAsync(patientId, ct);
        if (candidate is not null)
        {
            var closedLocal = _clock.ToLocal(candidate.ClosedAt)!.Value;
            var dueDate = DateOnly.FromDateTime(closedLocal.AddMonths(candidate.RecheckMonths));
            if (_clock.LocalToday > dueDate)
            {
                score += 1;
                factors.Add("Qu\u00e1 h\u1ea1n t\u00e1i kh\u00e1m (+1)");
            }
        }

        return new ClinicalRiskResult(score, factors);
    }
}
