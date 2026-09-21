import { useState } from "react";
import { useNavigate, useSearchParams } from "react-router-dom";
import { useData } from "@/contexts/DataContext";
import { useAuth } from "@/contexts/AuthContext";
import { useToast } from "@/contexts/ToastContext";
import { useAsync } from "@/lib/hooks";
import { can } from "@/lib/permissions";
import { hasRole } from "@/lib/roles";
import { ProtectedImage } from "@/components/ProtectedImage";
import {
  PageHeader,
  Panel,
  Tabs,
  LoadState,
  Button,
  DataTable,
  StatusBadge,
  GradeBadge,
  EyeBadge,
  Field,
  Modal,
  ConfirmDialog,
  Icon,
  Meter,
  ActionLink,
} from "@/components/ui";
import {
  genders,
  diabetesTypes,
  visitStatuses,
  referralTypes,
  qualityStatuses,
  metricTypes,
  metricContexts,
  label,
} from "@/lib/enums";
import { fmtDate, num } from "@/lib/format";
import type {
  PatientDetailDto,
  VisitDto,
  FundusImageDto,
  TempCredentialResponse,
  DoctorDto,
  PrescriptionDto,
} from "@/types/api";

const TABS = [
  { key: "profile", label: "Hồ sơ bệnh án" },
  { key: "visits", label: "Lượt khám" },
  { key: "images", label: "Ảnh & AI" },
  { key: "prescriptions", label: "Đơn thuốc" },
  { key: "monitoring", label: "Chỉ số" },
];

export function PatientDetailPage({ id }: { id: number }) {
  const data = useData();
  const navigate = useNavigate();
  const { user } = useAuth();
  const [sp, setSp] = useSearchParams();
  const tab = sp.get("tab") || "profile";
  const patient = useAsync(() => data.patients.get(id), [id]);
  const isReceptionist = hasRole(user, "Receptionist") && !hasRole(user, "Doctor");
  const visibleTabs = isReceptionist
    ? TABS.filter((item) => item.key === "profile" || item.key === "visits")
    : TABS;
  const activeTab = visibleTabs.some((item) => item.key === tab)
    ? tab
    : "profile";
  const setTab = (t: string) => setSp({ tab: t }, { replace: true });

  return (
    <LoadState
      loading={patient.loading}
      error={patient.error}
      onRetry={patient.reload}
      empty={!patient.data}
    >
      {patient.data && (
        <>
          <PageHeader
            title={`${patient.data.code} · ${patient.data.fullName}`}
            subtitle={`Hồ sơ tạo ngày ${fmtDate(patient.data.createdAt)} · ${patient.data.visitCount} lượt khám`}
            actions={
              <>
                <Button onClick={() => navigate(`/patients/${id}/edit`)}>
                  <Icon name="edit" />
                  Sửa hồ sơ
                </Button>
                {hasRole(user, "Doctor") && (
                  <Button onClick={() => navigate(`/progression/${id}`)}>
                    <Icon name="chart" />
                    Diễn tiến
                  </Button>
                )}
              </>
            }
          />
          <Tabs items={visibleTabs} active={activeTab} onChange={setTab} />
          <div style={{ marginTop: 12 }}>
            {activeTab === "profile" && <ProfileTab patient={patient.data} />}
            {activeTab === "visits" && <VisitsTab patientId={id} />}
            {activeTab === "images" && hasRole(user, "Doctor") && <ImagesTab patientId={id} />}
            {activeTab === "prescriptions" && hasRole(user, "Doctor") && (
              <PrescriptionsTab patientId={id} />
            )}
            {activeTab === "monitoring" && hasRole(user, "Doctor") && (
              <MonitoringTab patientId={id} />
            )}
          </div>
        </>
      )}
    </LoadState>
  );
}

function Info({
  k,
  v,
  mono = false,
}: {
  k: string;
  v: React.ReactNode;
  mono?: boolean;
}) {
  return (
    <div className="detail-item">
      <small>{k}</small>
      <strong className={mono ? "mono" : ""}>{v}</strong>
    </div>
  );
}

