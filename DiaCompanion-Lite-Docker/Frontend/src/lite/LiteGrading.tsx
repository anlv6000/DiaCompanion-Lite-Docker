import { useState } from "react";
import { gradingApi } from "@/api/services";
import { useToast } from "@/contexts/ToastContext";
import { useAsync } from "@/lib/hooks";
import { LoadState, PageHeader, Panel } from "@/components/ui";
import { BlindForm, BlindWorkspace } from "@/lite/BlindReview";
import type { BlindGradeRequest, GradingProgressDto } from "@/types/api";
import "@/lite/lite.css";

/* Trang của người chấm độc lập (vai trò Grader).
   Ảnh theo thứ tự xáo trộn, khung xem phóng to/kéo thả như trang AI, kèm thông tin lâm sàng ẨN DANH
   (1.3.1: chấm trong cùng điều kiện thông tin với bác sĩ vòng 1; không mã nghiên cứu, ngày khám, ghi chú).
   Không kết quả AI, không phân độ của người khác. Mỗi lần lưu là cố định, không sửa được. */
export function LiteGradingPage() {
  const toast = useToast();
  const [round, setRound] = useState<2 | 3>(2);
  const progress = useAsync(() => gradingApi.progress(), []);
  const queue = useAsync(() => gradingApi.queue(round), [round]);
  const current = queue.data?.items[0] ?? null;
  const [busy, setBusy] = useState(false);

  const submit = async (req: BlindGradeRequest) => {
    if (!current) return;
    setBusy(true);
    try {
      await gradingApi.submit(round, current.imageId, req);
      toast.push("Đã lưu phân độ.", "success");
    } catch (e) {
      toast.push((e as Error).message, "error");
    } finally {
      setBusy(false);
      queue.reload();
      progress.reload();
    }
  };

  return (
    <>
      <PageHeader
        title="Chấm ảnh độc lập"
        subtitle="Chấm theo thang ICDR, dựa trên ảnh và thông tin lâm sàng ẩn danh. Không có kết quả AI hay phân độ của người khác. Phân độ đã lưu không sửa được."
      />
      {progress.data && <ProgressPanel p={progress.data} />}

      <div className="tabs" role="tablist">
        {([
          [2, "Vòng 2 — chấm độc lập"],
          [3, "Vòng 3 — phân xử"],
        ] as const).map(([r, txt]) => (
          <button key={r} role="tab" aria-selected={round === r} className={`tab ${round === r ? "on" : ""}`} onClick={() => setRound(r)}>
            {txt}
          </button>
        ))}
      </div>

      {!queue.data ? (
        <LoadState loading={queue.loading} error={queue.error} onRetry={queue.reload}>{null}</LoadState>
      ) : !current ? (
        <Panel>
          <div className="empty">
            <b>Không còn ảnh chờ chấm ở vòng {round}</b>
            {round === 3 ? "Vòng 3 chỉ có ảnh khi vòng 1 và 2 bất đồng." : "Ảnh mới sẽ xuất hiện khi bác sĩ khám chấm xong vòng 1."}
          </div>
        </Panel>
      ) : (
        <BlindWorkspace
          key={`${round}-${current.imageId}`}
          source={{ kind: "grader", imageId: current.imageId }}
          context={null}
          viewerTitle={`Ảnh ${current.position} — còn ${queue.data.remaining} ảnh`}
          form={
            <BlindForm
              busy={busy}
              label="Lưu & sang ảnh tiếp"
              hint={round === 3 ? "phân xử độc lập, không sửa được sau khi lưu" : "chấm độc lập, không sửa được sau khi lưu"}
              onSubmit={submit}
            />
          }
        />
      )}
    </>
  );
}

export function ProgressPanel({ p }: { p: GradingProgressDto }) {
  return (
    <Panel title="Tiến độ chấm mù (toàn bộ nghiên cứu)">
      <div className="lite-kpis">
        <div><b>{p.round1}</b><span>ảnh đã chấm vòng 1</span></div>
        <div><b>{p.round1Ungradable}</b><span>không chấm được (vòng 1)</span></div>
        <div><b>{p.round2Done}</b><span>đã chấm vòng 2</span></div>
        <div><b>{p.round2Pending}</b><span>chờ chấm vòng 2</span></div>
        <div><b>{p.round3Needed}</b><span>bất đồng cần phân xử</span></div>
        <div><b>{p.round3Done}</b><span>đã phân xử</span></div>
        {(p.round2InPackages ?? 0) + (p.round3InPackages ?? 0) > 0 && (
          <div><b>{(p.round2InPackages ?? 0) + (p.round3InPackages ?? 0)}</b><span>đang chấm ở máy khác (gói)</span></div>
        )}
      </div>
    </Panel>
  );
}
