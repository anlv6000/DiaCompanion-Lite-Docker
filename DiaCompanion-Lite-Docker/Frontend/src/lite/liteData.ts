import type { useData } from "@/contexts/DataContext";
import { gradingApi } from "@/api/services";
import type { AiDiagnosisDto, BlindGradeDto, FundusImageDto, HealthMetricDto } from "@/types/api";

/* Dữ liệu và nhãn dùng chung cho trang ca khám (LiteVisit) và hồ sơ bệnh án (LiteRecord). */

export type Data = ReturnType<typeof useData>;

export interface Entry {
  img: FundusImageDto;
  dx: AiDiagnosisDto | null;
  /** 1.3.1: kết quả AI còn hiệu lực KHÁC của cùng ảnh (cũ hơn dx). Bình thường rỗng; có phần tử khi dính lỗi
   *  chạy AI hai mắt cùng lúc (lần thử lại ghi thêm kết quả). Bị ẩn khỏi thẻ mắt nhưng vẫn chặn đóng lượt. */
  extraDx: AiDiagnosisDto[];
  /** Phân độ chấm mù vòng 1 (bác sĩ khám, trước AI). null = chưa chấm. */
  blind: BlindGradeDto | null;
}

/** Ảnh (đã lọc thu hồi ở backend) kèm kết quả AI mới nhất và phân độ chấm mù của từng ảnh. */
export async function loadEntries(data: Data, q: { patientId: number; visitId?: number }): Promise<Entry[]> {
  const imgs = await data.images.list(q);
  return Promise.all(
    imgs.map(async (img) => {
      const [all, blind] = await Promise.all([
        data.diagnoses.byImage(img.id).catch(() => [] as AiDiagnosisDto[]),
        gradingApi.round1(img.id).then((g) => g ?? null).catch(() => null),
      ]);
      // Backend trả mới nhất trước và đã lọc kết quả thu hồi.
      return { img, dx: all[0] ?? null, extraDx: all.slice(1), blind };
    }),
  );
}

/** Ảnh mới nhất của một mắt. */
export const latestOf = (entries: Entry[], eye: number): Entry | null =>
  entries.filter((x) => x.img.eye === eye).sort((a, b) => b.img.id - a.img.id)[0] ?? null;

/** Phân độ cuối do bác sĩ xác nhận (null nếu chưa xác nhận). */
export const finalGrade = (e: Entry | null): number | null =>
  e?.dx?.isConfirmed && e.dx.review ? e.dx.review.finalGrade : null;

export const EYE_NAME = ["Mắt phải (OD)", "Mắt trái (OS)"];
export const DR_TEXT = ["Không có DR", "DR nhẹ", "DR trung bình", "DR nặng", "DR tăng sinh (PDR)"];
export const drText = (g?: number | null) => (g == null ? "—" : DR_TEXT[g] ?? String(g));

/* Khớp enum ReferralType của backend (None=0, FollowUp=1, Ophthalmology=2, Urgent=3).
   Không dùng `referralTypes` trong lib/enums.ts vì mảng đó lệch nhãn từ giá trị 1. */
export const REFERRAL = ["Không", "Theo dõi", "Chuyên khoa mắt", "Khẩn cấp"];
/** Gợi ý chuyển tuyến theo mức DR cao nhất đã xác nhận (0..4). */
export const REFERRAL_BY_GRADE = [0, 1, 2, 3, 3];

/** Lý do "cần xem kỹ" bằng lời thường cho bác sĩ. */
export function plainDeferReason(dx: AiDiagnosisDto): string {
  switch (dx.deferReason) {
    case 1:
      return "AI không chắc chắn với kết quả này";
    case 2:
      return "Phân độ của AI không khớp với tổn thương phát hiện được";
    case 3:
      return "AI không chắc chắn và không khớp với tổn thương";
    case 4:
      return "Thiếu kết quả của một phần phân tích AI";
    case 5:
      return "Có dấu hiệu có thể cần chuyển tuyến";
    default:
      return dx.deferReasonLabel || "Cần bác sĩ xem kỹ";
  }
}

/** Huyết áp: backend trả một chỉ số kèm cặp (tâm thu/tâm trương). */
export const bpOf = (bp?: HealthMetricDto | null) => ({
  sys: bp?.systolicValue ?? (bp?.metricType === 3 ? bp.value : null),
  dia: bp?.diastolicValue ?? (bp?.metricType === 4 ? bp.value : null),
});

export const addMonths = (iso: string, months: number) => {
  const d = new Date(iso);
  d.setMonth(d.getMonth() + months);
  return d;
};

/* ---------------------------------------------------------------- ẩn danh */
/** Số điện thoại giả cho hồ sơ ẩn danh: bắt đầu bằng "00" (không phải số thật), 11 chữ số. */
export const placeholderPhone = () =>
  "00" + Array.from({ length: 9 }, () => Math.floor(Math.random() * 10)).join("");
export const isPlaceholderPhone = (p?: string | null) => !!p && p.startsWith("00");
/** Ẩn danh chỉ lưu năm sinh; ngày sinh quy ước = 01/07 năm đó. */
export const birthYearToDob = (y: number) => `${y}-07-01`;

/** Lưu Blob thành tệp tải về (trình duyệt hỏi/ghi vào Downloads). */
export function saveBlob(blob: Blob, name: string) {
  const url = URL.createObjectURL(blob);
  const a = document.createElement("a");
  a.href = url;
  a.download = name;
  document.body.appendChild(a);
  a.click();
  a.remove();
  // Thu hồi sau một nhịp: thu hồi ngay có trình duyệt hủy tải tệp lớn.
  setTimeout(() => URL.revokeObjectURL(url), 10_000);
}
