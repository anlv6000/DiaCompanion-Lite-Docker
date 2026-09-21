import { http } from "@/api/client";
import { query } from "@/lib/format";
import type * as T from "@/types/api";
import { getRoles, primaryWebRole } from "@/lib/roles";

function normalizeLogin(value: T.LoginResponse): T.LoginResponse {
  const roles = getRoles(value);
  return { ...value, roles, role: value.role || primaryWebRole(roles) };
}

function normalizeStaff(value: T.StaffUserDto): T.StaffUserDto {
  const roles = getRoles(value);
  return { ...value, roles, role: value.role || roles[0] };
}

// Console bệnh viện: chỉ nhân viên đăng nhập bằng email + mật khẩu.
// Không có OTP/đăng nhập bằng số điện thoại (đó là luồng của app bệnh nhân).
// Quên mật khẩu do Admin cấp lại (usersApi.reset), không self-service qua SĐT.
export const authApi = {
  login: async (body: T.LoginRequest) =>
    normalizeLogin(
      await http.post<T.LoginResponse>(
        "/api/auth/login",
        body,
      ),
    ),

  forgotPassword: (phone: string) =>
    http.post<T.OtpResponse>(
      "/api/auth/forgot-password",
      { phone },
    ),

  resetPassword: (
    body: T.ResetPasswordRequest,
  ) =>
    http.post<T.ApiMessage>(
      "/api/auth/reset-password",
      body,
    ),

  me: async () =>
    normalizeLogin(
      await http.get<T.LoginResponse>(
        "/api/auth/me",
      ),
    ),

  profile: () =>
    http.get<T.StaffProfileDto>("/api/auth/profile"),

  updateProfile: (body: T.UpdateStaffProfileRequest) =>
    http.put<T.StaffProfileDto>("/api/auth/profile", body),

  refresh: async (refreshToken: string) =>
    normalizeLogin(
      await http.post<T.LoginResponse>(
        "/api/auth/refresh",
        { refreshToken },
      ),
    ),

  logout: () =>
    http.post<T.ApiMessage>(
      "/api/auth/logout",
    ),

  change: async (body: T.ChangePasswordRequest) =>
    normalizeLogin(
      await http.post<T.ChangePasswordResponse>(
        "/api/auth/change-password",
        body,
      ),
    ) as T.ChangePasswordResponse,
};
export const usersApi = {
  list: async (p: Record<string, unknown>) => {
    const result = await http.get<T.PagedResult<T.StaffUserDto>>("/api/users" + query(p));
    return { ...result, items: result.items.map(normalizeStaff) };
  },
  get: async (id: number) => normalizeStaff(await http.get<T.StaffUserDto>(`/api/users/${id}`)),
  create: (b: T.CreateStaffRequest) =>
    http.post<T.TempCredentialResponse>("/api/users", b),
  update: (id: number, b: T.UpdateStaffRequest) =>
    http.put<T.ApiMessage>(`/api/users/${id}`, b),
  active: (id: number, value: boolean, rowVersion: string) =>
    http.put<T.ApiMessage>(`/api/users/${id}/active?value=${value}`, {
      rowVersion,
    }),
  reset: (id: number, rowVersion: string) =>
    http.post<T.TempCredentialResponse>(`/api/users/${id}/reset-password`, {
      rowVersion,
    }),
  doctors: () => http.get<T.DoctorDto[]>("/api/users/doctors"),
  linkablePatients: (q?: string) =>
    http.get<T.LinkablePatientUserDto[]>(
      "/api/users/linkable-patients" + query({ q: q || undefined }),
    ),
};
export const patientsApi = {
  
  list: (p: Record<string, unknown>) =>
    http.get<T.PagedResult<T.PatientListItemDto>>("/api/patients" + query(p)),
  get: (id: number) => http.get<T.PatientDetailDto>(`/api/patients/${id}`),
  create: (b: T.CreatePatientRequest) =>
    http.post<T.CreatePatientResponse>("/api/patients", b),
  update: (id: number, b: T.UpdatePatientRequest) =>
    http.put<T.PatientDetailDto>(`/api/patients/${id}`, b),
  reissue: (id: number) =>
    http.post<T.TempCredentialResponse>(
      `/api/patients/${id}/reissue-credentials`,
    ),
  void: (id: number, reason: string, rowVersion: string) =>
    http.put<T.ApiMessage>(`/api/patients/${id}/void`, { reason, rowVersion }),


  adminList: (p: Record<string, unknown>) =>
  http.get<T.PagedResult<T.AdminPatientDto>>(
    "/api/patients/admin" + query(p),
  ),

adminUpdate: (
  id: number,
  body: T.AdminUpdatePatientRequest,
) =>
  http.put<T.ApiMessage>(
    `/api/patients/admin/${id}`,
    body,
  ),

adminSetActive: (
  id: number,
  value: boolean,
  rowVersion: string,
) =>
  http.put<T.ApiMessage>(
    `/api/patients/admin/${id}/active` +
      query({ value }),
    {
      rowVersion,
    },
  ),
};
export const visitsApi = {
  list: (p: Record<string, unknown>) =>
    http.get<T.PagedResult<T.VisitDto>>("/api/visits" + query(p)),
  assignedToMe: (p: Record<string, unknown>) =>
    http.get<T.PagedResult<T.VisitDto>>("/api/visits/assigned-to-me" + query(p)),
  get: (id: number) => http.get<T.VisitDto>(`/api/visits/${id}`),
  healthMetrics: (id: number) =>
    http.get<T.VisitHealthMetricsDto>(`/api/visits/${id}/health-metrics`),
  saveHealthMetrics: (id: number, b: T.SaveVisitHealthMetricsRequest) =>
    http.put<T.VisitHealthMetricsDto>(`/api/visits/${id}/health-metrics`, b),
  create: (b: T.CreateVisitRequest) => http.post<T.VisitDto>("/api/visits", b),
  close: (id: number, b: T.CloseVisitRequest) =>
    http.put<T.VisitDto>(`/api/visits/${id}/close`, b),
  void: (id: number, reason: string, rowVersion: string) =>
    http.put<T.ApiMessage>(`/api/visits/${id}/void`, { reason, rowVersion }),
};
export const imagesApi = {
  list: (p: Record<string, unknown>) =>
    http.get<T.FundusImageDto[]>("/api/images" + query(p)),
  get: (id: number) => http.get<T.FundusImageDto>(`/api/images/${id}`),
  upload: (
    file: File,
    patientId: number,
    visitId: number,
    eye: number,
  ) => {
    const f = new FormData();
    f.append("file", file);
    f.append("patientId", String(patientId));
    f.append("visitId", String(visitId));
    f.append("eye", String(eye));
    return http.upload<T.FundusImageDto>("/api/images", f);
  },
  quality: (
    id: number,
    status: number,
    note: string | undefined,
    rowVersion: string,
  ) =>
    http.put<T.ApiMessage>(`/api/images/${id}/quality`, {
      status,
      note,
      rowVersion,
    }),
  void: (id: number, reason: string, rowVersion: string) =>
    http.put<T.ApiMessage>(`/api/images/${id}/void`, {
      reason,
      rowVersion,
    }),
  content: (id: number) => http.blob(`/api/images/${id}/content`),
};
export const diagnosesApi = {
  run: (imageId: number) =>
    http.post<T.AiDiagnosisDto>(`/api/diagnoses/run/${imageId}`),
  get: (id: number) => http.get<T.AiDiagnosisDto>(`/api/diagnoses/${id}`),
  byImage: (imageId: number) =>
    http.get<T.AiDiagnosisDto[]>(`/api/diagnoses/by-image/${imageId}`),
  lesionMask: (id: number) => http.blob(`/api/diagnoses/${id}/lesion-mask`),
  fractalImage: (id: number) => http.blob(`/api/diagnoses/${id}/fractal-image`),
  void: (id: number, reason: string, rowVersion: string) =>
    http.put<T.ApiMessage>(`/api/diagnoses/${id}/void`, {
      reason,
      rowVersion,
    }),
  progression: (patientId: number, months: number) =>
    http.get<T.ProgressionDto>(
      `/api/diagnoses/progression/${patientId}?months=${months}`,
    ),
};
export const triageApi = {
  queue: (p: Record<string, unknown>) =>
    http.get<T.KeysetResult<T.TriageItemDto>>("/api/triage" + query(p)),
  count: () => http.get<T.TriageCountDto>("/api/triage/count"),
  approve: (id: number, rowVersion?: string | null) =>
    http.post<T.ReviewDto>(`/api/triage/${id}/approve`, { rowVersion }),
  override: (id: number, b: T.OverrideRequest) =>
    http.post<T.ReviewDto>(`/api/triage/${id}/override`, b),
  voidReview: (id: number, reason: string, rowVersion: string) =>
    http.put<T.ApiMessage>(`/api/triage/reviews/${id}/void`, {
      reason,
      rowVersion,
    }),
};
export const prescriptionsApi = {
  list: (p: Record<string, unknown>) =>
    http.get<T.PagedResult<T.PrescriptionDto>>("/api/prescriptions" + query(p)),
  get: (id: number) => http.get<T.PrescriptionDto>(`/api/prescriptions/${id}`),
  create: (b: T.CreatePrescriptionRequest) =>
    http.post<T.PrescriptionDto>("/api/prescriptions", b),
  update: (id: number, b: T.UpdatePrescriptionRequest) =>
    http.put<T.PrescriptionDto>(`/api/prescriptions/${id}`, b),
  void: (id: number, reason: string, rowVersion: string) =>
    http.put<T.ApiMessage>(`/api/prescriptions/${id}/void`, {
      reason,
      rowVersion,
    }),
  adherence: (patientId: number, days = 30) =>
    http.get<T.AdherenceDto>(
      `/api/prescriptions/adherence/${patientId}?days=${days}`,
    ),
};
export const recheckApi = {
  me: () => http.get<T.RecheckDto>("/api/recheck/me"),
  patient: (patientId: number) =>
    http.get<T.RecheckDto>(`/api/recheck/patient/${patientId}`),
  due: (p: Record<string, unknown>) =>
    http.get<T.PagedResult<T.RecheckDto>>("/api/recheck/due" + query(p)),
  overdueCount: () => http.get<{ overdue: number }>("/api/recheck/overdue-count"),
};

