import { useState } from "react";
import { useNavigate } from "react-router-dom";
import { useData } from "@/contexts/DataContext";
import { useAsync, useDebounce } from "@/lib/hooks";
import { useToast } from "@/contexts/ToastContext";
import {
  PageHeader,
  Panel,
  Field,
  Button,
  DataTable,
  LoadState,
  StatusBadge,
  ConfirmDialog,
  Icon,
} from "@/components/ui";
import { clinicToday, fmtDate } from "@/lib/format";
import { visitStatuses, label } from "@/lib/enums";
import type {
  PatientListItemDto,
  OnDutyDoctorDto,
  DoctorShiftDto,
  DoctorDto,
  VisitDto,
} from "@/types/api";

/* ======================================================================== */
/*  TRANG 1 — Tạo lượt khám + chọn bác sĩ đang trực                         */
/* ======================================================================== */
const SHIFT_LABELS: Record<number, string> = {
  1: "Sáng",
  2: "Chiều",
  3: "Đêm",
};
export function ReceptionNewVisitPage() {
  const data = useData();
  const toast = useToast();
  const navigate = useNavigate();

  // Bước 1: tìm và chọn bệnh nhân.
  const [q, setQ] = useState("");
  const dq = useDebounce(q);
  const [patient, setPatient] = useState<PatientListItemDto | null>(null);

  const results = useAsync(
    () =>
      dq.trim().length >= 2
        ? data.patients.list({ q: dq, page: 1, pageSize: 8, sort: "name" })
        : Promise.resolve(null),
    [dq],
  );

  // Bước 2: chọn bác sĩ đang trực hôm nay.
  // Tìm kiếm dùng chung endpoint on-duty, chỉ truyền thêm tham số q — không có
  // API riêng, và cũng không nên lọc ở client vì danh sách đã bị backend giới
  // hạn theo đúng ca trực hiện tại.
  const today = clinicToday();
  const [doctorQ, setDoctorQ] = useState("");
  const dDoctorQ = useDebounce(doctorQ);
  const onDuty = useAsync(
    () => data.reception.onDuty(today, undefined, dDoctorQ.trim() || undefined),
    [dDoctorQ],
  );
  const [doctorId, setDoctorId] = useState<number | null>(null);

  const [busy, setBusy] = useState(false);

  const submit = async () => {
    if (!patient) {
      toast.push("Hãy chọn bệnh nhân trước.", "error");
      return;
    }
    if (!doctorId) {
      toast.push("Hãy chọn bác sĩ phụ trách.", "error");
      return;
    }
    setBusy(true);
    try {
      const v = await data.visits.create({ patientId: patient.id, doctorId });
      toast.push("Đã tạo lượt khám.", "success");
      navigate(`/reception/visits`);
    } catch (err) {
      toast.push((err as Error).message, "error");
    } finally {
      setBusy(false);
    }
  };

  return (
    <>
      <PageHeader
        title="Tạo lượt khám"
        subtitle="Chọn bệnh nhân và bác sĩ đang trực hôm nay để mở lượt khám mới."
      />

      {/* Bước 1: chọn bệnh nhân */}
      <Panel title="1. Chọn bệnh nhân">
        {patient ? (
          <div className="selected-row">
            <div>
              <b>{patient.fullName}</b> · {patient.code} · {patient.phone}
            </div>
            <Button onClick={() => setPatient(null)}>Đổi</Button>
          </div>
        ) : (
          <>
            <Field labelText="Tìm theo tên, mã hoặc số điện thoại">
              <input
                value={q}
                onChange={(e) => setQ(e.target.value)}
                placeholder="Nhập ít nhất 2 ký tự…"
              />
            </Field>
            {results.data && (
              <DataTable headers={["Mã", "Họ tên", "Điện thoại", ""]}>
                {results.data.items.map((p: PatientListItemDto) => (
                  <tr key={p.id}>
                    <td>{p.code}</td>
                    <td>{p.fullName}</td>
                    <td>{p.phone}</td>
                    <td>
                      <Button kind="primary" onClick={() => setPatient(p)}>
                        Chọn
                      </Button>
                    </td>
                  </tr>
                ))}
              </DataTable>
            )}
            <p className="hint">
              Chưa có hồ sơ?{" "}
              <a onClick={() => navigate("/reception/patients/new")}>
                Tạo hồ sơ bệnh nhân
              </a>
            </p>
          </>
        )}
      </Panel>

      {/* Bước 2: chọn bác sĩ trực */}
      <Panel title="2. Chọn bác sĩ đang trực hôm nay">
        <Field labelText="Tìm bác sĩ">
          <input
            value={doctorQ}
            onChange={(e: any) => setDoctorQ(e.target.value)}
            placeholder="Họ tên hoặc số chứng chỉ hành nghề"
          />
        </Field>

        <LoadState
          loading={onDuty.loading}
          error={onDuty.error}
          empty={!onDuty.data?.doctors.length}
          emptyText={
            dDoctorQ.trim()
              ? "Không có bác sĩ trực nào khớp từ khoá này. Xoá ô tìm để xem toàn bộ ca trực hiện tại."
              : "Hôm nay không có bác sĩ nào trong lịch trực. Kiểm tra lại Lịch ca trực."
          }
        >
          {onDuty.data && (
            <>
              <p className="hint">
                {onDuty.data.dayLabel}
                {onDuty.data.currentShift
                  ? ` · Ca hiện tại: ${SHIFT_LABELS[onDuty.data.currentShift] ?? "Không xác định"}`
                  : ""}
              </p>
              <div className="duty-grid">
                {onDuty.data.doctors.map((d: OnDutyDoctorDto) => (
                  <button
                    key={d.doctorId}
                    className={`duty-card ${doctorId === d.doctorId ? "selected" : ""}`}
                    onClick={() => setDoctorId(d.doctorId)}
                    type="button"
                  >
                    <div className="duty-name">{d.doctorName}</div>
                    <div className="duty-meta">
                      {d.shiftLabel}
                      {d.licenseNo ? ` · ${d.licenseNo}` : ""}
                    </div>
                    <StatusBadge
                      text={`${d.openVisitCount} lượt đang mở`}
                      kind={d.openVisitCount === 0 ? "ok" : "watch"}
                    />
                  </button>
                ))}
              </div>
            </>
          )}
        </LoadState>
      </Panel>

      <div className="form-actions">
        <Button
          kind="primary"
          busy={busy}
          disabled={!patient || !doctorId}
          onClick={submit}
        >
          <Icon name="plus" />
          Tạo lượt khám
        </Button>
      </div>
    </>
  );
}

