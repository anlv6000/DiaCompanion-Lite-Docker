import { gradingApi } from "@/api/services";
import { useAsync } from "@/lib/hooks";
import { diabetesTypes, eyes, genders, label, metricContexts } from "@/lib/enums";
import { fmtDate } from "@/lib/format";
import type { ImageClinicalContextDto, PatientDetailDto, VisitHealthMetricsDto } from "@/types/api";

/* Khung "Thông tin lâm sàng" đặt cạnh ảnh ở mọi màn xem ảnh:
   trang kết quả AI, chấm mù vòng 1 (bác sĩ khám), vòng 2 và 3 (người chấm độc lập).
   Người chấm không nhận mã nghiên cứu và ngày khám (backend đã lược bỏ). */

const v = (x?: number | null, unit = "") => (x == null ? "—" : `${x}${unit}`);

export function ClinicalContextView({ ctx, compact = false }: { ctx: ImageClinicalContextDto; compact?: boolean }) {
  const bp = ctx.systolicBp != null && ctx.diastolicBp != null ? `${ctx.systolicBp}/${ctx.diastolicBp} mmHg` : "—";
  const glu = ctx.glucose == null
    ? "—"
    : `${ctx.glucose} mmol/L${ctx.glucoseContext ? ` (${label(metricContexts, ctx.glucoseContext).toLowerCase()})` : ""}`;
  const rows: [string, string][] = [
    ...(ctx.researchCode ? [["Mã nghiên cứu", ctx.researchCode] as [string, string]] : []),
    ["Mắt", label(eyes, ctx.eye)],
    // Người chấm vòng 2/3 không nhận năm sinh (backend trả 0): chỉ hiện tuổi.
    ["Tuổi", ctx.ageAtVisit != null
      ? ctx.birthYear > 1900 ? `${ctx.ageAtVisit} (sinh ${ctx.birthYear})` : String(ctx.ageAtVisit)
      : ctx.birthYear > 1900 ? `sinh ${ctx.birthYear}` : "—"],
    ["Giới", label(genders, ctx.gender)],
    ["Loại ĐTĐ", label(diabetesTypes, ctx.diabetesType)],
    ["Số năm mắc ĐTĐ", v(ctx.diabetesDurationYears, " năm")],
    ["HbA1c lượt khám", v(ctx.hbA1c, " %")],
    ["HbA1c nền", v(ctx.baselineHbA1c, " %")],
    ["Huyết áp", bp],
    ["Đường huyết", glu],
    ...(ctx.visitDate ? [["Ngày khám", fmtDate(ctx.visitDate)] as [string, string]] : []),
  ];
  return (
    <div className={`clin-ctx ${compact ? "compact" : ""}`}>
      <dl>
        {rows.map(([k, val]) => (
          <div key={k}>
            <dt>{k}</dt>
            <dd>{val}</dd>
          </div>
        ))}
      </dl>
      {ctx.patientNote && (
        <p className="clin-note">
          <span>Ghi chú lâm sàng:</span> {ctx.patientNote}
        </p>
      )}
    </div>
  );
}

/** Tải thông tin lâm sàng theo ảnh đã lưu trên hệ thống. */
export function ClinicalContextPanel({ imageId, compact = false }: { imageId: number; compact?: boolean }) {
  const ctx = useAsync(() => gradingApi.context(imageId), [imageId]);
  return (
    <section className="panel clin-panel">
      <div className="panel-h">Thông tin lâm sàng</div>
      <div className="panel-b">
        {ctx.data ? (
          <ClinicalContextView ctx={ctx.data} compact={compact} />
        ) : ctx.error ? (
          <div className="state error">
            {ctx.error.message} <button className="lite-linkbtn" onClick={ctx.reload}>Thử lại</button>
          </div>
        ) : (
          <div className="skeleton" style={{ height: 120 }} />
        )}
      </div>
    </section>
  );
}

/** Dựng cùng dữ liệu từ hồ sơ + chỉ số của lượt (ảnh chưa nạp lên máy chủ). */
export function contextFromVisit(
  patient: PatientDetailDto,
  metrics: VisitHealthMetricsDto | null,
  eye: number,
  visitDate?: string | null,
): ImageClinicalContextDto {
  const bp = metrics?.bloodPressure;
  const dob = new Date(patient.dateOfBirth);
  const year = dob.getFullYear();
  const at = visitDate ? new Date(visitDate) : new Date();
  const beforeBirthday = at.getMonth() < dob.getMonth() || (at.getMonth() === dob.getMonth() && at.getDate() < dob.getDate());
  return {
    imageId: 0,
    eye,
    researchCode: patient.fullName,
    visitDate: visitDate ?? null,
    birthYear: year,
    ageAtVisit: year > 1900 ? at.getFullYear() - year - (beforeBirthday ? 1 : 0) : null,
    gender: patient.gender,
    diabetesType: patient.diabetesType,
    diabetesDurationYears: patient.diabetesDurationYears ?? null,
    baselineHbA1c: patient.baselineHbA1c ?? null,
    patientNote: patient.note ?? null,
    hbA1c: metrics?.hbA1c?.value ?? null,
    systolicBp: bp?.systolicValue ?? (bp?.metricType === 3 ? bp.value : null),
    diastolicBp: bp?.diastolicValue ?? (bp?.metricType === 4 ? bp.value : null),
    glucose: metrics?.glucose?.value ?? null,
    glucoseContext: metrics?.glucose?.context ?? null,
  };
}
