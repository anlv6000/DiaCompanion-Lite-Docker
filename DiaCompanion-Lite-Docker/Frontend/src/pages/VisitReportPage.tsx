import { useEffect, useState } from "react";
import { useData } from "@/contexts/DataContext";
import { useAsync } from "@/lib/hooks";
import {
  PageHeader,
  Panel,
  Button,
  LoadState,
  GradeBadge,
  StatusBadge,
  Icon,
} from "@/components/ui";
import { ProtectedImage } from "@/components/ProtectedImage";
import { fmtDate, num } from "@/lib/format";
import {
  diabetesTypes,
  genders,
  grades,
  referralTypes,
  metricContexts,
  label,
} from "@/lib/enums";
import type {
  VisitReport,
  VisitReportFinding,
  VisitReportImage,
} from "@/types/api";

const vi = {
  patient: "Hồ sơ bệnh án",
  visit: "Lượt khám",
  findings: "Ảnh đáy mắt",
  metrics: "Chỉ số",
  prescriptions: "Đơn thuốc",
  conclusion: "Kết luận",
  feedback: "Phản hồi bệnh nhân",
  disclaimer: "Lưu ý lâm sàng",
};

const en = {
  patient: "Medical record",
  visit: "Visit",
  findings: "Fundus images",
  metrics: "Health metrics",
  prescriptions: "Prescriptions",
  conclusion: "Conclusion",
  feedback: "Patient feedback",
  disclaimer: "Clinical notice",
};

// Diễn giải mức độ bằng lời cho bệnh nhân (theo thứ tự grade 0..4).
const gradeMeaning = [
  "Không phát hiện dấu hiệu bệnh võng mạc đái tháo đường ở mắt này.",
  "Có tổn thương rất nhẹ. Hãy duy trì kiểm soát đường huyết và khám lại theo hẹn.",
  "Tổn thương mức trung bình. Cần theo dõi sát, có thể cần khám chuyên khoa mắt.",
  "Tổn thương nặng. Nên khám chuyên khoa mắt sớm.",
  "Bệnh võng mạc giai đoạn tăng sinh — mức nặng nhất. Cần khám chuyên khoa mắt ngay.",
];

