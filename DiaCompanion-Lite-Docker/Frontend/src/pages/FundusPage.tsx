import {
  useEffect,
  useRef,
  useState,
  type PointerEvent as ReactPointerEvent,
  type ReactNode,
  type CSSProperties,
} from "react";
import { useSearchParams, useNavigate } from "react-router-dom";
import { useData } from "@/contexts/DataContext";
import { useAuth } from "@/contexts/AuthContext";
import { useToast } from "@/contexts/ToastContext";
import { useAsync } from "@/lib/hooks";
import { can } from "@/lib/permissions";
import {
  PageHeader,
  Panel,
  Button,
  LoadState,
  GradeBadge,
  StatusBadge,
  Field,
  ConfirmDialog,
} from "@/components/ui";
import { fmtDate, num } from "@/lib/format";
import { grades, qualityStatuses } from "@/lib/enums";

export function FundusPage({ imageId }: { imageId: number }) {
  const [sp] = useSearchParams();
  const navigate = useNavigate();
  const data = useData();
  const toast = useToast();
  const { user } = useAuth();
  const diagId = Number(sp.get("diagnosis") || 0);
  const diagnoses = useAsync(() => data.diagnoses.byImage(imageId), [imageId]);
  const imageInfo = useAsync(() => data.images.get(imageId), [imageId]);
  const selected = diagId
    ? diagnoses.data?.find((x) => x.id === diagId)
    : diagnoses.data?.[0];

  const original = useBlobImage(() => data.images.content(imageId), [imageId]);
  const lesion = useBlobImage(
    () =>
      selected?.hasLesionMask
        ? data.diagnoses.lesionMask(selected.id)
        : Promise.resolve(null),
    [selected?.id, selected?.hasLesionMask],
  );
  const fractal = useBlobImage(
    () =>
      selected?.hasFractalImage
        ? data.diagnoses.fractalImage(selected.id)
        : Promise.resolve(null),
    [selected?.id, selected?.hasFractalImage],
  );

  const [redFree, setRedFree] = useState(false);
  const [busy, setBusy] = useState(false);
  const [override, setOverride] = useState(false);
  const [grade, setGrade] = useState(0);
  const [reason, setReason] = useState("");
  const [voidReview, setVoidReview] = useState(false);
  const [rejectQuality, setRejectQuality] = useState(false);

  useEffect(() => {
    if (selected) setGrade(selected.drGrade);
  }, [selected?.id]); // eslint-disable-line react-hooks/exhaustive-deps

  const closedVisit = selected?.visitStatus === 1;
  const canReview = can.reviewDiagnosis(user) && !closedVisit;
  const canRunAgain = canReview && !selected?.isConfirmed;

  const downloadOriginal = async () => {
    try {
      const blob = await data.images.content(imageId);
      const objectUrl = URL.createObjectURL(blob);
      const a = document.createElement("a");
      a.href = objectUrl;
      a.download = `fundus-${imageId}.${blob.type === "image/png" ? "png" : "jpg"}`;
      document.body.appendChild(a);
      a.click();
      a.remove();
      URL.revokeObjectURL(objectUrl);
    } catch (e) {
      toast.push((e as Error).message, "error");
    }
  };

  const run = async () => {
    if (selected?.isConfirmed) {
      toast.push("Kết quả đã được phê duyệt nên không thể chạy lại AI.", "error");
      return;
    }
    setBusy(true);
    try {
      const d = await data.diagnoses.run(imageId);
      toast.push(
        selected
          ? "Đã chạy lại đủ 3 model AI và tự động void lượt chạy cũ."
          : "Đã chạy đủ 3 model AI.",
        "success",
      );
      await diagnoses.reload();
      navigate(`/fundus/${imageId}?diagnosis=${d.id}`, { replace: true });
    } catch (e) {
      toast.push((e as Error).message, "error");
    } finally {
      setBusy(false);
    }
  };

  const review = async () => {
    if (!selected) return;
    setBusy(true);
    try {
      if (override)
        await data.triage.override(selected.id, {
          rowVersion: selected.rowVersion,
          finalGrade: grade,
          reason,
        });
      else await data.triage.approve(selected.id, selected.rowVersion);
      toast.push("Đã lưu xác nhận của bác sĩ.", "success");
      diagnoses.reload();
    } catch (e) {
      toast.push((e as Error).message, "error");
    } finally {
      setBusy(false);
    }
  };

  const setQuality = async (status: number, note?: string) => {
    const rv = imageInfo.data?.rowVersion;
    if (!rv) return;
    setBusy(true);
    try {
      await data.images.quality(imageId, status, note, rv);
      toast.push(
        status === 1
          ? "Đã duyệt: ảnh đạt chất lượng."
          : "Đã đánh dấu: ảnh không đạt chất lượng.",
        "success",
      );
      await imageInfo.reload();
      setRejectQuality(false);
    } catch (e) {
      toast.push((e as Error).message, "error");
    } finally {
      setBusy(false);
    }
  };

  const voidR = async (r: string) => {
    if (!selected?.review) return;
    await data.triage.voidReview(
      selected.review.id,
      r,
      selected.review.rowVersion,
    );
    toast.push("Đã thu hồi review; ca quay lại triage.", "success");
    setVoidReview(false);
    diagnoses.reload();
  };

  return (
    <>
      <PageHeader
        title={`Trình xem ảnh đáy mắt #${imageId}`}
        subtitle="Giữ chuột và kéo ảnh để xem chi tiết theo mọi hướng."
        actions={
          <>
            <Button onClick={() => navigate(-1)}>Quay lại</Button>
            <Button onClick={downloadOriginal}>Tải ảnh gốc</Button>
            {canReview && (
              <Button
                kind="primary"
                busy={busy}
                disabled={!canRunAgain}
                onClick={run}
              >
                {selected ? "Chạy lại 3 model AI" : "Chạy 3 model AI"}
              </Button>
            )}
          </>
        }
      />

      {imageInfo.data && (
        <div className="state ai-quality-bar">
          <span className="ai-quality-label">Chất lượng ảnh</span>
          <StatusBadge
            text={qualityStatuses[imageInfo.data.qualityStatus] ?? "—"}
            kind={
              imageInfo.data.qualityStatus === 1
                ? "ok"
                : imageInfo.data.qualityStatus === 2
                  ? "alert"
                  : ""
            }
          />
          {canReview && !closedVisit && !selected && (
            <div className="ai-quality-actions">
              <Button
                kind="primary"
                size="sm"
                busy={busy}
                disabled={busy || imageInfo.data.qualityStatus === 1}
                onClick={() => setQuality(1)}
              >
                Duyệt đạt
              </Button>
              <Button
                kind="danger"
                size="sm"
                disabled={busy || imageInfo.data.qualityStatus === 2}
                onClick={() => setRejectQuality(true)}
              >
                Không đạt
              </Button>
            </div>
          )}
          {selected && (
            <small className="ai-quality-hint faint">
              Đã chạy AI nên không đổi được đánh giá chất lượng.
            </small>
          )}
        </div>
      )}

      {closedVisit && (
        <div className="state ai-rerun-lock">
          Lượt khám đã đóng. Ảnh, kết quả AI và review của lượt này chỉ được xem.
        </div>
      )}

      {selected?.isConfirmed && !closedVisit && (
        <div className="state ai-rerun-lock">
          Kết quả này đã được phê duyệt. Hệ thống đã khóa chức năng chạy lại AI.
        </div>
      )}

      <LoadState
        loading={diagnoses.loading}
        error={diagnoses.error}
        empty={false}
        onRetry={diagnoses.reload}
      >
        <div className="ai-result-grid">
          <PanZoomSquare
            title="Ảnh gốc"
            url={original.url}
            loading={original.loading}
            error={original.error}
            imageStyle={{
              filter: redFree
                ? "grayscale(1) contrast(1.3) sepia(.1) hue-rotate(65deg)"
                : "none",
            }}
            extraTool={
              <label className="checkbox ai-viewer-check">
                <input
                  type="checkbox"
                  checked={redFree}
                  onChange={(e) => setRedFree(e.target.checked)}
                />
                Red-free
              </label>
            }
          />

          <PanZoomSquare
            title="Mask tổn thương"
            url={lesion.url}
            loading={lesion.loading}
            error={lesion.error}
            emptyText={
              selected
                ? "Lần chạy này không có ảnh mask tổn thương."
                : "Chưa có kết quả AI."
            }
          />

          <PanZoomSquare
            title="Ảnh mạch máu / Fractal"
            url={fractal.url}
            loading={fractal.loading}
            error={fractal.error}
            emptyText={
              selected
                ? "Lần chạy này không có ảnh fractal."
                : "Chưa có kết quả AI."
            }
          />

          <section className="panel ai-diagnosis-square">
            <div className="panel-h">AI hỗ trợ chẩn đoán</div>
            <div className="panel-b ai-diagnosis-scroll">
              <LoadState
                loading={diagnoses.loading}
                error={diagnoses.error}
                empty={!selected}
                onRetry={diagnoses.reload}
              >
                {selected && (
                  <>
                    <div className="split">
                      <GradeBadge grade={selected.drGrade} />
                      <StatusBadge
                        text={
                          selected.isConfirmed ? "Đã xác nhận" : "Chưa xác nhận"
                        }
                        kind={selected.isConfirmed ? "ok" : "defer"}
                      />
                    </div>

                    {/* Ưu tiên hành động: nếu cần bác sĩ xem, nói rõ bằng lời và đặt lên đầu */}
                    {selected.isDeferred ? (
                      <div
                        className="state"
                        style={{
                          background: "var(--defer-bg)",
                          borderColor: "var(--defer)",
                        }}
                      >
                        <b>Cần bác sĩ xem xét</b>
                        <div>
                          {selected.deferReason === 5
                            ? "AI thấy dấu hiệu có thể cần chuyển tuyến — bác sĩ xác nhận."
                            : selected.deferReason === 4
                              ? "Thiếu một phần kết quả AI — cần bác sĩ xác nhận trực tiếp trên ảnh."
                              : selected.deferReason === 2
                                ? "AI phân độ và mức độ tổn thương chưa thống nhất — cần bác sĩ xác nhận."
                                : selected.deferReasonLabel || "Cần bác sĩ xác nhận."}
                        </div>
                        {selected.clinicalRiskFactors && (
                          <div className="small faint" style={{ marginTop: 4 }}>
                            Yếu tố ưu tiên xem sớm: {selected.clinicalRiskFactors}
                          </div>
                        )}
                      </div>
                    ) : (
                      !selected.isConfirmed && (
                        <div className="state">
                          AI không thấy mâu thuẫn giữa các nhánh — bác sĩ xác nhận để
                          hoàn tất.
                        </div>
                      )
                    )}

                    {/* Đối chiếu phân độ theo tổn thương — thông tin lâm sàng, không phải số nghiên cứu */}
                    <div className="detail-grid ai-detail-grid">
                      <Info
                        k="Thời điểm chạy"
                        v={fmtDate(selected.createdAt, true)}
                      />
                      <Info
                        k="Phân độ theo tổn thương"
                        v={
                          selected.lesionGradeImplied == null
                            ? "—"
                            : grades[selected.lesionGradeImplied]
                        }
                      />
                    </div>
                    {selected.lesionGradeImplied != null &&
                      selected.lesionGradeImplied !== selected.drGrade && (
                        <div className="small faint">
                          Phân bố tổn thương gợi ý mức khác với phân độ chính — nên
                          xem kỹ ảnh trước khi xác nhận.
                        </div>
                      )}

                    {/* Tổn thương đếm được — thông tin lâm sàng trực tiếp */}
                    <div className="ai-section">
                      <b>Tổn thương phát hiện</b>
                      <div className="bars">
                        <Lesion name="Vi phình mạch (MA)" value={selected.countMA} />
                        <Lesion name="Xuất huyết (HE)" value={selected.countHE} />
                        <Lesion name="Xuất tiết cứng (EX)" value={selected.countEX} />
                        <Lesion name="Xuất tiết mềm (SE)" value={selected.countSE} />
                      </div>
                    </div>

                    {selected.fractalDimension != null && (
                      <div className="ai-section">
                        <b>Chỉ số mạch máu võng mạc</b>
                        <div className="split">
                          <span>Độ phức tạp mạch máu (FD)</span>
                          <b className="mono">
                            {num(selected.fractalDimension, 3)}
                          </b>
                        </div>
                        <div className="small faint">
                          Tham chiếu ~1,43–1,47 ở mạch máu bình thường. Dùng để so
                          sánh xu hướng giữa các lần chụp của cùng bệnh nhân, không
                          thay chẩn đoán của bác sĩ.
                        </div>
                      </div>
                    )}

                    {/* Số nghiên cứu gập lại — bác sĩ không cần, để phục vụ kiểm chứng/nghiên cứu */}
                    <details className="ai-tech">
                      <summary>Chi tiết kỹ thuật (dành cho nghiên cứu)</summary>
                      <div
                        className="detail-grid ai-detail-grid"
                        style={{ marginTop: 8 }}
                      >
                        <Info k="Bất đồng chéo" v={num(selected.disagreement, 3)} />
                        <Info
                          k="Ngưỡng áp dụng"
                          v={num(selected.effectiveDisagreementThreshold, 2)}
                        />
                        <Info
                          k="Điểm nguy cơ nền"
                          v={
                            selected.clinicalRiskScore == null
                              ? "—"
                              : `${selected.clinicalRiskScore} điểm`
                          }
                        />
                        <Info
                          k="Lacunarity (độ rỗng mạch)"
                          v={num(selected.lacunarity, 4)}
                        />
                        <Info
                          k="Bất đối xứng vùng"
                          v={num(selected.fractalAsymmetry, 4)}
                        />
                        <Info
                          k="Chênh thái dương–mũi"
                          v={num(selected.fractalTn, 4)}
                        />
                      </div>

                      {(selected.fractalSt != null ||
                        selected.fractalSn != null ||
                        selected.fractalIt != null ||
                        selected.fractalIn != null) && (
                        <table className="ai-quad-table">
                          <thead>
                            <tr>
                              <th>Vùng</th>
                              <th>Vị trí</th>
                              <th>FD</th>
                            </tr>
                          </thead>
                          <tbody>
                            <tr>
                              <td>ST</td>
                              <td>Trên, phía thái dương</td>
                              <td className="mono">{num(selected.fractalSt, 4)}</td>
                            </tr>
                            <tr>
                              <td>SN</td>
                              <td>Trên, phía mũi</td>
                              <td className="mono">{num(selected.fractalSn, 4)}</td>
                            </tr>
                            <tr>
                              <td>IT</td>
                              <td>Dưới, phía thái dương</td>
                              <td className="mono">{num(selected.fractalIt, 4)}</td>
                            </tr>
                            <tr>
                              <td>IN</td>
                              <td>Dưới, phía mũi</td>
                              <td className="mono">{num(selected.fractalIn, 4)}</td>
                            </tr>
                          </tbody>
                        </table>
                      )}

                      {selected.fractalNote &&
                        selected.fractalAsymmetry == null && (
                          <p className="small faint" style={{ marginTop: 8 }}>
                            {selected.fractalNote}
                          </p>
                        )}
                    </details>

                    {selected.review ? (
                      <div className="ai-section">
                        <b>Xác nhận của bác sĩ</b>
                        <div className="detail-grid ai-detail-grid">
                          <Info k="Hành động" v={selected.review.actionLabel} />
                          <Info
                            k="Phân độ cuối"
                            v={selected.review.finalGradeLabel}
                          />
                          <Info k="Bác sĩ" v={selected.review.doctorName} />
                          <Info
                            k="Thời điểm"
                            v={fmtDate(selected.review.createdAt, true)}
                          />
                        </div>
                        {selected.review.reason && <p>{selected.review.reason}</p>}
                        {!closedVisit && can.voidReview(user) && (
                          <Button kind="danger" onClick={() => setVoidReview(true)}>
                            Thu hồi xác nhận
                          </Button>
                        )}
                      </div>
                    ) : (
                      canReview && (
                        <div className="ai-section">
                          <b>Xác nhận kết quả</b>
                          <label className="checkbox">
                            <input
                              type="checkbox"
                              checked={override}
                              onChange={(e) => setOverride(e.target.checked)}
                            />
                            Ghi đè phân độ AI
                          </label>
                          {override && (
                            <>
                              <Field labelText="Phân độ cuối">
                                <select
                                  value={grade}
                                  onChange={(e) =>
                                    setGrade(Number(e.target.value))
                                  }
                                >
                                  {grades.map((x, i) => (
                                    <option key={i} value={i}>
                                      {x}
                                    </option>
                                  ))}
                                </select>
                              </Field>
                              <Field labelText="Lý do" required>
                                <textarea
                                  value={reason}
                                  onChange={(e) => setReason(e.target.value)}
                                />
                              </Field>
                            </>
                          )}
                          <Button
                            kind="primary"
                            busy={busy}
                            disabled={override && !reason.trim()}
                            onClick={review}
                          >
                            {override ? "Lưu ghi đè" : "Phê duyệt"}
                          </Button>
                        </div>
                      )
                    )}
                  </>
                )}
              </LoadState>
            </div>
          </section>
        </div>
      </LoadState>

      {rejectQuality && (
        <ConfirmDialog
          title="Đánh dấu ảnh không đạt"
          message="Ảnh không đạt chất lượng sẽ không được đưa vào chạy AI. Vui lòng nhập lý do."
          confirmText="Xác nhận không đạt"
          requireReason
          danger
          busy={busy}
          onClose={() => setRejectQuality(false)}
          onConfirm={(reason) => setQuality(2, reason)}
        />
      )}

      {voidReview && selected?.review && (
        <ConfirmDialog
          title="Thu hồi xác nhận"
          message="Kết quả xác nhận sẽ được thu hồi và ca được đưa lại vào danh sách cần bác sĩ xem xét."
          requireReason
          danger
          onClose={() => setVoidReview(false)}
          onConfirm={voidR}
        />
      )}
    </>
  );
}

