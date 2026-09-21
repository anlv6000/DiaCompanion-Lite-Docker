import {
  createContext,
  useContext,
  useState,
  useCallback,
  useMemo,
  type ReactNode,
} from "react";
import {
  authApi,
  usersApi,
  patientsApi,
  visitsApi,
  imagesApi,
  diagnosesApi,
  triageApi,
  prescriptionsApi,
  receptionApi,
  recheckApi,
  monitoringApi,
  engagementApi,
  blogApi,
  adminApi,
  exportApi,
} from "@/api/services";
import type * as T from "@/types/api";

/**
 * DataContext — tầng dữ liệu DUY NHẤT giữa UI và backend.
 *
 * Nguyên tắc:
 *  - Dữ liệu từ backend PHẢI đi qua DataContext trước khi tới màn hình.
 *  - Chỉ PAGE gọi useData(); COMPONENT nhận dữ liệu qua props, không tự fetch.
 *  - Không page nào import trực tiếp @/api/services — luôn qua useData().
 *
 * DataContext bọc toàn bộ nhóm service thành "action" trả Promise, kèm vài
 * mẩu state dùng chung (doctors, dashboard, triageCount) nạp sẵn.
 */

interface DataValue {
  // state dùng chung
  doctors: T.DoctorDto[] | null;
  dashboard: T.DashboardDto | null;
  triageCount: T.TriageCountDto | null;
  loadDoctors: () => Promise<T.DoctorDto[]>;
  loadDashboard: () => Promise<T.DashboardDto>;
  loadTriageCount: () => Promise<T.TriageCountDto>;
  // nhóm action theo nghiệp vụ
  auth: ReturnType<typeof buildAuth>;
  users: ReturnType<typeof buildUsers>;
  patients: ReturnType<typeof buildPatients>;
  visits: ReturnType<typeof buildVisits>;
  images: ReturnType<typeof buildImages>;
  diagnoses: ReturnType<typeof buildDiagnoses>;
  triage: ReturnType<typeof buildTriage>;
  prescriptions: ReturnType<typeof buildPrescriptions>;
  reception: ReturnType<typeof buildReception>;
  recheck: ReturnType<typeof buildRecheck>;
  monitoring: ReturnType<typeof buildMonitoring>;
  engagement: ReturnType<typeof buildEngagement>;
  blog: ReturnType<typeof buildBlog>;
  admin: ReturnType<typeof buildAdmin>;
  exports: ReturnType<typeof buildExports>;
}