export function VisitReportPage({ visitId }: { visitId: number }) {
  const ctx = useData();
  const data = useAsync<VisitReport>(
    () => ctx.exports.visitReport(visitId),
    [visitId],
  );
  const [language, setLanguage] = useState<"vi" | "en">("vi");
  const [sections, setSections] = useState({
    patient: true,
    visit: true,
    findings: true,
    metrics: true,
    prescriptions: true,
    conclusion: true,
    feedback: true,
    disclaimer: true,
  });

  const labels = language === "vi" ? vi : en;
  const toggle = (key: keyof typeof sections) =>
    setSections((x) => ({ ...x, [key]: !x[key] }));

  const report = data.data;

  // Tương thích cả API cũ và API mới.
  // API cũ có thể chưa trả images / healthMetrics, nên tuyệt đối không đọc
  // .length hoặc property con trực tiếp từ undefined.
  const findings: VisitReportFinding[] = report?.findings ?? [];
  const images: VisitReportImage[] =
    report?.images ??
    findings.map(
      (f: any) =>
        ({
          imageId: f.imageId,
          eye: f.eye,
          qualityStatus: f.qualityStatus ?? 0,
          qualityStatusLabel: f.qualityStatusLabel ?? "Chưa có dữ liệu",
          qualityNote: f.qualityNote ?? null,
        }) as VisitReportImage,
    );
  const prescriptions = report?.prescriptions ?? [];
  const healthMetrics = report?.healthMetrics ?? {
    glucose: null,
    hba1c: null,
    bloodPressure: null,
  };

  return (
    <>
      <PageHeader
        title={`Báo cáo lượt khám #${visitId}`}
        subtitle="Hồ sơ bệnh án của một lượt khám đã hoàn tất, gồm ảnh đáy mắt, kết quả AI, xác nhận bác sĩ, chỉ số, đơn thuốc và phản hồi bệnh nhân."
        actions={
          <Button kind="primary" onClick={() => window.print()}>
            <Icon name="download" />
            Tạo PDF / In
          </Button>
        }
      />

      <div className="report-layout">
        <Panel title="Thiết lập báo cáo">
          <label className="field">
            <span>Ngôn ngữ</span>
            <select
              value={language}
              onChange={(e: any) => setLanguage(e.target.value)}
            >
              <option value="vi">Tiếng Việt</option>
              <option value="en">English</option>
            </select>
          </label>

          <div className="stack" style={{ marginTop: 12 }}>
            {(Object.keys(sections) as (keyof typeof sections)[]).map((k) => (
              <label className="checkbox" key={k}>
                <input
                  type="checkbox"
                  checked={sections[k]}
                  onChange={() => toggle(k)}
                />
                {labels[k]}
              </label>
            ))}
          </div>

          <p className="help">
            Chọn “Save as PDF” trong hộp thoại in của trình duyệt để lưu tệp.
          </p>
        </Panel>

        <LoadState
          loading={data.loading}
          error={data.error}
          empty={!report}
          onRetry={data.reload}
        >
          {report && (
            <article className="report-preview medical-report">
              <header className="report-header medical-report-header">
                <div>
                  <div className="report-clinic-name">{report.clinic.name}</div>
                  <h1>HỒ SƠ BỆNH ÁN</h1>
                  <p>{report.clinic.subtitle}</p>
                </div>
                <div className="mono report-meta">
                  VISIT-{report.visit.id}
                  <br />
                  Xuất: {fmtDate(report.generatedAt, true)}
                </div>
              </header>

              {sections.patient && (
                <ReportSection title={labels.patient}>
                  <div className="detail-grid report-detail-grid">
                    <Info k="Mã bệnh nhân" v={report.patient.code} />
                    <Info k="Họ tên" v={report.patient.fullName} />
                    <Info k="Ngày sinh" v={fmtDate(report.patient.dateOfBirth)} />
                    <Info k="Giới tính" v={label(genders, report.patient.gender)} />
                    <Info
                      k="Loại ĐTĐ"
                      v={label(diabetesTypes, report.patient.diabetesType)}
                    />
                    <Info
                      k="Thời gian mắc"
                      v={
                        report.patient.diabetesDurationYears == null
                          ? "—"
                          : `${report.patient.diabetesDurationYears} năm`
                      }
                    />
                    <Info k="Số điện thoại" v={report.patient.phone} />
                  </div>
                </ReportSection>
              )}

              {sections.visit && (
                <ReportSection title={labels.visit}>
                  <div className="detail-grid report-detail-grid">
                    <Info k="Mã lượt" v={`#${report.visit.id}`} />
                    <Info k="Ngày khám" v={fmtDate(report.visit.visitDate, true)} />
                    <Info k="Bác sĩ" v={report.visit.doctorName} />
                    <Info k="Số chứng chỉ" v={report.visit.doctorLicense} />
                    <Info
                      k="Trạng thái"
                      v={report.visit.status === 1 ? "Đã đóng" : "Đang khám"}
                    />
                    <Info k="Đóng lúc" v={fmtDate(report.visit.closedAt, true)} />
                  </div>
                </ReportSection>
              )}

              {sections.findings && (
                <ReportSection title={labels.findings}>
                  {images.length ? (
                    <div className="report-eye-list">
                      {images.map((image) => (
                        <RetinalImageCard
                          key={image.imageId}
                          image={image}
                          finding={findings.find(
                            (f) => f.imageId === image.imageId,
                          )}
                        />
                      ))}
                    </div>
                  ) : (
                    <p>Không có ảnh đáy mắt trong lượt khám này.</p>
                  )}
                </ReportSection>
              )}

              {sections.metrics && (
                <ReportSection title={labels.metrics}>
                  <div className="report-metric-grid">
                    <MetricCard
                      title="Glucose"
                      value={
                        healthMetrics.glucose
                          ? `${num(healthMetrics.glucose.value)} ${healthMetrics.glucose.unit}`
                          : "Chưa ghi nhận"
                      }
                      detail={
                        healthMetrics.glucose
                          ? `${label(metricContexts, healthMetrics.glucose.context)} · ${fmtDate(healthMetrics.glucose.recordedAt, true)}`
                          : `Không có chỉ số trong ngày khám ${fmtDate(report.visit.visitDate)}`
                      }
                      abnormal={healthMetrics.glucose?.isAbnormal}
                    />
                    <MetricCard
                      title="HbA1c"
                      value={
                        healthMetrics.hba1c
                          ? `${num(healthMetrics.hba1c.value)} ${healthMetrics.hba1c.unit}`
                          : "Chưa ghi nhận"
                      }
                      detail={
                        healthMetrics.hba1c
                          ? fmtDate(healthMetrics.hba1c.recordedAt, true)
                          : `Không có chỉ số trong ngày khám ${fmtDate(report.visit.visitDate)}`
                      }
                      abnormal={healthMetrics.hba1c?.isAbnormal}
                    />
                    <MetricCard
                      title="Blood Pressure"
                      value={
                        healthMetrics.bloodPressure
                          ? `${num(healthMetrics.bloodPressure.systolic, 0)}/${num(healthMetrics.bloodPressure.diastolic, 0)} ${healthMetrics.bloodPressure.unit}`
                          : "Chưa ghi nhận"
                      }
                      detail={
                        healthMetrics.bloodPressure
                          ? fmtDate(
                              healthMetrics.bloodPressure.recordedAt,
                              true,
                            )
                          : `Không có chỉ số trong ngày khám ${fmtDate(report.visit.visitDate)}`
                      }
                      abnormal={healthMetrics.bloodPressure?.isAbnormal}
                    />
                  </div>
                </ReportSection>
              )}

             {sections.prescriptions && (
  <ReportSection title={labels.prescriptions}>
    {prescriptions.length ? (
      prescriptions.map((p, i) => (
        <div className="report-rx" key={`${p.issuedAt}-${i}`}>
          <div className="split report-rx-head">
            <b>Ngày kê: {fmtDate(p.issuedAt, true)}</b>
          </div>
          <div className="report-prescription-list">
            {p.items.map((x, j) => (
              <div className="report-prescription-item" key={j}>
                {/* Cắt drugName 20 kí tự */}
                <strong title={x.drugName}>
                  {x.drugName?.length > 20 ? `${x.drugName.substring(0, 20)}...` : x.drugName}
                </strong>
                
                {/* Cắt dose 20 kí tự nếu cần thiết */}
                <span title={x.dose}>
                  {x.dose?.length > 20 ? `${x.dose.substring(0, 20)}...` : x.dose}
                </span>
                
                <span>{x.timesPerDay} lần/ngày</span>
                <span>{x.durationDays} ngày</span>
                
                {/* Cắt instruction (ghi chú) dài, ví dụ 30 hoặc 20 kí tự */}
                {x.instruction && (
                  <small title={x.instruction}>
                    {x.instruction?.length > 20 ? `${x.instruction.substring(0, 20)}...` : x.instruction}
                  </small>
                )}
              </div>
            ))}
          </div>
        </div>
      ))
    ) : (
      <p>Không có đơn thuốc trong lượt khám này.</p>
    )}
  </ReportSection>
)}
              {sections.conclusion && (
                <ReportSection title={labels.conclusion}>
                  <div className="report-conclusion">
                    <p>{report.visit.conclusion || "Chưa có kết luận."}</p>
                  </div>
                  <div className="detail-grid report-detail-grid report-followup-grid">
                    <Info
                      k="Chuyển tuyến"
                      v={label(referralTypes, report.visit.referral)}
                    />
                    <Info
                      k="Tái khám"
                      v={
                        report.visit.recheckMonths == null
                          ? "—"
                          : `${report.visit.recheckMonths} tháng`
                      }
                    />
                  </div>
                  <div className="report-subblock report-patient-next">
                    <b>Bạn cần làm gì tiếp theo</b>
                    <ul>
                      {report.visit.referral === 3 && (
                        <li>
                          Khám chuyên khoa mắt <strong>ngay</strong> theo chỉ định
                          của bác sĩ.
                        </li>
                      )}
                      {report.visit.referral === 2 && (
                        <li>Sắp xếp khám chuyên khoa mắt theo chỉ định.</li>
                      )}
                      {report.visit.recheckMonths != null && (
                        <li>
                          Tái khám sau {report.visit.recheckMonths} tháng
                          {report.visit.recheckDueDate
                            ? ` (dự kiến ${fmtDate(report.visit.recheckDueDate)})`
                            : ""}
                          .
                        </li>
                      )}
                      <li>
                        Uống thuốc đúng đơn và kiểm soát đường huyết theo hướng dẫn.
                      </li>
                    </ul>
                  </div>
                </ReportSection>
              )}

              

              {sections.disclaimer && (
                <footer className="report-disclaimer">{report.disclaimer}</footer>
              )}
            </article>
          )}
        </LoadState>
      </div>
    </>
  );
}