// Nghiệp vụ lễ tân: bác sĩ đang trực + quản lý ca trực cố định theo tuần.
export const receptionApi = {
  /**
   * Bác sĩ đang trực.
   *
   * `q` lọc theo họ tên hoặc số chứng chỉ hành nghề — backend nhận cùng
   * endpoint này, không có API tìm kiếm riêng.
   *
   * Lưu ý: backend hiện BỎ QUA `date` và `shift`; ca trực luôn được suy ra từ
   * giờ hệ thống theo cấu hình ShiftMorningStart/AfternoonStart/NightStart.
   * Hai tham số giữ lại để không phá lời gọi cũ.
   */
  onDuty: (date?: string, shift?: number, q?: string) =>
    http.get<T.OnDutyResponse>(
      "/api/reception/on-duty" + query({ date, shift, q }),
    ),
  listShifts: (doctorId?: number) =>
    http.get<T.DoctorShiftDto[]>(
      "/api/reception/shifts" + query({ doctorId }),
    ),
  createShift: (b: T.CreateDoctorShiftRequest) =>
    http.post<T.DoctorShiftDto>("/api/reception/shifts", b),
  createShiftsBatch: (b: T.CreateDoctorShiftsBatchRequest) =>
    http.post<T.DoctorShiftDto[]>("/api/reception/shifts/batch", b),
  setShiftActive: (id: number, active: boolean, rowVersion: string) =>
    http.put<T.DoctorShiftDto>(
      `/api/reception/shifts/${id}/active?active=${active}&rowVersion=${encodeURIComponent(rowVersion)}`,
    ),
  deleteShift: (id: number, rowVersion: string) =>
    http.delete<void>(
      `/api/reception/shifts/${id}?rowVersion=${encodeURIComponent(rowVersion)}`,
    ),
};
export const monitoringApi = {
  metrics: (p: Record<string, unknown>) =>
    http.get<T.KeysetResult<T.HealthMetricDto>>(
      "/api/monitoring/metrics" + query(p),
    ),
  summary: (id: number, days = 30) =>
    http.get<T.MetricSummary>(
      `/api/monitoring/metrics/summary/${id}?days=${days}`,
    ),
  lifestyle: (p: Record<string, unknown>) =>
    http.get<T.LifestyleLogDto[]>("/api/monitoring/lifestyle" + query(p)),
  today: (patientId?: number) =>
    http.get<T.MedicationLogDto[]>(
      "/api/monitoring/medications/today" + query({ patientId }),
    ),
};
export const engagementApi = {
  notifications: (p: Record<string, unknown>) =>
    http.get<T.PagedResult<T.NotificationDto>>(
      "/api/engagement/notifications" + query(p),
    ),
  unread: () =>
    http.get<{ count: number }>("/api/engagement/notifications/unread-count"),
  read: (id: number) =>
    http.put<T.ApiMessage>(`/api/engagement/notifications/${id}/read`),
  readAll: () =>
    http.put<T.ApiMessage>("/api/engagement/notifications/read-all"),
  symptoms: (p: Record<string, unknown>) =>
    http.get<T.PagedResult<T.SymptomReportDto>>(
      "/api/engagement/symptoms" + query(p),
    ),
  reply: (id: number, reply: string, rowVersion: string) =>
    http.put<T.ApiMessage>(`/api/engagement/symptoms/${id}/reply`, {
      reply,
      rowVersion,
    }),
  feedback: (p: Record<string, unknown>) =>
    http.get<T.PagedResult<T.FeedbackDto>>(
      "/api/engagement/feedback" + query(p),
    ),
  feedbackSummary: () =>
    http.get<T.FeedbackSummary>("/api/engagement/feedback/summary"),
};
export const blogApi = {
  published: (p: Record<string, unknown>) =>
    http.get<T.PagedResult<T.BlogPostDto>>("/api/blog/published" + query(p)),
  get: (id: number) => http.get<T.BlogPostDto>(`/api/blog/${id}`),
  manage: (p: Record<string, unknown>) =>
    http.get<T.PagedResult<T.BlogPostDto>>("/api/blog/manage" + query(p)),
  create: (b: T.SaveBlogRequest) => http.post<T.BlogPostDto>("/api/blog", b),
  update: (id: number, b: T.SaveBlogRequest) =>
    http.put<T.BlogPostDto>(`/api/blog/${id}`, b),
  publish: (id: number, value: boolean, rowVersion: string) =>
    http.put<T.ApiMessage>(
      `/api/blog/${id}/${value ? "publish" : "unpublish"}`,
      { rowVersion },
    ),
  delete: (id: number, rowVersion: string) =>
    http.delete<T.ApiMessage>(
      `/api/blog/${id}?rowVersion=${encodeURIComponent(rowVersion)}`,
    ),
};
export const adminApi = {
  dashboard: (p: Record<string, unknown> = {}) =>
    http.get<T.DashboardDto>("/api/admin/dashboard" + query(p)),
  configs: () => http.get<T.SystemConfigDto[]>("/api/admin/configs"),
  updateConfig: (key: string, value: string, rowVersion: string) =>
    http.put<T.ApiMessage>(`/api/admin/configs/${encodeURIComponent(key)}`, {
      value,
      rowVersion,
    }),
  impact: (key: string, proposed: number) =>
    http.get<T.ThresholdImpactDto>(
      "/api/admin/configs/threshold-impact" + query({ key, proposed }),
    ),
  audit: (p: Record<string, unknown>) =>
    http.get<T.KeysetResult<T.AuditLogDto>>("/api/admin/audit" + query(p)),
};
export const exportApi = {
  visitReport: (id: number) =>
    http.get<T.VisitReport>(`/api/export/visit-report/${id}`),
  conflicts: () =>
    http.get<T.DisagreementExport>("/api/export/disagreement-cases"),
  conflictsCsv: () =>
    http.blob("/api/export/disagreement-cases.csv"),
};

export const researchApi = {
  /** CSV dataset nghiên cứu (Gap 2). Trả Blob kèm tên tệp gợi ý. */
  datasetCsv: (from?: string, to?: string) =>
    http.blob("/api/research/dataset.csv" + query({ from, to })),
};
