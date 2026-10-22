import { useState } from "react";
import { gradingApi, researchApi } from "@/api/services";
import { useAsync } from "@/lib/hooks";
import { ProgressPanel } from "@/lite/LiteGrading";
import { useToast } from "@/contexts/ToastContext";
import { Button, Field, PageHeader, Panel } from "@/components/ui";

/* Trang dành cho vai trò Nghiên cứu: chỉ tải bộ dữ liệu CSV để đánh giá Gap 2.
   Không truy cập được hồ sơ bệnh nhân hay chức năng lâm sàng nào khác. */
// Ngày theo giờ máy (toISOString là UTC: 0h–7h sáng giờ VN sẽ ra ngày hôm trước).
const localDate = () => new Date().toLocaleDateString("sv-SE");

export function LiteExportPage() {
  const toast = useToast();
  const [from, setFrom] = useState("");
  const [to, setTo] = useState("");
  const [busy, setBusy] = useState(false);
  const [busyGrading, setBusyGrading] = useState(false);
  const progress = useAsync(() => gradingApi.progress(), []);

  const save = (blob: Blob, name: string) => {
    const url = URL.createObjectURL(blob);
    const a = document.createElement("a");
    a.href = url;
    a.download = name;
    document.body.appendChild(a);
    a.click();
    a.remove();
    URL.revokeObjectURL(url);
  };

  const downloadGrading = async () => {
    setBusyGrading(true);
    try {
      save(await researchApi.gradingCsv(), `dr-ai-diagnosis-grading-${localDate()}.csv`);
      toast.push("Đã tải bảng chấm mù.", "success");
    } catch (e) {
      toast.push((e as Error).message, "error");
    } finally {
      setBusyGrading(false);
    }
  };

  const download = async () => {
    if (from && to && from > to) {
      toast.push("Khoảng ngày không hợp lệ (Từ ngày sau Đến ngày).", "error");
      return;
    }
    setBusy(true);
    try {
      const blob = await researchApi.datasetCsv(from || undefined, to || undefined);
      save(blob, `dr-ai-diagnosis-dataset-${localDate()}.csv`);
      toast.push("Đã tải tệp CSV.", "success");
    } catch (e) {
      toast.push((e as Error).message, "error");
    } finally {
      setBusy(false);
    }
  };

  return (
    <>
      <PageHeader
        title="Kết xuất dữ liệu nghiên cứu"
        subtitle="Bộ dữ liệu cho Gap 2 (đánh giá cơ chế deferral). Mỗi dòng là một lần chạy AI của một mắt; đã bỏ mọi trường định danh."
      />
      {progress.data && <ProgressPanel p={progress.data} />}
      <Panel title="Bảng chấm mù (nhãn tham chiếu)">
        <p className="muted">
          Mỗi dòng là một ảnh, gồm phân độ chấm mù vòng 1 (bác sĩ khám, trước AI), vòng 2 (người chấm độc lập),
          vòng 3 (phân xử) và nhãn tham chiếu cuối. Nối với dataset CSV qua cột <code>image_id</code>.
        </p>
        <Button kind="primary" busy={busyGrading} busyText="Đang tạo tệp…" onClick={downloadGrading}>
          Tải bảng chấm mù CSV
        </Button>
      </Panel>
      <Panel title="Tải dataset CSV (kết quả AI)">
        <p className="muted">
          Mỗi lần tải được ghi vào nhật ký kiểm toán. Tệp mã hóa UTF-8 (có BOM) nên Excel mở đúng tiếng Việt.
          Để trống khoảng ngày để lấy toàn bộ.
        </p>
        <div className="form-row two">
          <Field labelText="Từ ngày (theo ngày chạy AI)">
            <input type="date" value={from} onChange={(e) => setFrom(e.target.value)} />
          </Field>
          <Field labelText="Đến ngày">
            <input type="date" value={to} onChange={(e) => setTo(e.target.value)} />
          </Field>
        </div>
        <Button kind="primary" busy={busy} busyText="Đang tạo tệp…" onClick={download}>
          Tải dataset CSV
        </Button>
      </Panel>
      <Panel title="Bộ biến trong tệp">
        <p className="muted">Các nhóm cột chính:</p>
        <ul>
          <li>Bối cảnh bệnh nhân (giả lập): mã, giới, tuổi khi khám, loại ĐTĐ, số năm mắc, HbA1c nền.</li>
          <li>Chỉ số theo lượt: HbA1c, đường huyết, huyết áp tâm thu/tâm trương, tháng tái khám.</li>
          <li>Kết quả AI: phân độ DR, phân độ suy từ tổn thương, số đếm và diện tích MA/HE/EX/SE.</li>
          <li>Nội bộ deferral: bất đồng chéo, điểm nguy cơ nền và các yếu tố, ngưỡng hiệu dụng, trạng thái + lý do chuyển bác sĩ.</li>
          <li>Fractal: FD tổng, bốn cung, bất đối xứng, TN, lacunarity.</li>
          <li>Phiên bản model (DR / lesion / fractal).</li>
          <li>Quyết định lâm sàng của bác sĩ SAU khi xem AI (phê duyệt/ghi đè, lý do). Không dùng làm nhãn tham chiếu — nhãn tham chiếu nằm ở bảng chấm mù.</li>
          <li>Độ tin cậy và xác suất 5 mức của nhánh phân loại (baseline "hoãn theo độ tin cậy").</li>
        </ul>
      </Panel>
    </>
  );
}