const buildAuth = () => ({
  me: () => authApi.me(),
  refresh: (refreshToken: string) => authApi.refresh(refreshToken),
  change: (b: T.ChangePasswordRequest) => authApi.change(b),
});
const buildUsers = () => ({
  list: (p: Record<string, unknown>) => usersApi.list(p),
  get: (id: number) => usersApi.get(id),
  create: (b: T.CreateStaffRequest) => usersApi.create(b),
  update: (id: number, b: T.UpdateStaffRequest) => usersApi.update(id, b),
  setActive: (id: number, v: boolean, rowVersion: string) =>
    usersApi.active(id, v, rowVersion),
  resetPassword: (id: number, rowVersion: string) =>
    usersApi.reset(id, rowVersion),
  doctors: () => usersApi.doctors(),
  linkablePatients: (q?: string) => usersApi.linkablePatients(q),
});
const buildPatients = () => ({
  list: (p: Record<string, unknown>) => patientsApi.list(p),
  get: (id: number) => patientsApi.get(id),
  create: (b: T.CreatePatientRequest) => patientsApi.create(b),
  update: (id: number, b: T.UpdatePatientRequest) => patientsApi.update(id, b),
  reissue: (id: number) => patientsApi.reissue(id),
  void: (id: number, reason: string, rowVersion: string) =>
    patientsApi.void(id, reason, rowVersion),

   // ============================================================
  // ADMIN PATIENT MANAGEMENT
  // ============================================================

  adminList: (
    p: Record<string, unknown>,
  ) =>
    patientsApi.adminList(p),

  adminUpdate: (
    id: number,
    b: T.AdminUpdatePatientRequest,
  ) =>
    patientsApi.adminUpdate(id, b),

  adminSetActive: (
    id: number,
    value: boolean,
    rowVersion: string,
  ) =>
    patientsApi.adminSetActive(
      id,
      value,
      rowVersion,
    ),
});
const buildVisits = () => ({
  list: (p: Record<string, unknown>) => visitsApi.list(p),
  assignedToMe: (p: Record<string, unknown>) => visitsApi.assignedToMe(p),
  get: (id: number) => visitsApi.get(id),
  healthMetrics: (id: number) => visitsApi.healthMetrics(id),
  saveHealthMetrics: (id: number, b: T.SaveVisitHealthMetricsRequest) =>
    visitsApi.saveHealthMetrics(id, b),
  create: (b: T.CreateVisitRequest) => visitsApi.create(b),
  close: (id: number, b: T.CloseVisitRequest) => visitsApi.close(id, b),
  void: (id: number, reason: string, rowVersion: string) =>
    visitsApi.void(id, reason, rowVersion),
});
const buildImages = () => ({
  list: (p: Record<string, unknown>) => imagesApi.list(p),
  get: (id: number) => imagesApi.get(id),
  upload: (
    file: File,
    patientId: number,
    visitId: number,
    eye: number,
  ) => imagesApi.upload(file, patientId, visitId, eye),
  quality: (
    id: number,
    status: number,
    note: string | undefined,
    rowVersion: string,
  ) => imagesApi.quality(id, status, note, rowVersion),
  void: (id: number, reason: string, rowVersion: string) =>
    imagesApi.void(id, reason, rowVersion),
  content: (id: number) => imagesApi.content(id),
});
const buildDiagnoses = () => ({
  run: (imageId: number) => diagnosesApi.run(imageId),
  get: (id: number) => diagnosesApi.get(id),
  byImage: (imageId: number) => diagnosesApi.byImage(imageId),
  lesionMask: (id: number) => diagnosesApi.lesionMask(id),
  fractalImage: (id: number) => diagnosesApi.fractalImage(id),
  void: (id: number, reason: string, rowVersion: string) =>
    diagnosesApi.void(id, reason, rowVersion),
  progression: (patientId: number, months: number) =>
    diagnosesApi.progression(patientId, months),
});
const buildTriage = () => ({
  queue: (p: Record<string, unknown>) => triageApi.queue(p),
  count: () => triageApi.count(),
  approve: (id: number, rowVersion?: string | null) =>
    triageApi.approve(id, rowVersion),
  override: (id: number, b: T.OverrideRequest) => triageApi.override(id, b),
  voidReview: (id: number, reason: string, rowVersion: string) =>
    triageApi.voidReview(id, reason, rowVersion),
});
const buildPrescriptions = () => ({
  list: (p: Record<string, unknown>) => prescriptionsApi.list(p),
  get: (id: number) => prescriptionsApi.get(id),
  create: (b: T.CreatePrescriptionRequest) => prescriptionsApi.create(b),
  update: (id: number, b: T.UpdatePrescriptionRequest) =>
    prescriptionsApi.update(id, b),
  void: (id: number, reason: string, rowVersion: string) =>
    prescriptionsApi.void(id, reason, rowVersion),
  adherence: (patientId: number, days?: number) =>
    prescriptionsApi.adherence(patientId, days),
});
const buildReception = () => ({
  // q lọc bác sĩ theo họ tên hoặc số chứng chỉ hành nghề.
  // Wrapper này phải nhận và chuyển tiếp ĐỦ tham số: thiếu một cái là màn hình
  // gọi vẫn chạy nhưng bộ lọc im lặng không có tác dụng.
  onDuty: (date?: string, shift?: number, q?: string) =>
    receptionApi.onDuty(date, shift, q),
  listShifts: (doctorId?: number) => receptionApi.listShifts(doctorId),
  createShift: (b: T.CreateDoctorShiftRequest) => receptionApi.createShift(b),
  createShiftsBatch: (b: T.CreateDoctorShiftsBatchRequest) =>
    receptionApi.createShiftsBatch(b),
  setShiftActive: (id: number, active: boolean, rowVersion: string) =>
    receptionApi.setShiftActive(id, active, rowVersion),
  deleteShift: (id: number, rowVersion: string) =>
    receptionApi.deleteShift(id, rowVersion),
});
const buildRecheck = () => ({
  me: () => recheckApi.me(),
  patient: (patientId: number) => recheckApi.patient(patientId),
  due: (p: Record<string, unknown>) => recheckApi.due(p),
  overdueCount: () => recheckApi.overdueCount(),
});
const buildMonitoring = () => ({
  metrics: (p: Record<string, unknown>) => monitoringApi.metrics(p),
  summary: (id: number, days?: number) => monitoringApi.summary(id, days),
  lifestyle: (p: Record<string, unknown>) => monitoringApi.lifestyle(p),
  today: (patientId?: number) => monitoringApi.today(patientId),
});
const buildEngagement = () => ({
  notifications: (p: Record<string, unknown>) => engagementApi.notifications(p),
  unread: () => engagementApi.unread(),
  read: (id: number) => engagementApi.read(id),
  readAll: () => engagementApi.readAll(),
  symptoms: (p: Record<string, unknown>) => engagementApi.symptoms(p),
  reply: (id: number, reply: string, rowVersion: string) =>
    engagementApi.reply(id, reply, rowVersion),
  feedback: (p: Record<string, unknown>) => engagementApi.feedback(p),
  feedbackSummary: () => engagementApi.feedbackSummary(),
});
const buildBlog = () => ({
  manage: (p: Record<string, unknown>) => blogApi.manage(p),
  published: (p: Record<string, unknown>) => blogApi.published(p),
  get: (id: number) => blogApi.get(id),
  create: (b: T.SaveBlogRequest) => blogApi.create(b),
  update: (id: number, b: T.SaveBlogRequest) => blogApi.update(id, b),
  publish: (id: number, v: boolean, rowVersion: string) =>
    blogApi.publish(id, v, rowVersion),
  delete: (id: number, rowVersion: string) =>
    blogApi.delete(id, rowVersion),
});
const buildAdmin = () => ({
  dashboard: (p: Record<string, unknown> = {}) => adminApi.dashboard(p),
  configs: () => adminApi.configs(),
  updateConfig: (key: string, v: string, rowVersion: string) =>
    adminApi.updateConfig(key, v, rowVersion),
  impact: (key: string, proposed: number) => adminApi.impact(key, proposed),
  audit: (p: Record<string, unknown>) => adminApi.audit(p),
});
const buildExports = () => ({
  visitReport: (id: number) => exportApi.visitReport(id),
  conflicts: () => exportApi.conflicts(),
  conflictsCsv: () => exportApi.conflictsCsv(),
});

