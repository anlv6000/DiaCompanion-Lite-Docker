import { useCallback, useEffect, useMemo, useRef, useState, type ReactNode } from "react";
import { useData } from "@/contexts/DataContext";
import { gradingApi } from "@/api/services";
import { Button } from "@/components/ui";
import { PanZoomViewer, RED_FREE } from "@/components/PanZoomViewer";
import { ClinicalContextPanel, ClinicalContextView } from "@/components/ClinicalContext";
import type { BlindGradeRequest, ImageClinicalContextDto } from "@/types/api";
import { DR_TEXT } from "@/lite/liteData";
import "@/lite/lite.css";

/* Màn "xem ảnh & chấm mù" dùng chung cho vòng 1 (bác sĩ khám, trước AI)
   và vòng 2/3 (người chấm độc lập): khung xem phóng to/kéo thả như trang AI,
   thông tin lâm sàng, rồi mới đến phần phân độ. */

/** Nguồn ảnh: tệp vừa chọn (chưa nạp), ảnh đã lưu (bác sĩ), hoặc ảnh trong hàng chấm (người chấm). */
export type ImageSource =
  | { kind: "file"; file: File }
  | { kind: "image"; imageId: number }
  | { kind: "grader"; imageId: number };

export function FundusViewer({ source, title = "Ảnh đáy mắt" }: { source: ImageSource; title?: string }) {
  const data = useData();
  const [url, setUrl] = useState("");
  const [error, setError] = useState("");
  const [loading, setLoading] = useState(true);
  const [redFree, setRedFree] = useState(false);
  const key = source.kind === "file" ? source.file : `${source.kind}-${source.imageId}`;

  useEffect(() => {
    let active = true;
    let objectUrl = "";
    setUrl("");
    setError("");
    setLoading(true);
    const load =
      source.kind === "file" ? Promise.resolve<Blob>(source.file)
      : source.kind === "image" ? data.images.content(source.imageId)
      : gradingApi.content(source.imageId);
    load
      .then((blob) => {
        if (!active) return;
        objectUrl = URL.createObjectURL(blob);
        setUrl(objectUrl);
      })
      .catch((e) => active && setError((e as Error).message))
      .finally(() => active && setLoading(false));
    return () => {
      active = false;
      if (objectUrl) URL.revokeObjectURL(objectUrl);
    };
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [key]);

  return (
    <PanZoomViewer
      className="lite-viewer"
      title={title}
      url={url}
      loading={loading}
      error={error}
      imageStyle={redFree ? RED_FREE : undefined}
      extraTool={
        <label className="checkbox ai-viewer-check">
          <input type="checkbox" checked={redFree} onChange={(e) => setRedFree(e.target.checked)} />
          Red-free
        </label>
      }
    />
  );
}

export function BlindForm({
  busy,
  onSubmit,
  label,
  hint = "phân độ của bác sĩ trước khi xem AI, không sửa được sau khi lưu",
}: {
  busy: boolean;
  onSubmit: (req: BlindGradeRequest) => void;
  label: string;
  hint?: string;
}) {
  const [grade, setGrade] = useState<number | null>(null);
  const [ungradable, setUngradable] = useState(false);
  const [note, setNote] = useState("");
  const ready = ungradable || grade != null;
  return (
    <div className="lite-blind">
      <div className="lite-blind-h">
        <b>Chấm mù</b> <span className="faint">— {hint}</span>
      </div>
      <div className="lite-grade-row" role="radiogroup" aria-label="Phân độ DR">
        {DR_TEXT.map((t, i) => (
          <button
            key={i}
            type="button"
            role="radio"
            aria-checked={!ungradable && grade === i}
            className={`lite-grade-btn ${!ungradable && grade === i ? "on" : ""}`}
            onClick={() => { setUngradable(false); setGrade(i); }}
          >
            <span className="mono">{i}</span> {t}
          </button>
        ))}
        <button
          type="button"
          role="radio"
          aria-checked={ungradable}
          className={`lite-grade-btn warn ${ungradable ? "on" : ""}`}
          onClick={() => { setUngradable(true); setGrade(null); }}
        >
          Không chấm được
        </button>
      </div>
      <input
        className="lite-blind-note"
        placeholder="Ghi chú (không bắt buộc)"
        maxLength={500}
        value={note}
        onChange={(e) => setNote(e.target.value)}
      />
      <div className="lite-actions">
        <Button
          kind="primary"
          busy={busy}
          disabled={!ready}
          onClick={() => onSubmit({ gradable: !ungradable, grade: ungradable ? null : grade, note: note.trim() || null })}
        >
          {ungradable ? "Lưu: không chấm được" : label}
        </Button>
      </div>
    </div>
  );
}

/** Bố cục: ảnh lớn bên trái; thông tin lâm sàng (chỉ vòng 1) + phân độ bên phải. */
export function BlindWorkspace({
  source,
  context,
  form,
  viewerTitle,
  imageOnly = false,
}: {
  source: ImageSource;
  /** Dữ liệu dựng sẵn (ảnh chưa nạp), hoặc null để tải theo imageId. */
  context: ImageClinicalContextDto | null;
  form: ReactNode;
  viewerTitle?: string;
  /** Vòng 2 và 3: chỉ ảnh, không tải và không hiện thông tin lâm sàng. */
  imageOnly?: boolean;
}) {
  return (
    <div className="lite-review-grid">
      <FundusViewer source={source} title={viewerTitle} />
      <div className="lite-review-side">
        {imageOnly ? null : context ? (
          <section className="panel clin-panel">
            <div className="panel-h">Thông tin lâm sàng</div>
            <div className="panel-b"><ClinicalContextView ctx={context} /></div>
          </section>
        ) : source.kind !== "file" ? (
          <ClinicalContextPanel imageId={source.imageId} />
        ) : null}
        <section className="panel">
          <div className="panel-b">{form}</div>
        </section>
      </div>
    </div>
  );
}

/** Lớp phủ toàn màn hình cho vòng 1 (mở từ thẻ mắt ở trang lượt khám). */
export function BlindReviewDialog({
  title,
  source,
  context,
  onSubmit,
  onClose,
}: {
  title: string;
  source: ImageSource;
  context: ImageClinicalContextDto | null;
  onSubmit: (req: BlindGradeRequest) => void;
  onClose: () => void;
}) {
  useEffect(() => {
    const h = (e: KeyboardEvent) => e.key === "Escape" && onClose();
    addEventListener("keydown", h);
    const prev = document.body.style.overflow;
    document.body.style.overflow = "hidden";
    return () => {
      removeEventListener("keydown", h);
      document.body.style.overflow = prev;
    };
  }, [onClose]);
  // Chỉ gửi MỘT lần: lần bấm thứ hai (trước khi hộp kịp đóng) bị bỏ qua và nút chuyển sang trạng thái bận.
  const sent = useRef(false);
  const [submitting, setSubmitting] = useState(false);
  const submitOnce = useCallback(
    (req: BlindGradeRequest) => {
      if (sent.current) return;
      sent.current = true;
      setSubmitting(true);
      onSubmit(req);
    },
    [onSubmit],
  );
  const form = useMemo(
    () => <BlindForm busy={submitting} label="Lưu phân độ & chạy AI" onSubmit={submitOnce} />,
    [submitOnce, submitting],
  );
  return (
    <div className="lite-review" role="dialog" aria-modal="true" aria-label={title}>
      <div className="lite-review-h">
        <b>{title}</b>
        <span className="faint">Lăn chuột hoặc bấm +/− để phóng to · giữ chuột kéo để di chuyển · nhấp đúp để về 100%</span>
        <button className="close ghost" onClick={onClose} aria-label="Đóng" title="Đóng (Esc)">×</button>
      </div>
      <div className="lite-review-b">
        <BlindWorkspace source={source} context={context} form={form} />
      </div>
    </div>
  );
}
