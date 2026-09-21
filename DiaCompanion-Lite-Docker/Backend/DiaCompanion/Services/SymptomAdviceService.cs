using DiaCompanion.Api.Common;

namespace DiaCompanion.Api.Services;

/// <summary>
/// UC-50 / BR-20. Khuyến cáo do HỆ THỐNG sinh, theo quy tắc cứng dựa trên mức
/// độ bệnh nhân tự chọn — KHÔNG phải AI, KHÔNG phải chẩn đoán.
///
/// Ghi vào cột AutoAdvice (bất biến). Trả lời của bác sĩ ghi vào DoctorReply.
/// Tách hai cột vì nếu dùng một cột, trả lời của bác sĩ sẽ ghi đè khuyến cáo
/// tự động và mất vết nguồn gốc.
/// </summary>
public interface ISymptomAdviceService
{
    string Generate(SymptomSeverity severity);
}

public class SymptomAdviceService : ISymptomAdviceService
{
    private const string SafetyNote =
        " Đây không phải kênh cấp cứu. Bác sĩ sẽ xem trong giờ làm việc; " +
        "nếu khẩn cấp hãy gọi 115 hoặc đến cơ sở y tế gần nhất.";

    public string Generate(SymptomSeverity severity) => severity switch
    {
        SymptomSeverity.Severe =>
            "Triệu chứng bạn mô tả có thể nghiêm trọng. Hãy đến cơ sở y tế ngay " +
            "hoặc liên hệ bác sĩ phụ trách." + SafetyNote,

        SymptomSeverity.Moderate =>
            "Triệu chứng mức vừa. Bạn nên đặt lịch khám trong vài ngày tới và " +
            "theo dõi xem có nặng thêm không. Ứng dụng không đưa ra chẩn đoán." + SafetyNote,

        _ =>
            "Triệu chứng mức nhẹ. Hãy theo dõi thêm và ghi nhận nếu tăng lên. " +
            "Thông tin đã được chuyển tới bác sĩ phụ trách." + SafetyNote
    };
}
