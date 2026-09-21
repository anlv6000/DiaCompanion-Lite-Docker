export type Role = "Admin" | "Doctor" | "Patient" | "Receptionist" | "Research";
export type StaffRole = Exclude<Role, "Patient">;
export type DoctorOrReceptionist = "Doctor" | "Receptionist";

export type Nullable<T> = T | null | undefined;
export interface PagedResult<T> {
  items: T[];
  page: number;
  pageSize: number;
  totalItems: number;
  totalPages: number;
  rangeLabel: string;
}
export interface KeysetResult<T> {
  items: T[];
  nextCursor?: string | null;
  hasMore: boolean;
}
export interface ApiMessage {
  message: string;
  messageCode?: string;
  detail?: string;
  traceId?: string;
}
export interface ConcurrencyRequest {
  rowVersion: string;
  pairRowVersion?: string;
}
export interface VoidRequest {
  reason: string;
  rowVersion: string;
}
export interface LoginRequest {
  email?: string;
  phone?: string;
  password: string;
}
export interface LoginResponse {
  token?: string;
  expiresAt?: string;
  refreshToken?: string;
  refreshTokenExpiresAt?: string;
  userId: number;
  fullName: string;
  /** Vai trò ưu tiên để tương thích API cũ. Không dùng trường này làm nguồn quyền duy nhất. */
  role?: Role;
  /** Toàn bộ role active lấy từ Roles + UserRoles. */
  roles: Role[];
  patientId?: number | null;
  mustChangePassword: boolean;
  defaultRoute: string;
}
export interface OtpResponse {
  message: string;
  devCode?: string | null;
  note?: string | null;
}
export interface ResetPasswordRequest {
  phone: string;
  code: string;
  newPassword: string;
}
export interface ChangePasswordRequest {
  /** Bỏ trống khi mustChangePassword=true (lần đăng nhập đầu bằng mật khẩu tạm). */
  currentPassword?: string;
  newPassword: string;
}
export interface ChangePasswordResponse extends LoginResponse {
  message: string;
}
export interface StaffProfileDto {
  id: number;
  fullName: string;
  email?: string | null;
  phone?: string | null;
  role: DoctorOrReceptionist;
  licenseNo?: string | null;
  lastLoginAt?: string | null;
  createdAt: string;
  rowVersion: string;
}
export interface UpdateStaffProfileRequest {
  fullName: string;
  phone: string;
  licenseNo?: string | null;
  rowVersion: string;
}
export interface StaffUserDto {
  id: number;
  fullName: string;
  email?: string | null;
  phone?: string | null;
  /** Vai trò ưu tiên do backend trả để tương thích client cũ. */
  role?: Role;
  /** Danh sách role active thực tế của User. */
  roles: Role[];
  licenseNo?: string | null;
  isActive: boolean;
  lastLoginAt?: string | null;
  createdAt: string;
  rowVersion: string;
}
export interface DoctorDto {
  id: number;
  fullName: string;
  licenseNo?: string | null;
}
export interface LinkablePatientUserDto {
  id: number;
  fullName: string;
  email?: string | null;
  phone?: string | null;
  roles: Role[];
}
export interface TempCredentialResponse {
  loginId: string;
  tempPassword: string;
  note: string;
}
export interface CreateStaffRequest {
  email: string;
  fullName: string;
  phone: string;
  /** Role nhân viên được chọn trên UI. */
  role?: StaffRole;
  /** FE gửi mảng 1 phần tử để tương thích backend dùng UserRoles. */
  roles: StaffRole[];
  licenseNo?: string | null;
}
export interface UpdateStaffRequest {
  fullName: string;
  phone: string;
  licenseNo?: string | null;
  /** FE gửi tối đa 1 role nhân viên; backend giữ nguyên role Patient nếu User đang có. */
  roles?: StaffRole[];
  rowVersion: string;
}
export interface PatientListItemDto {
  id: number;
  code: string;
  fullName: string;
  age: number;
  gender: number;
  phone: string;
  diabetesType: number;
  diabetesDurationYears?: number | null;
  latestDrGrade?: number | null;
  latestVisitDate?: string | null;
  hasAccount: boolean;
}
export interface PatientDetailDto extends PatientListItemDto {
  dateOfBirth: string;
  address?: string | null;
  baselineHbA1c?: number | null;
  note?: string | null;
  createdAt: string;
  doctorInCharge?: string | null;
  visitCount: number;
  rowVersion: string;
}
export interface CreatePatientRequest {
  fullName: string;
  gender: number;
  dateOfBirth: string;
  phone: string;
  address?: string | null;
  diabetesType: number;
  diabetesDurationYears?: number | null;
  baselineHbA1c?: number | null;
  note?: string | null;
  createAccount: boolean;
  existingUserId?: number | null;
}
export interface CreatePatientResponse {
  patient: PatientDetailDto;
  account?: TempCredentialResponse | null;
}
export type UpdatePatientRequest = Omit<
  CreatePatientRequest,
  "createAccount" | "existingUserId"