/* ======================================================================== */
/*  TRANG 2 — Quản lý lịch ca trực bác sĩ                                   */
/*                                                                          */
/*  Thuộc quyền Admin (trước đây là Lễ tân). Lễ tân vẫn dùng danh sách bác   */
/*  sĩ đang trực ở trang tạo lượt khám, nhưng không còn tự sửa lịch trực.    */
/* ======================================================================== */

const DAYS = [
  { value: 1, label: "Thứ 2" },
  { value: 2, label: "Thứ 3" },
  { value: 3, label: "Thứ 4" },
  { value: 4, label: "Thứ 5" },
  { value: 5, label: "Thứ 6" },
  { value: 6, label: "Thứ 7" },
  { value: 0, label: "Chủ nhật" },
];

export function ReceptionShiftsPage() {
  const data = useData();
  const toast = useToast();

  // Bác sĩ lọc ở SERVER (BE /api/reception/shifts nhận doctorId).
  const [doctorFilter, setDoctorFilter] = useState("");
  // Thứ, ca và trạng thái lọc ở CLIENT: lịch trực cố định theo tuần nên tập dữ
  // liệu tối đa chỉ là (số bác sĩ × 7 thứ × 3 ca), tải hết một lần rẻ hơn là
  // thêm tham số backend và gọi lại API mỗi lần đổi ô lọc.
  const [dayFilter, setDayFilter] = useState("");
  const [shiftFilter, setShiftFilter] = useState("");
  const [activeFilter, setActiveFilter] = useState("");

  const doctors = useAsync(() => data.users.doctors(), []);
  const shifts = useAsync(
    () => data.reception.listShifts(doctorFilter ? Number(doctorFilter) : undefined),
    [doctorFilter],
  );
  const [adding, setAdding] = useState(false);

  const rows = (shifts.data || []).filter((s: DoctorShiftDto) => {
    if (dayFilter !== "" && s.dayOfWeek !== Number(dayFilter)) return false;
    if (shiftFilter !== "" && s.shift !== Number(shiftFilter)) return false;
    if (activeFilter === "true" && !s.isActive) return false;
    if (activeFilter === "false" && s.isActive) return false;
    return true;
  });

  const hasFilter =
    !!doctorFilter || dayFilter !== "" || shiftFilter !== "" || activeFilter !== "";

  const clearFilters = () => {
    setDoctorFilter("");
    setDayFilter("");
    setShiftFilter("");
    setActiveFilter("");
  };

  const toggleActive = async (s: DoctorShiftDto) => {
    try {
      await data.reception.setShiftActive(s.id, !s.isActive, s.rowVersion);
      shifts.reload();
    } catch (err) {
      toast.push((err as Error).message, "error");
    }
  };

  const remove = async (s: DoctorShiftDto) => {
    try {
      await data.reception.deleteShift(s.id, s.rowVersion);
      toast.push("Đã xoá ca trực.", "success");
      shifts.reload();
    } catch (err) {
      toast.push((err as Error).message, "error");
    }
  };

  return (
    <>
      <PageHeader
        title="Lịch ca trực"
        subtitle="Ca trực cố định theo tuần. Bác sĩ trực ca nào, thứ mấy — dùng để gán khi tạo lượt khám."
        actions={
          <Button kind="primary" onClick={() => setAdding(true)}>
            <Icon name="plus" />
            Thêm ca trực
          </Button>
        }
      />

      <Panel>
        <div className="toolbar">
          <Field labelText="Bác sĩ" className="inline">
            <select
              value={doctorFilter}
              onChange={(e) => setDoctorFilter(e.target.value)}
            >
              <option value="">Tất cả bác sĩ</option>
              {(doctors.data || []).map((d: DoctorDto) => (
                <option value={d.id} key={d.id}>
                  {d.fullName}
                </option>
              ))}
            </select>
          </Field>
          <Field labelText="Thứ" className="inline">
            <select
              value={dayFilter}
              onChange={(e) => setDayFilter(e.target.value)}
            >
              <option value="">Cả tuần</option>
              {DAYS.map((d) => (
                <option value={d.value} key={d.value}>
                  {d.label}
                </option>
              ))}
            </select>
          </Field>
          <Field labelText="Ca" className="inline">
            <select
              value={shiftFilter}
              onChange={(e) => setShiftFilter(e.target.value)}
            >
              <option value="">Tất cả ca</option>
              {Object.entries(SHIFT_LABELS).map(([value, text]) => (
                <option value={value} key={value}>
                  {text}
                </option>
              ))}
            </select>
          </Field>
          <Field labelText="Trạng thái" className="inline">
            <select
              value={activeFilter}
              onChange={(e) => setActiveFilter(e.target.value)}
            >
              <option value="">Tất cả</option>
              <option value="true">Đang áp dụng</option>
              <option value="false">Tạm nghỉ</option>
            </select>
          </Field>
          {hasFilter && (
            <Button size="sm" onClick={clearFilters}>
              Xoá lọc
            </Button>
          )}
        </div>

        <LoadState
          loading={shifts.loading}
          error={shifts.error}
          empty={!rows.length}
          emptyText={
            hasFilter
              ? "Không có ca trực nào khớp bộ lọc. Thử nới điều kiện hoặc xoá lọc."
              : "Chưa có ca trực nào. Nhấn 'Thêm ca trực' để bắt đầu."
          }
        >
          {rows.length > 0 && (
            <DataTable
              headers={["Bác sĩ", "Giấy phép", "Thứ", "Ca", "Trạng thái", ""]}
            >
              {rows.map((s: DoctorShiftDto) => (
                <tr key={s.id}>
                  <td>{s.doctorName}</td>
                  <td>{s.licenseNo || "—"}</td>
                  <td>{s.dayLabel}</td>
                  <td>{s.shiftLabel}</td>
                  <td>
                    <StatusBadge
                      text={s.isActive ? "Đang áp dụng" : "Tạm nghỉ"}
                      kind={s.isActive ? "ok" : ""}
                    />
                  </td>
                  <td>
                    <div className="actions">
                      <Button size="sm" onClick={() => toggleActive(s)}>
                        {s.isActive ? "Tạm nghỉ" : "Bật lại"}
                      </Button>
                      <Button size="sm" kind="danger" onClick={() => remove(s)}>
                        Xoá
                      </Button>
                    </div>
                  </td>
                </tr>
              ))}
            </DataTable>
          )}
        </LoadState>
      </Panel>

      {adding && (
        <AddShiftModal
          doctors={doctors.data || []}
          onClose={() => setAdding(false)}
          onSaved={() => {
            setAdding(false);
            shifts.reload();
          }}
        />
      )}
    </>
  );
}