function ProfileTab({ patient }: { patient: PatientDetailDto }) {
  const data = useData();
  const { user } = useAuth();
  const toast = useToast();
  const navigate = useNavigate();
  const [cred, setCred] = useState<TempCredentialResponse | null>(null);
  const [voiding, setVoiding] = useState(false);

  const reissue = async () => setCred(await data.patients.reissue(patient.id));
 const doVoid = async (reason: string) => {
  try {
    await data.patients.void(
      patient.id,
      reason,
      patient.rowVersion,
    );

    toast.push(
      "Đã thu hồi hồ sơ bệnh nhân.",
      "success",
    );

    setVoiding(false);
    navigate("/patients");
  } catch (err) {
    const message =
      err instanceof Error
        ? err.message
        : "Không thể thu hồi hồ sơ bệnh nhân.";

    toast.push(message, "error");
  }
};

  return (
    <>
      <div className="grid2">
        <Panel title="Thông tin hành chính">
          <div className="detail-grid">
            <Info k="Mã bệnh nhân" v={patient.code} mono />
            <Info k="Họ tên" v={patient.fullName} />
            <Info k="Ngày sinh" v={fmtDate(patient.dateOfBirth)} />
            <Info k="Tuổi" v={patient.age} />
            <Info k="Giới tính" v={label(genders, patient.gender)} />
            <Info k="Số điện thoại" v={patient.phone} mono />
            <Info k="Địa chỉ" v={patient.address || "—"} />
            <Info
              k="Tài khoản"
              v={patient.hasAccount ? "Đã cấp" : "Chưa cấp"}
            />
          </div>
        </Panel>
        <Panel title="Tiền sử tiểu đường">
          <div className="detail-grid">
            <Info
              k="Loại tiểu đường"
              v={label(diabetesTypes, patient.diabetesType)}
            />
            <Info
              k="Thời gian mắc"
              v={
                patient.diabetesDurationYears == null
                  ? "—"
                  : `${patient.diabetesDurationYears} năm`
              }
            />
            <Info
              k="HbA1c nền"
              v={
                patient.baselineHbA1c == null
                  ? "—"
                  : `${patient.baselineHbA1c}%`
              }
            />
            <Info
              k="DR xác nhận gần nhất"
              v={<GradeBadge grade={patient.latestDrGrade} />}
            />
            <Info k="Bác sĩ phụ trách" v={patient.doctorInCharge || "—"} />
            <Info k="Ghi chú" v={patient.note || "—"} />
          </div>
        </Panel>
      </div>

      {can.reissuePatientCredential(user) && (
        <Panel
          title="Tài khoản bệnh nhân"
          action={<Button onClick={reissue}>Cấp lại mật khẩu</Button>}
        >
          <p className="muted">
            Mật khẩu tạm mới chỉ hiển thị một lần và buộc bệnh nhân đổi ở lần
            đăng nhập tiếp theo.
          </p>
        </Panel>
      )}

      {/* Thu hồi hồ sơ là thao tác lâm sàng dành cho Lễ tân. */}
      {can.voidPatient(user) && (
        <Panel title="Thu hồi hồ sơ" className="danger-zone">
          <p>
            Chỉ dùng khi hồ sơ được tạo nhầm hoặc bị trùng. Hành động này sẽ
            thu hồi các lượt khám, ảnh, kết quả AI và đơn thuốc liên quan.
          </p>
          <Button kind="danger" onClick={() => setVoiding(true)}>
            Thu hồi hồ sơ
          </Button>
        </Panel>
      )}

      {cred && (
        <Modal
          title="Thông tin đăng nhập mới"
          onClose={() => setCred(null)}
          footer={
            <Button kind="primary" onClick={() => setCred(null)}>
              Đã lưu
            </Button>
          }
        >
          <div className="credential">
            <div>
              Đăng nhập: <code>{cred.loginId}</code>
            </div>
            <div>
              Mật khẩu tạm: <code>{cred.tempPassword}</code>
            </div>
            <p>{cred.note}</p>
          </div>
        </Modal>
      )}
      {voiding && (
        <ConfirmDialog
          title="Thu hồi hồ sơ bệnh nhân"
          message={`Hồ sơ ${patient.code} — ${patient.fullName} sẽ bị thu hồi cùng chuỗi lâm sàng liên quan.`}
          requireReason
          danger
          confirmText="Thu hồi hồ sơ"
          onClose={() => setVoiding(false)}
          onConfirm={doVoid}
        />
      )}
    </>
  );
}

