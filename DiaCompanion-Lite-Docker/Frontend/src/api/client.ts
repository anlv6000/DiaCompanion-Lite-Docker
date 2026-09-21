import { API_BASE, STORAGE_KEYS } from "@/config";
import type { ApiMessage } from "@/types/api";

/* Lớp HTTP mỏng. Token giữ trong sessionStorage; mọi request tự đính Bearer.
   API_BASE lấy từ config (không đọc window trực tiếp ở đây). */

const TOKEN_KEY = STORAGE_KEYS.token;

export { API_BASE };

export const tokenStore = {
  get: () => sessionStorage.getItem(TOKEN_KEY),
  set: (v: string | null) =>
    v
      ? sessionStorage.setItem(TOKEN_KEY, v)
      : sessionStorage.removeItem(TOKEN_KEY),
};

export class ApiError extends Error {
  status: number;
  code?: string;
  detail?: string;
  traceId?: string;
  constructor(status: number, message: string, body?: ApiMessage) {
    super(message);
    this.name = "ApiError";
    this.status = status;
    this.code = body?.messageCode;
    this.detail = body?.detail;
    this.traceId = body?.traceId;
  }

  get isConflict() {
    return this.status === 409;
  }
}

export function isConflict(error: unknown): error is ApiError {
  return error instanceof ApiError && error.status === 409;
}

function normalizeJson(value: unknown): unknown {
  if (typeof value === "string") return value.normalize("NFC");
  if (Array.isArray(value)) return value.map(normalizeJson);
  if (value && typeof value === "object") {
    return Object.fromEntries(
      Object.entries(value as Record<string, unknown>).map(([k, v]) => [
        k,
        normalizeJson(v),
      ]),
    );
  }
  return value;
}

async function parseError(res: Response): Promise<never> {
  let body: (ApiMessage & { title?: string; errors?: Record<string, string[]> }) | undefined;
  try {
    body = await res.json();
  } catch {
    body = undefined;
  }

  const validation = body?.errors
    ? Object.values(body.errors).flat().filter(Boolean).join(" ")
    : "";
  const message = body?.message || validation || body?.title || `Yêu cầu thất bại (${res.status})`;
  throw new ApiError(res.status, message, body);
}

export async function request<T>(
  path: string,
  init: RequestInit = {},
): Promise<T> {
  const headers = new Headers(init.headers || {});
  const token = tokenStore.get();
  if (token) headers.set("Authorization", `Bearer ${token}`);

  let body: BodyInit | null | undefined = init.body;
  if (body && !(body instanceof FormData) && !headers.has("Content-Type")) {
    headers.set("Content-Type", "application/json");
  }

  if (body && !(body instanceof FormData) && typeof body !== "string") {
    body = JSON.stringify(normalizeJson(body));
  }

  let res: Response;
  try {
    res = await fetch(API_BASE + path, { ...init, headers, body });
  } catch {
    throw new ApiError(
      0,
      "Không kết nối được backend. Kiểm tra API đang chạy tại " + API_BASE,
    );
  }

  // Token hết hạn / không hợp lệ → phát sự kiện để AuthContext đẩy về login.
  if (res.status === 401)
    window.dispatchEvent(new CustomEvent("dc:unauthorized"));
  if (!res.ok) await parseError(res);
  if (res.status === 204) return undefined as T;

  const type = res.headers.get("content-type") || "";
  if (type.includes("application/json")) {
    const json = await res.json();
    return normalizeJson(json) as T;
  }
  return (await res.text()) as T;
}

export const http = {
  get: <T>(p: string) => request<T>(p),
  post: <T>(p: string, b?: unknown) =>
    request<T>(p, {
      method: "POST",
      body: b === undefined ? undefined : JSON.stringify(b),
    }),
  put: <T>(p: string, b?: unknown) =>
    request<T>(p, {
      method: "PUT",
      body: b === undefined ? undefined : JSON.stringify(b),
    }),
  delete: <T>(p: string) => request<T>(p, { method: "DELETE" }),
  upload: <T>(p: string, f: FormData) =>
    request<T>(p, { method: "POST", body: f }),
  blob: async (p: string): Promise<Blob> => {
    const headers = new Headers();
    const token = tokenStore.get();
    if (token) headers.set("Authorization", `Bearer ${token}`);
    const res = await fetch(API_BASE + p, { headers });
    if (!res.ok) await parseError(res);
    return await res.blob();
  },
};
