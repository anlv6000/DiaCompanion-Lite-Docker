import { useState } from "react";
import { useNavigate } from "react-router-dom";
import { useAuth } from "@/contexts/AuthContext";
import { useData } from "@/contexts/DataContext";
import { useToast } from "@/contexts/ToastContext";
import { useAsync } from "@/lib/hooks";
import { Button, LoadState, PageHeader, Panel } from "@/components/ui";
import { diabetesTypes, genders, label } from "@/lib/enums";
import type { PatientDetailDto } from "@/types/api";
import { VisitWorkspace } from "@/lite/LiteVisit";
import { PatientRecord } from "@/lite/LiteRecord";

/* Trang làm việc của MỘT bệnh nhân: hai tab
   - "Lượt hiện tại": lượt thu thập đang mở (LiteVisit)
   - "Hồ sơ bệnh án": toàn bộ hồ sơ gộp một trang (LiteRecord)
   Mặc định mở "Lượt hiện tại" nếu có lượt đang mở, ngược lại mở hồ sơ.
   Chỉ hiện khung chờ ở lần tải đầu; các lần tải lại giữ nguyên giao diện để
   không làm mất ảnh xem trước hay phân tích đang chạy ở mắt còn lại. */
export function LiteCasePage({ id }: { id: number }) {
  const data = useData();
  const { user } = useAuth();
  const toast = useToast();
  const navigate = useNavigate();
  const patient = useAsync(() => data.patients.get(id), [id]);
  const visits = useAsync(() => data.visits.list({ patientId: id, page: 1, pageSize: 100 }), [id]);
  const [starting, setStarting] = useState(false);
  const [tab, setTab] = useState<"visit" | "record" | null>(null);

  const items = visits.data?.items ?? [];
  const open = items.find((v) => v.status === 0);
  const current = tab ?? (visits.data && !open ? "record" : "visit");

  const reloadAll = () => {
    visits.reload();
    patient.reload();
  };

  const start = async () => {
    setStarting(true);
    try {
      await data.visits.create({ patientId: id, doctorId: user?.userId });
      toast.push("Đã bắt đầu lượt thu thập mới.", "success");
      setTab("visit");
      reloadAll();
    } catch (e) {
      toast.push((e as Error).message, "error");
    } finally {
      setStarting(false);
    }
  };

  if (!patient.data)
    return (
      <LoadState loading={patient.loading} error={patient.error} onRetry={patient.reload} empty={!patient.loading && !patient.error}>
        {null}
      </LoadState>
    );

  const p = patient.data;
  return (
    <>
      <div className="no-print">
        <PageHeader
          title={`${p.code} · ${p.fullName}`}
          subtitle={summary(p)}
          actions={
            <>
              <Button onClick={() => navigate("/cases")}>Danh sách</Button>
              <Button onClick={() => navigate(`/cases/${id}/edit`)}>Sửa hồ sơ</Button>
            </>
          }
        />
        <div className="tabs" role="tablist">
          {([
            ["visit", open ? "Lượt hiện tại · đang mở" : "Lượt hiện tại"],
            ["record", "Hồ sơ bệnh án"],
          ] as const).map(([key, txt]) => (
            <button
              key={key}
              role="tab"
              aria-selected={current === key}
              className={`tab ${current === key ? "on" : ""}`}
              onClick={() => setTab(key)}
            >
              {txt}
            </button>
          ))}
        </div>
      </div>

      {!visits.data ? (
        <LoadState loading={visits.loading} error={visits.error} onRetry={visits.reload}>{null}</LoadState>
      ) : current === "visit" ? (
        open ? (
          <VisitWorkspace
            key={open.id}
            patient={p}
            visit={open}
            onChanged={reloadAll}
            onClosed={() => {
              setTab("record");
              reloadAll();
            }}
          />
        ) : (
          <Panel title="Lượt thu thập">
            <p className="muted">
              Chưa có lượt đang mở. Mỗi lượt gồm chỉ số lâm sàng, ảnh hai mắt, kết quả AI và xác nhận của bác sĩ.
            </p>
            <Button kind="primary" busy={starting} onClick={start}>
              Bắt đầu lượt thu thập mới
            </Button>
          </Panel>
        )
      ) : (
        <PatientRecord patient={p} visits={items} onGoVisit={() => setTab("visit")} />
      )}
    </>
  );
}

function summary(p: PatientDetailDto) {
  return [
    label(genders, p.gender),
    `${p.age} tuổi`,
    label(diabetesTypes, p.diabetesType),
    p.diabetesDurationYears != null ? `mắc ${p.diabetesDurationYears} năm` : null,
    p.baselineHbA1c != null ? `HbA1c nền ${p.baselineHbA1c}%` : null,
  ]
    .filter(Boolean)
    .join(" · ");
}