function VisitsTab({ patientId }: { patientId: number }) {
  const data = useData();
  const { user } = useAuth();
  const navigate = useNavigate();
  const list = useAsync(
    () => data.visits.list({ patientId, page: 1, pageSize: 100 }),
    [patientId],
  );

  return (
    <Panel
      title="Lượt khám"
      action={
        can.createVisit(user) ? (
          <ActionLink to="/reception/visits/new">
            <Button kind="primary">
              <Icon name="plus" />
              Tạo lượt khám
            </Button>
          </ActionLink>
        ) : undefined
      }
    >
      <LoadState
        loading={list.loading}
        error={list.error}
        empty={!list.data?.items.length}
        onRetry={list.reload}
      >
        <DataTable
          headers={[
            "Mã",
            "Ngày khám",
            "Bác sĩ",
            "Ảnh",
            "Chờ duyệt",
            "Kết luận",
            "Chuyển tuyến",
            "Trạng thái",
            "Thao tác",
          ]}
        >
          {list.data?.items.map((v) => (
            <tr key={v.id}>
              <td className="mono">#{v.id}</td>
              <td className="mono">{fmtDate(v.visitDate, true)}</td>
              <td>{v.doctorName || "Chưa phân công"}</td>
              <td className="mono">{v.imageCount}</td>
              <td className="mono">{v.pendingReviewCount}</td>
              <td className="wrap-text">{v.conclusion || "—"}</td>
              <td>
                {v.referral == null ? "—" : label(referralTypes, v.referral)}
              </td>
              <td>
                <StatusBadge
                  text={label(visitStatuses, v.status)}
                  kind={v.status === 1 ? "ok" : "watch"}
                />
              </td>
              <td>
                <Button onClick={() => navigate(`/reports/visit/${v.id}`)}>
                  Báo cáo
                </Button>
              </td>
            </tr>
          ))}
        </DataTable>
      </LoadState>
    </Panel>
  );
}

function CreateVisitModal({
  doctors,
  onClose,
  onSave,
}: {
  doctors: DoctorDto[];
  onClose: () => void;
  onSave: (id?: number) => void;
}) {
  const [doctor, setDoctor] = useState("");
  return (
    <Modal
      title="Tạo lượt khám"
      onClose={onClose}
      footer={
        <>
          <Button onClick={onClose}>Hủy</Button>
          <Button
            kind="primary"
            onClick={() => onSave(doctor ? Number(doctor) : undefined)}
          >
            Tạo lượt khám
          </Button>
        </>
      }
    >
      <Field labelText="Bác sĩ phụ trách">
        <select value={doctor} onChange={(e) => setDoctor(e.target.value)}>
          <option value="">Tự động theo người tạo</option>
          {doctors.map((d) => (
            <option key={d.id} value={d.id}>
              {d.fullName} · {d.licenseNo || "—"}
            </option>
          ))}
        </select>
      </Field>
    </Modal>
  );
}

function CloseVisitModal({
  visit,
  onClose,
  onSave,
}: {
  visit: VisitDto;
  onClose: () => void;
  onSave: (b: {
    conclusion: string;
    referral: number;
    recheckMonths: number | null;
  }) => void;
}) {
  const [conclusion, setConclusion] = useState("");
  const [referral, setReferral] = useState(0);
  const [months, setMonths] = useState("");
  return (
    <Modal
      title={`Đóng lượt khám #${visit.id}`}
      onClose={onClose}
      footer={
        <>
          <Button onClick={onClose}>Hủy</Button>
          <Button
            kind="primary"
            disabled={!conclusion.trim() || visit.pendingReviewCount > 0}
            onClick={() =>
              onSave({
                conclusion,
                referral,
                recheckMonths: months ? Number(months) : null,
              })
            }
          >
            Đóng lượt khám
          </Button>
        </>
      }
    >
      <Field labelText="Kết luận lâm sàng" required>
        <textarea
          value={conclusion}
          onChange={(e) => setConclusion(e.target.value)}
        />
      </Field>
      <div className="form-row">
        <Field labelText="Chuyển tuyến">
          <select
            value={referral}
            onChange={(e) => setReferral(Number(e.target.value))}
          >
            {referralTypes.map((x, i) => (
              <option key={i} value={i}>
                {x}
              </option>
            ))}
          </select>
        </Field>
        <Field
          labelText="Tái khám sau (tháng)"
          help="Để trống để hệ thống tự xác định theo mức DR đã xác nhận."
        >
          <input
            type="number"
            min="1"
            max="60"
            value={months}
            onChange={(e) => setMonths(e.target.value)}
          />
        </Field>
      </div>
      {visit.pendingReviewCount > 0 && (
        <div className="state error">
          Còn {visit.pendingReviewCount} kết quả AI chưa được bác sĩ xác nhận.
        </div>
      )}
    </Modal>
  );
}

