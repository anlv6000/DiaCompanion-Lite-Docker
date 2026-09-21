using DiaCompanion.Api.Common;

namespace DiaCompanion.Api.Services;

/// <summary>
/// NF-05 / NF-06 — Gap 2, đóng góp chính của đề tài.
///
/// Ý tưởng: hai nhánh đọc cùng một ảnh cho hai mục đích khác nhau. Khi nhánh
/// phân loại nói "Moderate" mà phân bố tổn thương lại tương ứng "Severe", hai
/// cách đọc cùng một võng mạc mâu thuẫn nhau. Mâu thuẫn đó là thông tin mà
/// không đầu ra nào tự nó mang được: nó cho biết ảnh khó thật, hoặc mô hình
/// đang chạy ngoài vùng năng lực.
///
/// THIẾT KẾ (lưới an toàn referable):
///   Kiểm định trên IDRiD cho thấy dùng bất đồng chéo làm cổng defer BỎ SÓT
///   ~60% ca cần chuyển tuyến — không an toàn cho sàng lọc. Vì vậy quyết định
///   defer nay theo nguyên tắc referable: chỉ TỰ THÔNG QUA khi CẢ HAI nhánh
///   (phân loại DR và grade suy từ tổn thương) đều dưới ngưỡng referable; bất
///   kỳ nhánh nào chạm referable, hoặc thiếu một nhánh, đều chuyển bác sĩ.
///   Bất đồng chéo VẪN được tính và lưu để minh bạch/nghiên cứu nhưng KHÔNG
///   còn là cổng quyết định. Điểm nguy cơ nền KHÔNG gate defer nữa mà dùng để
///   XẾP ƯU TIÊN hàng đợi triage. Điểm nguy cơ KHÔNG đụng DR grade và không
///   thay Doctor confirmation.
/// </summary>
public interface IDeferralService
{
    DeferralResult Evaluate(DrGrade gradeHead, DrGrade? lesionImplied,
                            decimal baseDisagreementThreshold, DrGrade referableGrade);
}

public record DeferralResult(
    decimal? Disagreement,
    bool IsDeferred,
    DeferReason? Reason,
    decimal EffectiveThreshold);

public class DeferralService : IDeferralService
{
    /// <summary>Thang DR có 5 mức nên chênh lệch tối đa là 4 bậc.</summary>
    private const decimal MaxGradeDistance = 4m;

    public DeferralResult Evaluate(
        DrGrade gradeHead,
        DrGrade? lesionImplied,
        decimal baseDisagreementThreshold,
        DrGrade referableGrade)
    {
        // Bất đồng chéo vẫn được tính để LƯU (minh bạch/nghiên cứu) nhưng KHÔNG
        // còn là cổng quyết định defer.
        decimal? disagreement = null;
        if (lesionImplied is not null)
        {
            var distance = Math.Abs((int)gradeHead - (int)lesionImplied.Value);
            disagreement = Math.Round(distance / MaxGradeDistance, 4);
        }

        // Thiếu một nhánh -> không đủ hai ý kiến để tự thông qua an toàn ->
        // chuyển bác sĩ. An toàn nghiêng về phía con người.
        if (lesionImplied is null)
            return new DeferralResult(disagreement, true, DeferReason.MissingBranch,
                                      baseDisagreementThreshold);

        // LƯỚI AN TOÀN referable: chỉ tự thông qua khi CẢ HAI nhánh đều dưới
        // ngưỡng referable. Bất kỳ nhánh nào chạm referable -> chuyển bác sĩ.
        // Sàng lọc thà xem thừa còn hơn bỏ sót ca cần chuyển tuyến.
        var referable = (int)referableGrade;
        var potentiallyReferable =
            (int)gradeHead >= referable || (int)lesionImplied.Value >= referable;

        var reason = potentiallyReferable ? DeferReason.Referable : (DeferReason?)null;
        return new DeferralResult(disagreement, potentiallyReferable, reason,
                                  baseDisagreementThreshold);
    }
}