function RetinalImageCard({
  image,
  finding,
}: {
  image: VisitReportImage;
  finding?: VisitReportFinding;
}) {
  const eyeName = image.eye === 1 ? "Mắt trái (OS)" : "Mắt phải (OD)";
  const legacyFinding = finding as any;
  const action =
    finding?.action ?? (legacyFinding?.ai?.wasOverridden ? 1 : 0);
  const actionLabel =
    finding?.actionLabel ?? (action === 1 ? "Override" : "Approve");
  const reason =
    finding?.reason ?? legacyFinding?.ai?.overrideReason ?? null;

  return (
    <section className="report-eye-card">
      <h3>{eyeName}</h3>

      <div className="report-image-gallery">
        <div className="report-image-primary">
          <div className="report-image-label">
            <strong>Ảnh gốc</strong>
            <small className="mono">Image #{image.imageId}</small>
          </div>
          <ProtectedImage
            imageId={image.imageId}
            alt={`Ảnh đáy mắt gốc ${eyeName}`}
            className="report-clinical-image report-original-image"
            style={{ width: "100%", height: "100%" }}
          />
        </div>

        <div className="report-image-derived-column">
          <ReportAiImage
            title="Lesion mask"
            diagnosisId={finding?.diagnosisId}
            kind="lesion"
            available={Boolean(finding?.urlImageLesionAfterMedical)}
            alt={`Ảnh lesion ${eyeName}`}
          />
          <ReportAiImage
            title="Vessel / Fractal"
            diagnosisId={finding?.diagnosisId}
            kind="fractal"
            available={Boolean(finding?.urlImageVesselAfterMedical)}
            alt={`Ảnh vessel fractal ${eyeName}`}
          />
        </div>
      </div>

      <div className="report-eye-details">
        {finding ? (
          <>
            {/* Kết luận dễ hiểu — bệnh nhân đọc trước */}
            <div className="report-subblock report-eye-verdict">
              <b>Kết luận mắt {eyeName}</b>
              <div className="report-inline-value">
                <GradeBadge grade={finding.finalGrade} />
                <span>{grades[finding.finalGrade] ?? "—"}</span>
              </div>
              <p>{gradeMeaning[finding.finalGrade] ?? ""}</p>
            </div>

            {/* Chi tiết chuyên môn — cho bác sĩ */}
            <div className="report-subblock">
              <b>Chi tiết chuyên môn</b>
              <div className="report-eye-summary-grid">
                <div>
                  <small>AI gợi ý</small>
                  <div className="report-inline-value">
                    <GradeBadge grade={finding.ai.grade} />
                  </div>
                  {finding.ai.isDeferred && (
                    <small>AI đánh dấu cần bác sĩ xem xét.</small>
                  )}
                </div>
                <div>
                  <small>Tổn thương đếm được</small>
                  <div className="report-lesion-grid">
                    <span>Vi phình mạch: {finding.lesions.countMA ?? "—"}</span>
                    <span>Xuất huyết: {finding.lesions.countHE ?? "—"}</span>
                    <span>Xuất tiết cứng: {finding.lesions.countEX ?? "—"}</span>
                    <span>Xuất tiết mềm: {finding.lesions.countSE ?? "—"}</span>
                  </div>
                </div>
              </div>
            </div>

            {/* Xác nhận của bác sĩ */}
            <div className="report-subblock report-doctor-confirmation">
              <b>Bác sĩ xác nhận</b>
              <div className="report-confirm-row">
                <span>Hướng xác nhận:</span>
                <StatusBadge
                  text={actionLabel}
                  kind={action === 1 ? "defer" : "ok"}
                />
              </div>
              <div className="report-confirm-row report-reason-row">
                <span>Lý do:</span>
                <strong>{reason || "—"}</strong>
              </div>
              <small>
                {finding.confirmedBy} · {fmtDate(finding.createdAt, true)}
              </small>
            </div>

            
          </>
        ) : (
          <>
            
            <div className="report-subblock">
              <b>Kết quả</b>
              <p>
                {image.qualityStatus === 2
                  ? "Ảnh không đạt chất lượng nên không có kết quả AI."
                  : "Chưa có kết quả được bác sĩ xác nhận cho ảnh này."}
              </p>
            </div>
          </>
        )}
      </div>
    </section>
  );
}