> & {
  rowVersion: string;
};
export interface VisitDto {
  id: number;
  patientId: number;
  patientName: string;
  patientCode: string;
  doctorId?: number | null;
  doctorName?: string | null;
  visitDate: string;
  status: number;
  conclusion?: string | null;
  referral?: number | null;
  recheckMonths?: number | null;
  closedAt?: string | null;
  imageCount: number;
  pendingReviewCount: number;
  healthMetrics?: VisitHealthMetricsDto | null;
  rowVersion: string;
}
export interface CreateVisitRequest {
  patientId: number;
  doctorId?: number | null;
}
export interface CloseVisitRequest {
  conclusion: string;
  referral: number;
  recheckMonths?: number | null;
  rowVersion: string;
}
export interface RecheckDto {
  patientId: number;
  patientCode: string;
  patientName: string;
  patientPhone?: string | null;
  lastVisitId: number;
  lastVisitClosedAt: string;
  lastConfirmedGrade?: number | null;
  lastConfirmedGradeLabel?: string | null;
  referral?: number | null;
  recheckMonths: number;
  dueDate: string;
  daysPastDue: number;
  isOverdue: boolean;
  statusLabel: string;
}
export interface FundusImageDto {
  id: number;
  patientId: number;
  visitId?: number | null;
  eye: number;
  qualityStatus: number;
  qualityNote?: string | null;
  createdAt: string;
  contentUrl?: string | null;
  latestDiagnosis?: AiDiagnosisDto | null;
  rowVersion: string;
}
export interface AiDiagnosisDto {
  id: number;
  fundusImageId: number;
  visitId?: number | null;
  visitStatus?: number | null;
  eye: number;
  drGrade: number;
  drGradeLabel: string;
  clinicalRiskScore?: number | null;
  effectiveDisagreementThreshold?: number | null;
  clinicalRiskFactors?: string | null;
  lesionGradeImplied?: number | null;
  countMA?: number | null;
  countHE?: number | null;
  countEX?: number | null;
  countSE?: number | null;
  disagreement?: number | null;
  isDeferred: boolean;
  deferReason?: number | null;
  deferReasonLabel?: string | null;
  fractalDimension?: number | null;
  fractalSt?: number | null;
  fractalSn?: number | null;
  fractalIt?: number | null;
  fractalIn?: number | null;
  fractalAsymmetry?: number | null;
  fractalTn?: number | null;
  lacunarity?: number | null;
  fractalNote?: string | null;
  hasLesionMask: boolean;
  hasFractalImage: boolean;
  createdAt: string;
  isConfirmed: boolean;
  review?: ReviewDto | null;
  rowVersion?: string | null;
}
export interface TriageItemDto {
  aiDiagnosisId: number;
  patientId: number;
  patientCode: string;
  patientName: string;
  visitId?: number | null;
  eye: number;
  drGrade: number;
  clinicalRiskScore?: number | null;
  disagreement?: number | null;
  isDeferred: boolean;
  deferReason?: number | null;
  needsReferral: boolean;
  createdAt: string;
  doctorName?: string | null;
  rowVersion?: string | null;
}
export interface TriageCountDto {
  pending: number;
  deferred: number;
}
export interface ReviewDto {
  id: number;
  aiDiagnosisId: number;
  action: number;
  actionLabel: string;
  finalGrade: number;
  finalGradeLabel: string;
  reason?: string | null;
  doctorName: string;
  createdAt: string;
  rowVersion: string;
}
export interface OverrideRequest {
  rowVersion?: string | null;
  finalGrade: number;
  reason: string;
}
export interface DisagreementCaseDto {
  aiDiagnosisId: number;
  patientCode: string;
  eye: number;
  aiGrade: number;
  doctorGrade: number;
  gradeDistance: number;
  disagreement?: number | null;
  wasDeferred: boolean;
  reason?: string | null;
  reviewedAt: string;
}
export interface DisagreementSummaryDto {
  totalReviewed: number;
  totalOverridden: number;
  overrideRate: number;
  deferredCount: number;
  overrideRateWithinDeferred: number;
  overrideRateOutsideDeferred: number;
  avgDisagreement: number;
  interpretation: string;
}
export interface DisagreementExport {
  summary: DisagreementSummaryDto;
  cases: DisagreementCaseDto[];
}
export interface PrescriptionItemDto {
  id?: number;
  drugName: string;
  dose: string;
  timesPerDay: number;
  durationDays: number;
  instruction?: string | null;
  instructions?: string | null;
}
export interface AdherenceDto {
  total: number;
  taken: number;
  missed: number;
  pending: number;
  rate: number;
  days: number;
  note: string;
}
export interface PrescriptionDto {
  id: number;
  patientId: number;
  visitId?: number | null;
  doctorName: string;
  issuedAt: string;
  note?: string | null;
  items: PrescriptionItemDto[];
  rowVersion: string;
}
export interface CreatePrescriptionRequest {
  patientId: number;
  visitId?: number | null;
  note?: string | null;
  items: PrescriptionItemDto[];
}
export interface UpdatePrescriptionRequest extends CreatePrescriptionRequest {
  rowVersion: string;
}
// ===== Lễ tân: ca trực + bác sĩ đang trực =====
export interface DoctorShiftDto {
  id: number;
  doctorId: number;
  doctorName: string;
  licenseNo?: string | null;
  dayOfWeek: number; // 0=CN … 6=T7
  dayLabel: string;
  shift: number; // 1=Sáng, 2=Chiều, 3=Đêm
  shiftLabel: string;
  isActive: boolean;
  rowVersion: string;
}
export interface CreateDoctorShiftRequest {
  doctorId: number;
  dayOfWeek: number;
  shift: number;
}
export interface CreateDoctorShiftsBatchRequest {
  doctorId: number;
  daysOfWeek: number[];
  shift: number;
}
export interface OnDutyDoctorDto {
  doctorId: number;
  doctorName: string;
  licenseNo?: string | null;
  shift: number;
  shiftLabel: string;
  openVisitCount: number;
}
export interface OnDutyResponse {
  date: string;
  dayLabel: string;
  currentShift?: number | null;
  doctors: OnDutyDoctorDto[];
}
export interface HealthMetricDto {
  id: number;
  visitId?: number | null;
  metricType: number;
  value: number;
  unit: string;
  context?: number | null;
  recordedAtUtc: string;
  recordedLocalDate: string;
  note?: string | null;
  isAbnormal: boolean;
  rowVersion: string;
  pairMetricId?: number | null;
  pairRowVersion?: string | null;
  systolicValue?: number | null;
  diastolicValue?: number | null;
}
export interface VisitHealthMetricsDto {
  visitId: number;
  glucose?: HealthMetricDto | null;
  hbA1c?: HealthMetricDto | null;
  bloodPressure?: HealthMetricDto | null;
}
export interface SaveVisitHealthMetricsRequest {
  glucose?: number | null;
  glucoseContext?: number | null;
  glucoseNote?: string | null;
  glucoseRowVersion?: string | null;

