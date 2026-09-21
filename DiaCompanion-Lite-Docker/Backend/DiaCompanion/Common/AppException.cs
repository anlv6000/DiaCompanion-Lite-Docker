namespace DiaCompanion.Api.Common;

/// <summary>
/// Lỗi nghiệp vụ có mã thông điệp khớp bảng MSG-xx trong Report 3 phần 5.2.
/// Giao diện tra mã này để hiển thị đúng câu tiếng Việt đã đặc tả.
/// </summary>
public class AppException : Exception
{
    public int StatusCode { get; }
    public string MessageCode { get; }

    public AppException(string messageCode, string message, int statusCode = 400) : base(message)
    {
        MessageCode = messageCode;
        StatusCode = statusCode;
    }

    public static AppException BadRequest(string code, string msg) => new(code, msg, 400);
    public static AppException Unauthorized(string code, string msg) => new(code, msg, 401);
    public static AppException Forbidden(string code, string msg) => new(code, msg, 403);
    public static AppException NotFound(string code, string msg) => new(code, msg, 404);
    public static AppException Conflict(string code, string msg) => new(code, msg, 409);
}

public static class Msg
{
    public const string BadCredentials   = "MSG-01";
    public const string AccountLocked    = "MSG-02";
    public const string SessionExpired   = "MSG-03";
    public const string Forbidden        = "MSG-04";
    public const string PhoneTaken       = "MSG-05";
    public const string OtpInvalid       = "MSG-06";
    public const string WeakPassword     = "MSG-07";
    public const string PatientNotFound  = "MSG-08";
    public const string AlreadyLinked    = "MSG-09";
    public const string RequiredFields   = "MSG-10";
    public const string LicenseRequired  = "MSG-11";
    public const string FileTooLarge     = "MSG-12";
    public const string BadFileType      = "MSG-13";
    public const string ImageNotGradable = "MSG-14";
    public const string AiUnavailable    = "MSG-15";
    public const string OverrideReason   = "MSG-16";
    public const string VoidReason       = "MSG-17";
    public const string ConclusionNeeded = "MSG-18";
    public const string EmptyPrescription= "MSG-19";
    public const string SlotTaken        = "MSG-20";
    public const string ApptImmutable    = "MSG-21";
    public const string ThresholdRange   = "MSG-22";
    public const string ModelWasActive   = "MSG-23";
    public const string LoadFailed       = "MSG-24";
    public const string InvalidData      = "MSG-25";
    public const string ConcurrentEdit   = "MSG-43";
    public const string StaleVersion     = "MSG-43";

}