function ReportAiImage({
  title,
  diagnosisId,
  kind,
  available,
  alt,
}: {
  title: string;
  diagnosisId?: number;
  kind: "lesion" | "fractal";
  available: boolean;
  alt: string;
}) {
  const data = useData();
  const [url, setUrl] = useState("");
  const [error, setError] = useState("");

  useEffect(() => {
    let active = true;
    let objectUrl = "";

    setUrl("");
    setError("");

    if (!diagnosisId || !available) return;

    const loader =
      kind === "lesion"
        ? data.diagnoses.lesionMask(diagnosisId)
        : data.diagnoses.fractalImage(diagnosisId);

    loader
      .then((blob) => {
        if (!active) return;
        objectUrl = URL.createObjectURL(blob);
        setUrl(objectUrl);
      })
      .catch((e) => {
        if (active) setError((e as Error).message);
      });

    return () => {
      active = false;
      if (objectUrl) URL.revokeObjectURL(objectUrl);
    };
  }, [available, data.diagnoses, diagnosisId, kind]);

  return (
    <div className="report-image-derived">
      <div className="report-image-label">
        <strong>{title}</strong>
        {diagnosisId ? (
          <small className="mono">AI #{diagnosisId}</small>
        ) : null}
      </div>

      {!diagnosisId || !available ? (
        <div className="report-image-placeholder">Chưa có ảnh</div>
      ) : error ? (
        <div className="report-image-placeholder" title={error}>
          Không tải được ảnh
        </div>
      ) : !url ? (
        <div className="report-image-placeholder">Đang tải…</div>
      ) : (
        <img src={url} alt={alt} className="report-clinical-image" />
      )}
    </div>
  );
}

function MetricCard({
  title,
  value,
  detail,
  abnormal,
}: {
  title: string;
  value: string;
  detail: string;
  abnormal?: boolean;
}) {
  return (
    <div className={`report-metric-card ${abnormal ? "is-abnormal" : ""}`}>
      <small>{title}</small>
      <strong>{value}</strong>
      <span className="faint">{detail}</span>
    </div>
  );
}

function ReportSection({ title, children }: { title: string; children?: any }) {
  return (
    <section className="report-section">
      <h2>{title}</h2>
      {children}
    </section>
  );
}

function Info({ k, v }: { k: string; v: any }) {
  return (
    <div className="detail-item">
      <small>{k}</small>
      <strong>{v ?? "—"}</strong>
    </div>
  );
}