  hbA1c?: number | null;
  hbA1cNote?: string | null;
  hbA1cRowVersion?: string | null;

  systolicBp?: number | null;
  diastolicBp?: number | null;
  bloodPressureNote?: string | null;
  systolicRowVersion?: string | null;
  diastolicRowVersion?: string | null;
}
export interface CreateMetricRequest {
  metricType: number;
  value: number;
  context?: number | null;
  recordedAtUtc?: string | null;
  note?: string | null;
}
export interface MetricChartPoint {
  date: string;
  value: number;
  count: number;
  abnormalCount: number;
  isAbnormal: boolean;
}
export interface MetricTrend {
  average?: number | null;
  latest?: {
    value: number;
    unit: string;
    recordedAtUtc: string;
    isAbnormal: boolean;
  } | null;
  abnormalCount: number;
  chart: MetricChartPoint[];
}
export interface BloodPressureTrend {
  averageSystolic?: number | null;
  averageDiastolic?: number | null;
  latest?: {
    systolic: number;
    diastolic: number;
    unit: string;
    recordedAtUtc: string;
    isAbnormal: boolean;
  } | null;
  abnormalCount: number;
  chart: {
    date: string;
    systolic: number;
    diastolic: number;
    isAbnormal: boolean;
  }[];
}
export interface MetricSummary {
  days: number;
  from: string;
  to: string;
  totalAbnormalCount: number;
  glucose: MetricTrend;
  hbA1c: MetricTrend;
  bloodPressure: BloodPressureTrend;
}
export interface LifestyleLogDto {
  id: number;
  logLocalDate: string;
  mealNote?: string | null;
  mealTags?: string | null;
  exerciseMinutes?: number | null;
  exerciseType?: string | null;
}
export interface MedicationLogDto {
  id: number;
  drugName: string;
  dose: string;
  scheduledAt: string;
  takenAt?: string | null;
  status: number;
}
export interface NotificationDto {
  id: number;
  type: number;
  title: string;
  message: string;
  linkEntity?: string | null;
  linkEntityId?: number | null;
  isRead: boolean;
  createdAt: string;
}
export interface SymptomReportDto {
  id: number;
  symptoms: string;
  severity: number;
  description?: string | null;
  onsetNote?: string | null;
  autoAdvice: string;
  doctorReply?: string | null;
  repliedByName?: string | null;
  repliedAt?: string | null;
  createdAt: string;
  state: string;
  patientName: string;
  rowVersion: string;
}
export interface FeedbackDto {
  id: number;
  patientId: number;
  patientCode: string;
  patientName: string;
  visitId?: number | null;
  rating: number;
  tags?: string | null;
  comment?: string | null;
  createdAt: string;
}
export interface FeedbackSummary {
  total: number;
  average: number;
  distribution: Record<string, number>;
}
export interface BlogPostDto {
  id: number;
  title: string;
  summary?: string | null;
  body?: string | null;
  category: number;
  isPublished: boolean;
  publishedAt?: string | null;
  authorName: string;
  createdAt: string;
  rowVersion: string;
}
export interface SaveBlogRequest {
  title: string;
  summary?: string | null;
  body: string;
  category: number;
  rowVersion?: string;
}
export interface DashboardDto {
  periodFrom: string;
  periodTo: string;
  scope: string;
  totalPatients: number;
  visitsThisMonth: number;
  pendingTriage: number;
  deferredPending: number;
  deferralRate: number;
  referralRate: number;
  overrideRate: number;
  gradeDistribution: Record<string, number>;
}
export interface SystemConfigDto {
  key: string;
  value: string;
  valueType: string;
  description?: string | null;
  minValue?: number | null;
  maxValue?: number | null;
  updatedAt?: string | null;
  rowVersion: string;
}
export interface UpdateConfigRequest {
  value: string;
  rowVersion: string;
}
export interface ThresholdImpactDto {
  currentThreshold: number;
  proposedThreshold: number;
  totalCases: number;
  currentDeferred: number;
  projectedDeferred: number;
  currentRate: number;
  projectedRate: number;
  note: string;
}
export interface AuditLogDto {
  id: number;
  userName?: string | null;
  action: string;
  entityType: string;
  entityId?: number | null;
  oldValue?: string | null;
  newValue?: string | null;
  detail?: string | null;
  ipAddress?: string | null;
  createdAt: string;
}
export interface ProgressionPoint {
  date: string;
  visitId?: number | null;
  confirmedGrade?: number | null;
  fractalDimension?: number | null;
  hbA1c?: number | null;
}
export interface ProgressionDto {
  points: ProgressionPoint[];
  trendWarning?: string | null;
}
export interface VisitReportClinic {
  name: string;
  subtitle: string;
}
export interface VisitReportPatient {
  code: string;
  fullName: string;
  dateOfBirth: string;
  gender: number;
  phone: string;
  diabetesType: number;
  diabetesDurationYears?: number | null;
}
export interface VisitReportVisit {
  id: number;
  visitDate: string;
  status: number;
  conclusion?: string | null;
  referral?: number | null;
  recheckMonths?: number | null;
  recheckDueDate?: string | null;
  closedAt?: string | null;
  doctorName?: string | null;
  doctorLicense?: string | null;
}
export interface VisitReportImage {
  imageId: number;
  eye: number;
  qualityStatus: number;
  qualityStatusLabel: string;
  qualityNote?: string | null;
}
export interface VisitReportFinding {
  diagnosisId: number;
  eye: number;
  imageId: number;
  qualityStatus: number;
  qualityStatusLabel: string;
  qualityNote?: string | null;
  finalGrade: number;
  finalGradeLabel: string;
  action: number;
  actionLabel: string;
  reason?: string | null;
  confirmedBy: string;
  createdAt: string;
  urlImageLesionAfterMedical?: string | null;
  urlImageVesselAfterMedical?: string | null;
  urlImgBeforeMEDICAL?: string | null;
  ai: {
    grade: number;
    gradeLabel: string;
    disagreement?: number | null;
    isDeferred: boolean;
    wasOverridden: boolean;
    overrideReason?: string | null;
  };
  lesions: {
    countMA?: number | null;
    countHE?: number | null;
    countEX?: number | null;
    countSE?: number | null;
  };
  fractal?: number | null;
}
export interface VisitReportMetricValue {
  value: number;
  unit: string;
  recordedAt: string;
  isAbnormal: boolean;
}
export interface VisitReportGlucose extends VisitReportMetricValue {
  context?: number | null;
}
export interface VisitReportBloodPressure {
  systolic: number;
  diastolic: number;
  unit: string;
  recordedAt: string;
  isAbnormal: boolean;
}
export interface VisitReportHealthMetrics {
  date: string;
  glucose?: VisitReportGlucose | null;
  hba1c?: VisitReportMetricValue | null;
  bloodPressure?: VisitReportBloodPressure | null;
}
export interface VisitReportPrescription {
  issuedAt: string;
  note?: string | null;
  items: {
    drugName: string;
    dose: string;
    timesPerDay: number;
    durationDays: number;
    instruction?: string | null;
  }[];
}
export interface VisitReportFeedback {
  rating: number;
  tags?: string | null;
  comment?: string | null;
  createdAt: string;
}
export interface VisitReport {
  clinic: VisitReportClinic;
  patient: VisitReportPatient;
  visit: VisitReportVisit;
  images: VisitReportImage[];
  findings: VisitReportFinding[];
  healthMetrics: VisitReportHealthMetrics;
  prescriptions: VisitReportPrescription[];
  feedback?: VisitReportFeedback | null;
  disclaimer: string;
  generatedAt: string;
}
export interface AdminPatientDto {
  id: number;

  userId?: number | null;

  code: string;

  fullName: string;

  gender: number;

  phone: string;

  address?: string | null;

  hasAccount: boolean;

  isActive?: boolean | null;

  patientRowVersion: string;

  accountRowVersion?: string | null;
}
export interface AdminUpdatePatientRequest {
  fullName: string;
  gender: number;
  address?: string | null;
  rowVersion: string;
}