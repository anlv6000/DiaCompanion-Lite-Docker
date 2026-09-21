const CLINIC_TIME_ZONE = "Asia/Ho_Chi_Minh";

/** YYYY-MM-DD theo ngày của phòng khám (+07), không phụ thuộc timezone máy đang mở web. */
export const clinicToday = () => {
  const parts = new Intl.DateTimeFormat("en-CA", {
    timeZone: CLINIC_TIME_ZONE,
    year: "numeric",
    month: "2-digit",
    day: "2-digit",
  }).formatToParts(new Date());
  const get = (type: string) => parts.find((p) => p.type === type)?.value || "";
  return `${get("year")}-${get("month")}-${get("day")}`;
};

export const fmtDate = (value?: string | null, withTime = false) => {
  if (!value) return "—";
  const d = new Date(value);
  return Number.isNaN(d.getTime())
    ? value
    : new Intl.DateTimeFormat(
        "vi-VN",
        withTime
          ? { dateStyle: "short", timeStyle: "short", timeZone: CLINIC_TIME_ZONE }
          : { dateStyle: "short", timeZone: CLINIC_TIME_ZONE },
      ).format(d);
};
export const fmtTime = (value?: string | null) =>
  value
    ? new Intl.DateTimeFormat("vi-VN", {
        hour: "2-digit",
        minute: "2-digit",
        timeZone: CLINIC_TIME_ZONE,
      }).format(new Date(value))
    : "—";
export const pct = (value?: number | null) =>
  value == null ? "—" : `${Math.round(Number(value) * 100)}%`;
export const num = (value?: number | null, digits = 2) =>
  value == null
    ? "—"
    : Number(value).toLocaleString("vi-VN", { maximumFractionDigits: digits });
export const localDateInput = (value?: string | null) => {
  if (!value) return "";
  const d = new Date(value);
  const off = d.getTimezoneOffset();
  return new Date(d.getTime() - off * 60000).toISOString().slice(0, 16);
};
export const query = (params: Record<string, unknown>) => {
  const p = new URLSearchParams();
  Object.entries(params).forEach(([k, v]) => {
    if (v !== undefined && v !== null && v !== "") p.set(k, String(v));
  });
  const s = p.toString();
  return s ? `?${s}` : "";
};
/**
 * Chuẩn hoá chuỗi Unicode về dạng TỔ HỢP (NFC).
 *
 * Trước đây dùng NFD — đó là dạng PHÂN RÃ, tách "ề" thành "ê" + dấu huyền
 * rời (U+0300). Trình duyệt vẽ hai ký tự tách nhau nên tiêu đề hiện thành
 * "Tiê`n sử tiểu đường", "Thu hô`i hô` sơ".
 *
 * NFC gộp lại thành một ký tự dựng sẵn, đúng dạng SQL Server trả về và đúng
 * dạng mọi phông chữ có glyph sẵn.
 */
export function normalizeText(value?: string | null) {
  return value == null ? value : value.normalize("NFC");
}

export const initials = (name?: string | null) =>
  (name || "DC")
    .split(/\s+/)
    .slice(-2)
    .map((x) => x[0])
    .join("")
    .toUpperCase();
export function downloadText(
  filename: string,
  text: string,
  type = "text/plain;charset=utf-8",
) {
  const blob = new Blob([text], { type });
  const url = URL.createObjectURL(blob);
  const a = document.createElement("a");
  a.href = url;
  a.download = filename;
  a.click();
  setTimeout(() => URL.revokeObjectURL(url), 500);
}
export function toCsv(rows: Record<string, unknown>[]) {
  if (!rows.length) return "";
  const keys = Object.keys(rows[0]);
  const esc = (v: unknown) => `"${String(v ?? "").replace(/"/g, '""')}"`;
  return [
    keys.map(esc).join(","),
    ...rows.map((r) => keys.map((k) => esc(r[k])).join(",")),
  ].join("\n");
}