function ImagesTab({ patientId }: { patientId: number }) {
  const data = useData();
  const { user } = useAuth();
  const toast = useToast();
  const navigate = useNavigate();
  const visits = useAsync(
    () => data.visits.list({ patientId, page: 1, pageSize: 100 }),
    [patientId],
  );
  const list = useAsync(async () => {
    const images = await data.images.list({ patientId });
    return Promise.all(
      images.map(async (img) => {
        try {
          const all = await data.diagnoses.byImage(img.id);
          return { ...img, latestDiagnosis: all[0] || null };
        } catch {
          return img;
        }
      }),
    );
  }, [patientId]);
  const visitStatus = new Map(
    (visits.data?.items || []).map((v) => [v.id, v.status] as const),
  );
  const [quality, setQuality] = useState<FundusImageDto | null>(null);
  const [voiding, setVoiding] = useState<FundusImageDto | null>(null);
  const [running, setRunning] = useState<number | null>(null);

  const run = async (img: FundusImageDto) => {
    setRunning(img.id);
    try {
      const d = await data.diagnoses.run(img.id);
      toast.push("Cả 3 model AI đã hoàn tất suy luận.", "success");
      navigate(`/fundus/${img.id}?diagnosis=${d.id}`);
    } catch (e) {
      toast.push((e as Error).message, "error");
    } finally {
      setRunning(null);
    }
  };
  const setQ = async (status: number, note: string) => {
    if (!quality) return;
    await data.images.quality(quality.id, status, note, quality.rowVersion);
    toast.push("Đã cập nhật chất lượng ảnh.", "success");
    setQuality(null);
    list.reload();
  };
  const voidImg = async (reason: string) => {
    if (!voiding) return;
    try {
      await data.images.void(voiding.id, reason, voiding.rowVersion);
      toast.push("Đã thu hồi ảnh và kết quả liên quan.", "success");
      setVoiding(null);
      list.reload();
    } catch (e) {
      toast.push((e as Error).message, "error");
    }
  };

  return (
    <>
      <Panel
        title="Ảnh đáy mắt"
        action={
          /* Nạp ảnh mới gắn với LƯỢT KHÁM — thực hiện ở trang lượt khám của
             bác sĩ. Ở trang bệnh nhân chỉ xem lại ảnh và kết quả AI đã có. */
          undefined
        }
      >
        <LoadState
          loading={list.loading}
          error={list.error}
          empty={!list.data?.length}
          onRetry={list.reload}
        >
          <DataTable
            headers={[
              "Ảnh",
              "Mắt",
              "Lượt khám",
              "Chất lượng",
              "AI gần nhất",
              "Nguy cơ",
              "Bất đồng",
              "Xác nhận",
              "Ngày nạp",
              "Thao tác",
            ]}
          >
            {list.data?.map((img) => {
              const closed = img.visitId != null && visitStatus.get(img.visitId) === 1;
              return (
              <tr key={img.id}>
                <td>
                  <ProtectedImage
                    imageId={img.id}
                    alt={`Ảnh đáy mắt #${img.id}`}
                    onClick={() =>
                      navigate(
                        img.latestDiagnosis
                          ? `/fundus/${img.id}?diagnosis=${img.latestDiagnosis.id}`
                          : `/fundus/${img.id}`,
                      )
                    }
                  />
                  <div className="mono" style={{ marginTop: 4 }}>#{img.id}</div>
                </td>
                <td>
                  <EyeBadge eye={img.eye} />
                </td>
                <td className="mono">
                  {img.visitId ? `#${img.visitId}` : "—"}
                </td>
                <td>
                  <StatusBadge
                    text={label(qualityStatuses, img.qualityStatus)}
                    kind={
                      img.qualityStatus === 1
                        ? "ok"
                        : img.qualityStatus === 2
                          ? "alert"
                          : "watch"
                    }
                  />
                </td>
                <td>
                  <GradeBadge grade={img.latestDiagnosis?.drGrade} />
                </td>
                <td className="mono">
                  {img.latestDiagnosis?.clinicalRiskScore == null
                    ? "—"
                    : img.latestDiagnosis.clinicalRiskScore}
                </td>
                <td>
                  {img.latestDiagnosis ? (
                    <Meter
                      value={img.latestDiagnosis.disagreement}
                      kind="defer"
                    />
                  ) : (
                    "—"
                  )}
                </td>
                <td>
                  {img.latestDiagnosis ? (
                    <StatusBadge
                      text={
                        img.latestDiagnosis.isConfirmed
                          ? "Đã xác nhận"
                          : "Chưa xác nhận"
                      }
                      kind={img.latestDiagnosis.isConfirmed ? "ok" : "defer"}
                    />
                  ) : (
                    "—"
                  )}
                </td>
                <td className="mono">{fmtDate(img.createdAt, true)}</td>
                <td>
                  <div className="actions">
                    {!closed && can.manageImages(user) && (
                      <Button onClick={() => setQuality(img)}>
                        Chất lượng
                      </Button>
                    )}
                    <Button
                      disabled={(!img.latestDiagnosis && (closed || img.qualityStatus !== 1)) || running === img.id}
                      busy={running === img.id}
                      onClick={() =>
                        img.latestDiagnosis
                          ? navigate(
                              `/fundus/${img.id}?diagnosis=${img.latestDiagnosis.id}`,
                            )
                          : run(img)
                      }
                    >
                      {img.latestDiagnosis ? "Xem" : closed ? "Chỉ đọc" : "Chạy 3 model AI"}
                    </Button>
                    {/* Void ảnh: Bác sĩ hoặc Admin. */}
                    {!closed && can.voidImage(user) && (
                      <Button kind="danger" onClick={() => setVoiding(img)}>
                        Thu hồi
                      </Button>
                    )}
                  </div>
                </td>
              </tr>
              );
            })}
          </DataTable>
        </LoadState>
      </Panel>
      {quality && (
        <QualityModal
          image={quality}
          onClose={() => setQuality(null)}
          onSave={setQ}
        />
      )}
      {voiding && (
        <ConfirmDialog
          title="Thu hồi ảnh đáy mắt"
          message={`Ảnh #${voiding.id} và mọi kết quả AI/review liên quan sẽ bị thu hồi.`}
          requireReason
          danger
          onClose={() => setVoiding(null)}
          onConfirm={voidImg}
        />
      )}
    </>
  );
}

