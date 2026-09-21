import { useState } from "react";
import { researchApi } from "@/api/services";
import { useToast } from "@/contexts/ToastContext";
import { Button, Field, PageHeader, Panel } from "@/components/ui";

/* Trang dành cho vai trò Nghiên cứu: chỉ tải bộ dữ liệu CSV để đánh giá Gap 2.
   Không truy cập được hồ sơ bệnh nhân hay chức năng lâm sàng nào khác. */
export function LiteExportPage() {
  const toast = useToast();
  const [from, setFrom] = useState("");
  const [to, setTo] = useState("");
  const [busy, setBusy] = useState(false);

  const download = async () => {
    if (from && to && from > to) {
      toast.push("Khoảng ngày không hợp lệ (Từ ngày sau Đến ngày).", "error");
      return;
    }
    setBusy(true);
    try {
      const blob = await researchApi.datasetCsv(from || undefined, to || undefined);
      const stamp = new Date().toISOString().slice(0, 10);
      const url = URL.createObjectURL(blob);
      const a = document.createElement("a");
      a.href = url;
      a.download = `diacompanion-dataset-${stamp}.csv`;
      document.body.appendChild(a);
      a.click();
      a.remove();
      URL.revokeObjectURL(url);
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
      <Panel title="Tải CSV">
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
          <li>Xác nhận của bác sĩ: có xác nhận chưa, phê duyệt/ghi đè, phân độ tham chiếu, khoảng lệch phân độ, lý do, thời điểm.</li>
        </ul>
      </Panel>
    </>
  );
}
