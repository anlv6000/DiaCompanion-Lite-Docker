export const roles = [
  { value: "Admin", label: "Admin", key: "Admin" },
  { value: "Doctor", label: "Bác sĩ", key: "Doctor" },
  { value: "Receptionist", label: "Lễ tân", key: "Receptionist" },
] as const;
export const grades = ["Bình thường", "Nhẹ", "Trung bình", "Nặng", "PDR"];
export const gradeCodes = ["Normal", "Mild", "Moderate", "Severe", "PDR"];
export const eyes = ["OD", "OS"];
export const genders = ["Nam", "Nữ", "Khác"];
export const diabetesTypes = ["Không xác định", "Type 1", "Type 2", "Thai kỳ"];
export const visitStatuses = ["Đang khám", "Đã đóng"];
export const referralTypes = [
  "Không",
  
  "Chuyên khoa mắt",
  "Khẩn cấp",
];
export const qualityStatuses = ["Chờ duyệt", "Đạt", "Không đạt"];
export const metricTypes = [
  "",
  "Glucose",
  "HbA1c",
  "Huyết áp tâm thu",
  "Huyết áp tâm trương",
];
export const metricContexts = ["", "Trước ăn", "Sau ăn", "Trước ngủ"];
export const symptomSeverities = ["", "Nhẹ", "Trung bình", "Nặng"];
export const blogCategories = ["", "Kiến thức", "Dinh dưỡng", "Cảnh báo"];
export function label(
  list: string[],
  value: number | null | undefined,
  fallback = "—",
) {
  return value == null ? fallback : (list[value] ?? String(value));
}