function QualityModal({
  image,
  onClose,
  onSave,
}: {
  image: FundusImageDto;
  onClose: () => void;
  onSave: (s: number, n: string) => void;
}) {
  const [status, setStatus] = useState(image.qualityStatus);
  const [note, setNote] = useState(image.qualityNote || "");
  return (
    <Modal
      title={`Kiểm duyệt chất lượng ảnh #${image.id}`}
      onClose={onClose}
      footer={
        <>
          <Button onClick={onClose}>Hủy</Button>
          <Button
            kind="primary"
            disabled={status === 2 && !note.trim()}
            onClick={() => onSave(status, note)}
          >
            Lưu
          </Button>
        </>
      }
    >
      <Field labelText="Trạng thái">
        <select
          value={status}
          onChange={(e) => setStatus(Number(e.target.value))}
        >
          {qualityStatuses.map((x, i) => (
            <option key={i} value={i}>
              {x}
            </option>
          ))}
        </select>
      </Field>
      <Field labelText="Ghi chú" required={status === 2}>
        <textarea
          value={note}
          onChange={(e) => setNote(e.target.value)}
          placeholder="Bắt buộc khi ảnh không đạt"
        />
      </Field>
    </Modal>
  );
}

function PrescriptionsTab({ patientId }: { patientId: number }) {
  const data = useData();
  const toast = useToast();
  const [viewing, setViewing] = useState<PrescriptionDto | null>(null);
  const [viewingId, setViewingId] = useState<number | null>(null);
  const list = useAsync(
    () => data.prescriptions.list({ patientId, page: 1, pageSize: 100 }),
    [patientId],
  );
  const adherence = useAsync(
    () => data.prescriptions.adherence(patientId),
    [patientId],
  );

  const viewPrescription = async (id: number) => {
    setViewingId(id);
    try {
      setViewing(await data.prescriptions.get(id));
    } catch (err) {
      toast.push(
        err instanceof Error ? err.message : "Không thể tải chi tiết đơn thuốc.",
        "error",
      );
    } finally {
      setViewingId(null);
    }
  };

  return (
    <>
      <div className="grid2">
        <Panel title="Tuân thủ 30 ngày">
          <LoadState
            loading={adherence.loading}
            error={adherence.error}
            empty={!adherence.data}
            onRetry={adherence.reload}
          >
            {adherence.data && (
              <>
                <div className="stats compact">
                  <div className="stat">
                    <span>Đã tới hạn</span>
                    <b className="mono">{adherence.data.total}</b>
                  </div>
                  <div className="stat">
                    <span>Đã uống</span>
                    <b className="mono">{adherence.data.taken}</b>
                  </div>
                  <div className="stat">
                    <span>Bỏ lỡ</span>
                    <b className="mono">{adherence.data.missed}</b>
                  </div>
                  <div className="stat">
                    <span>Tuân thủ</span>
                    <b className="mono">{adherence.data.rate}%</b>
                  </div>
                </div>
                <p className="help">{adherence.data.note}</p>
              </>
            )}
          </LoadState>
        </Panel>
        <Panel title="Thông tin đơn thuốc">
          <p className="muted">
            Các đơn thuốc được bác sĩ kê trong từng lượt khám. Tại hồ sơ bệnh
            nhân, thông tin này được dùng để theo dõi và tra cứu lịch sử điều trị.
          </p>
        </Panel>
      </div>
      <Panel title="Lịch sử đơn thuốc">
        <LoadState
          loading={list.loading}
          error={list.error}
          empty={!list.data?.items.length}
          onRetry={list.reload}
        >
          <DataTable
            headers={[
              "Mã đơn",
              "Ngày kê",
              "Bác sĩ",
              "Lượt khám",
              "Thuốc",
              "Ghi chú",
              "Thao tác",
            ]}
          >
            {list.data?.items.map((p) => (
              <tr key={p.id}>
                <td className="mono">#{p.id}</td>
                <td className="mono">{fmtDate(p.issuedAt, true)}</td>
                <td>{p.doctorName}</td>
                <td className="mono">{p.visitId ? `#${p.visitId}` : "—"}</td>
                <td className="wrap-text">
                  {p.items
                    .map(
                      (x) =>
                        `${x.drugName} ${x.dose} · ${x.timesPerDay} lần/ngày · ${x.durationDays} ngày`,
                    )
                    .join("; ")}
                </td>
                <td className="wrap-text">{p.note || "—"}</td>
                <td>
                  <Button
                    busy={viewingId === p.id}
                    onClick={() => viewPrescription(p.id)}
                  >
                    Xem
                  </Button>
                </td>
              </tr>
            ))}
          </DataTable>
        </LoadState>
      </Panel>

      {viewing && (
        <Modal
          title={`Đơn thuốc #${viewing.id}`}
          onClose={() => setViewing(null)}
          footer={<Button onClick={() => setViewing(null)}>Đóng</Button>}
        >
          <div className="detail-grid">
            <Info k="Bác sĩ kê" v={viewing.doctorName || "—"} />
            <Info
              k="Lượt khám"
              v={viewing.visitId ? `#${viewing.visitId}` : "—"}
              mono
            />
            <Info k="Ngày kê" v={fmtDate(viewing.issuedAt, true)} mono />
            <Info k="Ghi chú" v={viewing.note || "—"} />
          </div>

          <div style={{ marginTop: 16 }}>
            <DataTable
              headers={[
                "Thuốc",
                "Liều dùng",
                "Số lần/ngày",
                "Số ngày",
                "Hướng dẫn",
              ]}
            >
              {viewing.items.map((item, index) => (
                <tr key={item.id ?? index}>
                  <td>{item.drugName}</td>
                  <td className="mono">{item.dose}</td>
                  <td className="mono">{item.timesPerDay}</td>
                  <td className="mono">{item.durationDays}</td>
                  <td className="wrap-text">
                    {item.instruction || item.instructions || "—"}
                  </td>
                </tr>
              ))}
            </DataTable>
          </div>
        </Modal>
      )}
    </>
  );
}