function AddShiftModal({
  doctors,
  onClose,
  onSaved,
}: {
  doctors: DoctorDto[];
  onClose: () => void;
  onSaved: () => void;
}) {
  const data = useData();
  const toast = useToast();
  const [doctorId, setDoctorId] = useState<number | "">("");
  const [shift, setShift] = useState(1);
  const [days, setDays] = useState<number[]>([]);
  const [busy, setBusy] = useState(false);

  const toggleDay = (d: number) =>
    setDays((prev) =>
      prev.includes(d) ? prev.filter((x) => x !== d) : [...prev, d],
    );

  const save = async () => {
    if (!doctorId) {
      toast.push("Chọn bác sĩ.", "error");
      return;
    }
    if (!days.length) {
      toast.push("Chọn ít nhất một ngày.", "error");
      return;
    }
    setBusy(true);
    try {
      // Dùng batch: thêm nhiều thứ cùng lúc cho một bác sĩ + một ca.
      await data.reception.createShiftsBatch({
        doctorId: Number(doctorId),
        daysOfWeek: days,
        shift,
      });
      toast.push("Đã thêm ca trực.", "success");
      onSaved();
    } catch (err) {
      toast.push((err as Error).message, "error");
    } finally {
      setBusy(false);
    }
  };

  return (
    <div className="modal-backdrop" onClick={onClose}>
      <div className="modal" onClick={(e) => e.stopPropagation()}>
        <h3>Thêm ca trực</h3>

        <Field labelText="Bác sĩ" required>
          <select
            value={String(doctorId)}
            onChange={(e) =>
              setDoctorId(e.target.value ? Number(e.target.value) : "")
            }
          >
            <option value="">— Chọn bác sĩ —</option>
            {doctors.map((d) => (
              <option key={d.id} value={d.id}>
                {d.fullName}
                {d.licenseNo ? ` (${d.licenseNo})` : ""}
              </option>
            ))}
          </select>
        </Field>

        <Field labelText="Ca" required>
          <select value={String(shift)} onChange={(e) => setShift(Number(e.target.value))}>
            <option value="1">Ca sáng</option>
            <option value="2">Ca chiều</option>
            <option value="3">Ca đêm</option>
          </select>
        </Field>

        <Field labelText="Các thứ trong tuần" required>
          <div className="day-picker">
            {DAYS.map((d) => (
              <button
                key={d.value}
                type="button"
                className={`day-chip ${days.includes(d.value) ? "selected" : ""}`}
                onClick={() => toggleDay(d.value)}
              >
                {d.label}
              </button>
            ))}
          </div>
        </Field>

        <div className="modal-actions">
          <Button onClick={onClose}>Hủy</Button>
          <Button kind="primary" busy={busy} onClick={save}>
            Lưu
          </Button>
        </div>
      </div>
    </div>
  );
}