const DataContext = createContext<DataValue | null>(null);

export function DataProvider({ children }: { children?: ReactNode }) {
  const [doctors, setDoctors] = useState<T.DoctorDto[] | null>(null);
  const [dashboard, setDashboard] = useState<T.DashboardDto | null>(null);
  const [triageCount, setTriageCount] = useState<T.TriageCountDto | null>(null);

  const loadDoctors = useCallback(async () => {
    const d = await usersApi.doctors();
    setDoctors(d);
    return d;
  }, []);
  const loadDashboard = useCallback(async () => {
    const d = await adminApi.dashboard();
    setDashboard(d);
    return d;
  }, []);
  const loadTriageCount = useCallback(async () => {
    const c = await triageApi.count();
    setTriageCount(c);
    return c;
  }, []);
  const value = useMemo<DataValue>(
    () => ({
      doctors,
      dashboard,
      triageCount,
      loadDoctors,
      loadDashboard,
      loadTriageCount,
      auth: buildAuth(),
      users: buildUsers(),
      patients: buildPatients(),
      visits: buildVisits(),
      images: buildImages(),
      diagnoses: buildDiagnoses(),
      triage: buildTriage(),
      prescriptions: buildPrescriptions(),
      reception: buildReception(),
      recheck: buildRecheck(),
      monitoring: buildMonitoring(),
      engagement: buildEngagement(),
      blog: buildBlog(),
      admin: buildAdmin(),
      exports: buildExports(),
    }),
    [
      doctors,
      dashboard,
      triageCount,
      loadDoctors,
      loadDashboard,
      loadTriageCount,
    ],
  );

  return <DataContext.Provider value={value}>{children}</DataContext.Provider>;
}

export function useData(): DataValue {
  const ctx = useContext(DataContext);
  if (!ctx) throw new Error("useData phải nằm trong <DataProvider>");
  return ctx;
}