function useBlobImage(
  loader: () => Promise<Blob | null>,
  deps: readonly unknown[],
) {
  const [url, setUrl] = useState("");
  const [loading, setLoading] = useState(false);
  const [error, setError] = useState("");

  useEffect(() => {
    let active = true;
    let objectUrl = "";
    setUrl("");
    setError("");
    setLoading(true);

    loader()
      .then((blob) => {
        if (!active || !blob) return;
        objectUrl = URL.createObjectURL(blob);
        setUrl(objectUrl);
      })
      .catch((e) => {
        if (active) setError((e as Error).message);
      })
      .finally(() => {
        if (active) setLoading(false);
      });

    return () => {
      active = false;
      if (objectUrl) URL.revokeObjectURL(objectUrl);
    };
    // loader is intentionally represented by deps supplied by the caller.
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, deps);

  return { url, loading, error };
}

function PanZoomSquare({
  title,
  url,
  loading,
  error,
  emptyText = "Không có ảnh.",
  imageStyle,
  extraTool,
}: {
  title: string;
  url: string;
  loading: boolean;
  error: string;
  emptyText?: string;
  imageStyle?: CSSProperties;
  extraTool?: ReactNode;
}) {
  const [zoom, setZoom] = useState(1);
  const [offset, setOffset] = useState({ x: 0, y: 0 });
  const drag = useRef<{
    pointerId: number;
    startX: number;
    startY: number;
    originX: number;
    originY: number;
  } | null>(null);

  useEffect(() => {
    setZoom(1);
    setOffset({ x: 0, y: 0 });
  }, [url]);

  const changeZoom = (next: number) => {
    const value = Math.min(6, Math.max(0.5, next));
    setZoom(value);
    if (value <= 1) setOffset({ x: 0, y: 0 });
  };

  const onPointerDown = (e: ReactPointerEvent<HTMLDivElement>) => {
    if (!url || zoom <= 1) return;
    e.currentTarget.setPointerCapture(e.pointerId);
    drag.current = {
      pointerId: e.pointerId,
      startX: e.clientX,
      startY: e.clientY,
      originX: offset.x,
      originY: offset.y,
    };
  };

  const onPointerMove = (e: ReactPointerEvent<HTMLDivElement>) => {
    if (!drag.current || drag.current.pointerId !== e.pointerId) return;
    setOffset({
      x: drag.current.originX + e.clientX - drag.current.startX,
      y: drag.current.originY + e.clientY - drag.current.startY,
    });
  };

  const stopDrag = (e: ReactPointerEvent<HTMLDivElement>) => {
    if (drag.current?.pointerId === e.pointerId) drag.current = null;
  };

  return (
    <section className="panel ai-image-square">
      <div className="panel-h ai-square-header">
        <span>{title}</span>
        <span className="badge mono">{Math.round(zoom * 100)}%</span>
      </div>
      <div className="fundus-toolbar ai-square-toolbar">
        <Button onClick={() => changeZoom(zoom + 0.25)}>+</Button>
        <Button onClick={() => changeZoom(zoom - 0.25)}>−</Button>
        <Button
          onClick={() => {
            setZoom(1);
            setOffset({ x: 0, y: 0 });
          }}
        >
          100%
        </Button>
        {extraTool}
      </div>
      <div
        className={`fundus ai-panzoom ${zoom > 1 ? "is-zoomed" : ""}`}
        onPointerDown={onPointerDown}
        onPointerMove={onPointerMove}
        onPointerUp={stopDrag}
        onPointerCancel={stopDrag}
      >
        {loading ? (
          <div>Đang tải ảnh có kiểm quyền…</div>
        ) : error ? (
          <div className="ai-image-error">{error}</div>
        ) : url ? (
          <img
            className="viewer-image"
            src={url}
            draggable={false}
            alt={title}
            style={{
              transform: `translate3d(${offset.x}px, ${offset.y}px, 0) scale(${zoom})`,
              ...imageStyle,
            }}
          />
        ) : (
          <div>{emptyText}</div>
        )}
      </div>
    </section>
  );
}

function Info({ k, v }: { k: string; v: ReactNode }) {
  return (
    <div>
      <small>{k}</small>
      <div className="mono">{v ?? "—"}</div>
    </div>
  );
}

function Lesion({ name, value }: { name: string; value?: number | null }) {
  return (
    <div className="split">
      <span>{name}</span>
      <b className="mono">{value ?? "—"}</b>
    </div>
  );
}
