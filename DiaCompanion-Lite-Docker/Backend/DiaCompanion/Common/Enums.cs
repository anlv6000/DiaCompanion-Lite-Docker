namespace DiaCompanion.Api.Common;
/// <summary>
/// QT-8: mức DR là thang THỨ BẬC nên lưu dạng số, không phải chuỗi.
/// Gap 2 phải tính |DrGrade - LesionGradeImplied|; với chuỗi thì mọi chỗ
/// so sánh đều phải ánh xạ lại.
/// </summary>
public enum DrGrade : byte { Normal = 0, Mild = 1, Moderate = 2, Severe = 3, Pdr = 4 }
public enum Eye : byte { Od = 0, Os = 1 }
public enum QualityStatus : byte { Pending = 0, Gradable = 1, Ungradable = 2 }
public enum VisitStatus : byte { InProgress = 0, Completed = 1 }
public enum ReferralType : byte { None = 0, FollowUp = 1, Ophthalmology = 2, Urgent = 3 }
public enum ReviewAction : byte { Approve = 0, Override = 1 }

/// <summary>Loại mô hình AI. Mỗi loại có tối đa một phiên bản đang kích hoạt.</summary>
public enum ModelType : byte
{
    Dr = 1,
    Lesion = 2,
    Fractal = 3
}

/// <summary>Lý do một ca được chuyển cho bác sĩ (Gap 2).</summary>
public enum DeferReason : byte
{
    LowConfidence = 1,
    HighDisagreement = 2,
    Both = 3,
    MissingBranch = 4,
    Referable = 5
}

public enum MedicationStatus : byte { Pending = 0, Taken = 1, Missed = 2, Skipped = 3, Cancelled = 4 }
public enum MetricType : byte { Glucose = 1, HbA1c = 2, SystolicBp = 3, DiastolicBp = 4 }
public enum MetricContext : byte { BeforeMeal = 1, AfterMeal = 2 }
public enum SymptomSeverity : byte { Mild = 1, Moderate = 2, Severe = 3 }

public enum NotificationType : byte { Recheck = 1, Medication = 2, Result = 3, Metric = 4, Blog = 5, Visit = 6 }

public enum BlogCategory : byte { Knowledge = 1, Nutrition = 2, Warning = 3 }
public enum OtpPurpose : byte { Login = 1, ResetPassword = 2, ChangePhone = 3 }

public static class AuditAction
{
    public const string Login = "LOGIN";
    public const string LoginFailed = "LOGIN_FAILED";
    public const string Logout = "LOGOUT";
    public const string PasswordChange = "PASSWORD_CHANGE";
    public const string PasswordReset = "PASSWORD_RESET";
    public const string OtpIssued = "OTP_ISSUED";
    public const string UserCreate = "USER_CREATE";
    public const string UserUpdate = "USER_UPDATE";
    public const string UserLock = "USER_LOCK";
    public const string PatientCreate = "PATIENT_CREATE";
    public const string PatientUpdate = "PATIENT_UPDATE";
    public const string PatientPhoneChange = "PATIENT_PHONE_CHANGE";
    public const string ImageUpload = "IMAGE_UPLOAD";
    public const string QualityCheck = "QUALITY_CHECK";
    public const string AiRun = "AI_RUN";
    public const string ReviewApprove = "REVIEW_APPROVE";
    public const string ReviewOverride = "REVIEW_OVERRIDE";
    public const string VisitClose = "VISIT_CLOSE";
    public const string PrescriptionIssue = "PRESCRIPTION_ISSUE";
    public const string PrescriptionUpdate = "PRESCRIPTION_UPDATE";
    public const string MedicationConfirm = "MEDICATION_CONFIRM";
    public const string MetricCreate = "METRIC_CREATE";
    public const string MetricUpdate = "METRIC_UPDATE";
    public const string MetricDelete = "METRIC_DELETE";
    public const string LifestyleCreate = "LIFESTYLE_CREATE";
    public const string LifestyleUpdate = "LIFESTYLE_UPDATE";
    public const string LifestyleDelete = "LIFESTYLE_DELETE";
    public const string SymptomReport = "SYMPTOM_REPORT";
    public const string SymptomReply = "SYMPTOM_REPLY";
    public const string FeedbackCreate = "FEEDBACK_CREATE";
    public const string BlogCreate = "BLOG_CREATE";
    public const string BlogUpdate = "BLOG_UPDATE";
    public const string BlogState = "BLOG_STATE";
    public const string Void = "VOID";
    public const string ConfigChange = "CONFIG_CHANGE";
    public const string ModelRegister = "MODEL_REGISTER";
    public const string ModelActivate = "MODEL_ACTIVATE";
    public const string ModelDelete = "MODEL_DELETE";
    public const string Export = "EXPORT";
}

public static class ConfigKeys
{
    public const string DisagreementThreshold = "ai.disagreement_threshold";
    public const string ReferableGrade = "screening.referable_grade";
    /// <summary>Giờ làm việc — dùng cho thông báo, không còn để đặt lịch theo khung giờ.</summary>
    public const string OpenHour = "clinic.open_hour";
    public const string CloseHour = "clinic.close_hour";
    public const string OtpTtlSeconds = "otp.ttl_seconds";
    public const string OtpMaxAttempts = "otp.max_attempts";
    public const string ShiftMorningStart = "clinic.shift_morning_start";
    public const string ShiftAfternoonStart = "clinic.shift_afternoon_start";
    public const string ShiftNightStart = "clinic.shift_night_start";

    public static string RecheckMonths(byte grade) => $"recheck.months_grade_{grade}";
}