function MonitoringTab({ patientId }: { patientId: number }) {
  const data = useData();
  const [type, setType] = useState("");
  const metrics = useAsync(
    () => data.monitoring.metrics({ patientId, type, size: 100 }),
    [patientId, type],
  );
  const visits = useAsync(
    () => data.visits.list({ patientId, page: 1, pageSize: 100 }),
    [patientId],
  );
  const doctorByVisit = new Map(
    (visits.data?.items || []).map((v) => [v.id, v.doctorName] as const),
  );
  const summary = useAsync(
    () => data.monitoring.summary(patientId),
    [patientId],
  );
  const glucose = summary.data?.glucose;
  const hba1c = summary.data?.hbA1c;
  const bloodPressure = summary.data?.bloodPressure;

  return (
    <>
      <div className="stats">
        <div className="stat">
          <span>Glucose bất thường</span>
          <b className="mono">{glucose?.abnormalCount ?? "—"}</b>
        </div>
        <div className="stat">
          <span>Glucose trung bình</span>
          <b className="mono">{num(glucose?.average)}</b>
        </div>
        <div className="stat">
          <span>HbA1c gần nhất</span>
          <b className="mono">
            {hba1c?.latest?.value == null ? "—" : `${hba1c.latest.value}%`}
          </b>
        </div>
        <div className="stat">
          <span>HA tâm thu</span>
          <b className="mono">{num(bloodPressure?.latest?.systolic)}</b>
        </div>
        <div className="stat">
          <span>HA tâm trương</span>
          <b className="mono">{num(bloodPressure?.latest?.diastolic)}</b>
        </div>
      </div>
      <Panel
        title="Chỉ số sức khỏe"
        action={
          <select value={type} onChange={(e) => setType(e.target.value)}>
            <option value="">Tất cả loại</option>
            {metricTypes.map(
              (x, i) =>
                i > 0 && (
                  <option value={i} key={i}>
                    {x}
                  </option>
                ),
            )}
          </select>
        }
      >
        <p className="muted">
          Nguồn cho biết chỉ số do bệnh nhân tự ghi nhận hay được bác sĩ ghi
          trong một lượt khám.
        </p>
        <LoadState
          loading={metrics.loading}
          error={metrics.error}
          empty={!metrics.data?.items.length}
          onRetry={metrics.reload}
        >
          <DataTable
            headers={[
              "Ngày",
              "Nguồn",
              "Loại",
              "Giá trị",
              "Bối cảnh",
              "Đánh giá",
              "Ghi chú",
            ]}
          >
            {metrics.data?.items.map((m) => (
              <tr key={m.id}>
                <td className="mono">{fmtDate(m.recordedAtUtc, true)}</td>
                <td>
                  {m.visitId
                    ? doctorByVisit.get(m.visitId)
                      ? `BS. ${doctorByVisit.get(m.visitId)} · Lượt #${m.visitId}`
                      : `Bác sĩ · Lượt #${m.visitId}`
                    : "Bệnh nhân tự nhập"}
                </td>
                <td>{label(metricTypes, m.metricType)}</td>
                <td className="mono">
                  {m.value} {m.unit}
                </td>
                <td>{label(metricContexts, m.context)}</td>
                <td>
                  <StatusBadge
                    text={m.isAbnormal ? "Bất thường" : "Trong ngưỡng"}
                    kind={m.isAbnormal ? "alert" : "ok"}
                  />
                </td>
                <td className="wrap-text">{m.note || "—"}</td>
              </tr>
            ))}
          </DataTable>
        </LoadState>
      </Panel>
    </>
  );
}
