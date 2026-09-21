/**
 * Cấu hình tập trung. Mọi nơi cần địa chỉ backend đều import API_BASE từ đây,
 * không đọc window trực tiếp và không hardcode URL rải rác.
 *
 * Giá trị lấy từ window.__DIACOMPANION_API__ (đặt trong public/config.js) để
 * đổi được lúc deploy mà không cần build lại.
 */
declare global {
  interface Window {
    __DIACOMPANION_API__?: string;
  }
}

const DEFAULT_API = "https://localhost:55403/";

/**
 * Địa chỉ backend lúc chạy.
 *  - window.__DIACOMPANION_API__ (public/config.js) được ưu tiên → đổi được
 *    khi triển khai mà KHÔNG cần build lại.
 *  - Chuỗi rỗng "" nghĩa là cùng origin (IIS/Nginx reverse proxy /api/*).
 *  - Không đặt gì thì rơi về DEFAULT_API (môi trường dev).
 */
const runtimeApi =
  typeof window !== "undefined" ? window.__DIACOMPANION_API__ : undefined;

export const API_BASE: string = (
  typeof runtimeApi === "string" ? runtimeApi : DEFAULT_API
).replace(/\/$/, "");

/** Khoá lưu phiên trong sessionStorage. */
export const STORAGE_KEYS = {
  token: "diacompanion.token",
  user: "diacompanion.user",
} as const;

/** Trang mặc định sau đăng nhập nếu backend không chỉ định defaultRoute. */
export const DEFAULT_ROUTE = "/triage";

export {};