/* ======================================================================== */
/*  TRANG 3 — Danh sách toàn bộ lượt khám tại quầy tiếp đón                 */
/* ======================================================================== */

export function ReceptionVisitsPage() {
  const data = useData();
  const toast = useToast();
  const navigate = useNavigate();
  const [from, setFrom] = useState("");
  const [to, setTo] = useState("");
  const [status, setStatus] = useState("");
  // BE /api/visits nhận doctorId nhưng FE chưa từng gửi; lễ tân không lọc được
  // theo bác sĩ phụ trách dù đó là câu hỏi hay gặp nhất ở quầy.
  const [doctorId, setDoctorId] = useState("");
  const [page, setPage] = useState(1);
  const [voiding, setVoiding] = useState<VisitDto | null>(null);

  const doctors = useAsync(() => data.users.doctors(), []);

  const list = useAsync(
    () =>
      data.visits.list({
        from: from || undefined,
        to: to || undefined,
        status: status || undefined,
        doctorId: doctorId || undefined,
        page,
        pageSize: 25,
      }),
    [from, to, status, doctorId, page],
  );

  const setToday = () => {
    const today = clinicToday();
    setFrom(today);
    setTo(today);
    setPage(1);
  };

  const clearFilters = () => {
    setFrom("");
    setTo("");
    setStatus("");
    setDoctorId("");
    setPage(1);
  };

  const voidVisit = async (reason: string) => {
    if (!voiding) return;
    try {
      await data.visits.void(voiding.id, reason, voiding.rowVersion);
      toast.push("Đã thu hồi lượt khám.", "success");
      setVoiding(null);
      list.reload();
    } catch (err) {
      toast.push((err as Error).message, "error");
    }
  };

  const totalPages = list.data
    ? Math.max(1, Math.ceil(list.data.totalItems / list.data.pageSize))
    : 1;

  return (
    <>
      <PageHeader
        title="Danh sách lượt khám"
        subtitle="Toàn bộ lượt khám tại quầy tiếp đón. Có thể lọc theo ngày và trạng thái."
        actions={
          <Button kind="primary" onClick={() => navigate("/reception/visits/new")}>
            <Icon name="plus" />
            Tạo lượt khám
          </Button>
        }
      />

      <Panel>
        <div className="toolbar">
          <Field labelText="Từ ngày" className="inline">
            <input
              type="date"
              value={from}
              onChange={(e) => {
                setFrom(e.target.value);
                setPage(1);
              }}
            />
          </Field>
          <Field labelText="Đến ngày" className="inline">
            <input
              type="date"
              value={to}
              onChange={(e) => {
                setTo(e.target.value);
                setPage(1);
              }}
            />
          </Field>
          <Field labelText="Trạng thái" className="inline">
            <select
              value={status}
              onChange={(e) => {
                setStatus(e.target.value);
                setPage(1);
              }}
            >
              <option value="">Tất cả</option>
              <option value="0">Đang khám</option>
              <option value="1">Đã đóng</option>
            </select>
          </Field>
          <Field labelText="Bác sĩ phụ trách" className="inline">
            <select
              value={doctorId}
              onChange={(e) => {
                setDoctorId(e.target.value);
                setPage(1);
              }}
            >
              <option value="">Tất cả bác sĩ</option>
              {(doctors.data || []).map((d: DoctorDto) => (
                <option value={d.id} key={d.id}>
                  {d.fullName}
                </option>
              ))}
            </select>
          </Field>
          <Button onClick={setToday}>Hôm nay</Button>
          <Button onClick={clearFilters}>Xóa lọc</Button>
        </div>

        <LoadState
          loading={list.loading}
          error={list.error}
          empty={!list.data?.items.length}
          emptyText="Chưa có lượt khám phù hợp."
          onRetry={list.reload}
        >
          {list.data && (
            <>
              <DataTable
                headers={[
                  "Mã lượt",
                  "Ngày tiếp nhận",
                  "Mã BN",
                  "Bệnh nhân",
                  "Bác sĩ",
                  "Ảnh",
                  "Trạng thái",
                  "Thao tác",
                ]}
              >
                {list.data.items.map((v) => (
                  <tr key={v.id}>
                    <td className="mono">#{v.id}</td>
                    <td className="mono">{fmtDate(v.visitDate, true)}</td>
                    <td>{v.patientCode}</td>
                    <td>{v.patientName}</td>
                    <td>{v.doctorName || "Chưa phân công"}</td>
                    <td className="mono">{v.imageCount}</td>
                    <td>
                      <StatusBadge
                        text={label(visitStatuses, v.status)}
                        kind={v.status === 1 ? "ok" : "watch"}
                      />
                    </td>
                    <td>
                      <div className="actions">
                        <Button onClick={() => navigate(`/patients/${v.patientId}?tab=visits`)}>
                          Mở hồ sơ
                        </Button>
                        {v.status === 0 && (
                          <Button kind="danger" onClick={() => setVoiding(v)}>
                            Thu hồi
                          </Button>
                        )}
                      </div>
                    </td>
                  </tr>
                ))}
              </DataTable>

              <div className="pager">
                <Button disabled={page <= 1} onClick={() => setPage((x) => x - 1)}>
                  Trang trước
                </Button>
                <span>
                  Trang {page}/{totalPages} · {list.data.totalItems} lượt
                </span>
                <Button
                  disabled={page >= totalPages}
                  onClick={() => setPage((x) => x + 1)}
                >
                  Trang sau
                </Button>
              </div>
            </>
          )}
        </LoadState>
      </Panel>

      {voiding && (
        <ConfirmDialog
          title="Thu hồi lượt khám"
          message={`Thu hồi lượt khám #${voiding.id} của ${voiding.patientName}. Lễ tân chỉ được thu hồi lượt chưa có dữ liệu lâm sàng.`}
          requireReason
          danger
          confirmText="Thu hồi"
          onClose={() => setVoiding(null)}
          onConfirm={voidVisit}
        />
      )}
    </>
  );
}
