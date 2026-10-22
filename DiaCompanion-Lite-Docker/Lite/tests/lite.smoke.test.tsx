// @vitest-environment jsdom
import { describe, it, expect, beforeEach, afterEach, vi } from "vitest";
import { render, screen, fireEvent, waitFor, cleanup } from "@testing-library/react";
import { MemoryRouter } from "react-router-dom";
import { AuthProvider } from "@/contexts/AuthContext";
import { DataProvider } from "@/contexts/DataContext";
import { ToastProvider } from "@/contexts/ToastContext";
import { LiteRoutes } from "@/lite/LiteRoutes";

const patient = { id: 7, code: "BN001", fullName: "Nguyen Van A", age: 55, gender: 0, phone: "0912345678", diabetesType: 2, diabetesDurationYears: 8, baselineHbA1c: 8.1, latestDrGrade: null, latestVisitDate: null, hasAccount: false, dateOfBirth: "1970-01-01", createdAt: "2026-01-01T00:00:00Z", visitCount: 1, rowVersion: "AA==" };
const paged = (items: any[]) => ({ items, page: 1, pageSize: 25, totalItems: items.length, totalPages: 1, rangeLabel: "1–1 / 1" });
const openVisit = { id: 11, patientId: 7, patientName: "x", patientCode: "BN001", doctorId: 1, visitDate: "2026-09-20T01:00:00Z", status: 0, imageCount: 1, pendingReviewCount: 1, rowVersion: "AA==" };
const closedVisit = { id: 9, patientId: 7, patientName: "x", patientCode: "BN001", doctorId: 1, visitDate: "2026-06-01T01:00:00Z", status: 1, imageCount: 1, pendingReviewCount: 0, conclusion: "ok", referral: 2, rowVersion: "AA==" };
const img = { id: 21, patientId: 7, visitId: 11, eye: 0, qualityStatus: 1, createdAt: "2026-09-20T01:00:00Z", rowVersion: "AA==" };
const imgOld = { id: 15, patientId: 7, visitId: 9, eye: 1, qualityStatus: 1, createdAt: "2026-06-01T01:00:00Z", rowVersion: "AA==" };
const dx = { id: 31, fundusImageId: 21, eye: 0, drGrade: 0, lesionGradeImplied: 3, disagreement: 0.75, effectiveDisagreementThreshold: 0.35, clinicalRiskScore: 0, countMA: 0, countHE: 0, countEX: 55, countSE: 0, isDeferred: true, deferReasonLabel: "Bất đồng cao", hasLesionMask: true, hasFractalImage: true, createdAt: "2026-09-20T01:05:00Z", isConfirmed: false };
const dxOld = { ...dx, id: 25, fundusImageId: 15, eye: 1, drGrade: 2, isDeferred: false, isConfirmed: true, review: { id: 5, action: 0, actionLabel: "Phê duyệt", finalGrade: 2, finalGradeLabel: "Trung bình" } };
const calls: string[] = [];
let dxDeferred = true; // đổi theo từng test
let blind21: any = { imageId: 21, round: 1, gradable: true, grade: 0, createdAt: "2026-09-20T01:02:00Z" };
let hasDx21 = true;
const posted: { path: string; body: any }[] = [];

function route(url: string, init?: any): any {
  const path = url.replace(/^https?:\/\/[^/]+/, "");
  calls.push((init?.method || "GET") + " " + path);
  if (path.startsWith("/api/patients?")) return paged([patient]);
  if (path === "/api/patients/7") return patient;
  if (path.startsWith("/api/visits?")) return paged([openVisit, closedVisit]);
  if (path === "/api/visits/11/health-metrics") return { visitId: 11, hbA1c: { value: 8.4, metricType: 2 } };
  if (path === "/api/visits/9/health-metrics") return { visitId: 9, hbA1c: { value: 9.1, metricType: 2 }, bloodPressure: { value: 150, metricType: 3, diastolicValue: 90 } };
  if (path.startsWith("/api/images?")) {
    const v = new URLSearchParams(path.split("?")[1]).get("visitId"); // backend lọc theo visitId
    return [img, imgOld].filter((i) => !v || String(i.visitId) === v);
  }
  if (path === "/api/diagnoses/by-image/21") return hasDx21 ? [{ ...dx, isDeferred: dxDeferred }] : [];
  if (path === "/api/grading/round1/21" && (init?.method || "GET") === "GET") return blind21 ?? undefined;
  if (path === "/api/grading/round1/15") return { imageId: 15, round: 1, gradable: true, grade: 2, createdAt: "2026-06-01T01:02:00Z" };
  if (path.startsWith("/api/grading/round1/") && init?.method === "POST") { posted.push({ path, body: JSON.parse(init.body) }); return { imageId: 21, round: 1, gradable: true, grade: 1, createdAt: "x" }; }
  if (path === "/api/patients" && init?.method === "POST") { posted.push({ path, body: JSON.parse(init.body) }); return { patient: { id: 7 } }; }
  if (path === "/api/grading/images/21/context") return { imageId: 21, eye: 0, researchCode: null, birthYear: 1968, ageAtVisit: 58, gender: 0, diabetesType: 2, diabetesDurationYears: 12, baselineHbA1c: 7.9, patientNote: "đã laser mắt trái", hbA1c: 8.2, systolicBp: 150, diastolicBp: 90, glucose: 9.1, glucoseContext: 1 };
  if (path === "/api/grading/progress") return { round1: 4, round1Ungradable: 1, round2Done: 2, round2Pending: 1, round3Needed: 1, round3Done: 0 };
  if (path === "/api/grading/queue/2") return { round: 2, remaining: 1, items: [{ imageId: 21, position: 1 }] };
  if (path === "/api/grading/queue/3") return { round: 3, remaining: 0, items: [] };
  if (path.startsWith("/api/grading/round/") && init?.method === "POST") { posted.push({ path, body: JSON.parse(init.body) }); return {}; }
  if (path === "/api/diagnoses/by-image/15") return [dxOld];
  if (path.startsWith("/api/diagnoses/progression/7")) return { points: [{ date: "2026-06-01", visitId: 9, confirmedGrade: 2, fractalDimension: 1.4, hbA1c: 9.1 }], trendWarning: null };
  return {};
}

afterEach(() => { cleanup(); sessionStorage.clear(); });
beforeEach(() => {
  calls.length = 0;
  posted.length = 0;
  dxDeferred = true;
  hasDx21 = true;
  blind21 = { imageId: 21, round: 1, gradable: true, grade: 0, createdAt: "2026-09-20T01:02:00Z" };
  sessionStorage.setItem("diacompanion.token", "t");
  (globalThis as any).fetch = vi.fn(async (url: string, init: any) => {
    const u = String(url);
    if (u.includes("/content") || u.includes(".csv"))
      return new Response(new Blob(["a,b\n1,2\n"], { type: "text/csv" }), { status: 200 });
    const body = route(u, init);
    if (body === undefined) return new Response(null, { status: 204 });
    return new Response(JSON.stringify(body), { status: 200, headers: { "Content-Type": "application/json" } });
  });
  (URL as any).createObjectURL = () => "blob:x";
  (URL as any).revokeObjectURL = () => {};
});
const as = (roles: string[]) =>
  sessionStorage.setItem("diacompanion.user", JSON.stringify({ userId: 1, fullName: "NCV", role: roles[0], roles, mustChangePassword: false, defaultRoute: "/triage" }));
const app = (path: string) =>
  render(<MemoryRouter initialEntries={[path]}><AuthProvider><DataProvider><ToastProvider><LiteRoutes /></ToastProvider></DataProvider></AuthProvider></MemoryRouter>);

describe("Lite — bác sĩ (Doctor + Receptionist)", () => {
  beforeEach(() => as(["Doctor", "Receptionist"]));

  it("landing cũ /triage về danh sách bệnh nhân, không gọi API đã bỏ", async () => {
    app("/triage");
    await screen.findByText("BN001");
    expect(calls.some((c) => c.includes("/api/engagement") || c.includes("/api/triage"))).toBe(false);
  });
  it("có lượt mở → mặc định tab Lượt hiện tại: tiến độ, chỉ số, hai mắt, đóng lượt bị khóa", async () => {
    app("/cases/7");
    await screen.findByText("Chỉ số lâm sàng");
    await screen.findByText("Tiến độ lượt");
    await screen.findByText(/Kéo thả ảnh OS/);
    await screen.findByText(/Cần xem kỹ: Bất đồng cao/);
    await screen.findByText("Xem kỹ để xác nhận →");
    expect(screen.queryByText(/Đồng ý:/)).toBeNull(); // ca cần xem kỹ không xác nhận nhanh
    const close = (await screen.findAllByText("Đóng lượt")).find((el) => el.tagName === "BUTTON") as HTMLButtonElement;
    expect(close.disabled).toBe(true);
    await screen.findByText(/Mắt phải \(OD\): cần xem kỹ để xác nhận/);
  });
  it("ca thường quy: xác nhận ngay trên thẻ gọi approve", async () => {
    dxDeferred = false;
    app("/cases/7");
    fireEvent.click(await screen.findByText(/Đồng ý: Không có DR/));
    await waitFor(() => expect(calls).toContain("POST /api/triage/31/approve"));
  });
  it("tab Hồ sơ bệnh án: hành chính, tóm tắt, các lần khám AI → BS", async () => {
    app("/cases/7");
    fireEvent.click(await screen.findByRole("tab", { name: "Hồ sơ bệnh án" }));
    await screen.findByText("Hồ sơ bệnh án — BN001");
    await screen.findByText("I. Hành chính");
    await screen.findByText(/8\.4% \(/);          // HbA1c gần nhất từ lượt mới
    await screen.findByText(/150\/90 mmHg/);       // HA gần nhất từ lượt cũ
    await screen.findByText("V. Các lần khám (2)");
    expect(screen.queryByText(/Kết luận:/)).toBeNull(); // lượt cũ đang thu gọn
    fireEvent.click(document.querySelector(".lite-visit-h[aria-expanded=\"false\"]")!); // mở lượt cũ
    expect((await screen.findAllByText("DR trung bình")).length).toBe(2); // chấm mù + quyết định lâm sàng
    await screen.findByText(/Phê duyệt/);
    await screen.findByText(/Kết luận:/);
    expect(calls.some((c) => c.includes("/api/diagnoses/progression"))).toBe(false);
  });
  it("ảnh chưa chấm mù: phải chấm trước, lưu gọi round1 rồi mới chạy AI", async () => {
    hasDx21 = false;
    blind21 = undefined;
    app("/cases/7");
    // Không chấm ngay trên thẻ: phải mở màn xem ảnh (phóng to/kéo thả + thông tin lâm sàng).
    expect(screen.queryByText(/phân độ của bác sĩ trước khi xem AI/)).toBeNull();
    fireEvent.click(await screen.findByText(/Xem ảnh & chấm mù/));
    await screen.findByText(/phân độ của bác sĩ trước khi xem AI/);
    await screen.findByText("8.2 %");
    await screen.findByText("150/90 mmHg");
    await screen.findByText(/đã laser mắt trái/);
    expect(screen.getAllByText("100%").length).toBeGreaterThan(0); // khung xem phóng to
    const save = (await screen.findByText("Lưu phân độ & chạy AI")).closest("button") as HTMLButtonElement;
    expect(save.disabled).toBe(true);
    fireEvent.click(screen.getAllByRole("radio")[1]); // DR nhẹ
    expect(save.disabled).toBe(false);
    fireEvent.click(save);
    await waitFor(() => expect(calls).toContain("POST /api/diagnoses/run/21"));
    const i = calls.indexOf("POST /api/grading/round1/21");
    expect(i).toBeGreaterThan(-1);
    expect(i).toBeLessThan(calls.indexOf("POST /api/diagnoses/run/21"));
    expect(posted.find((p) => p.path === "/api/grading/round1/21")!.body).toMatchObject({ gradable: true, grade: 1 });
  });
  it("tạo hồ sơ ẩn danh: mã nghiên cứu, năm sinh, SĐT giả 00…", async () => {
    app("/cases/new");
    const inputs = await screen.findAllByRole("textbox");
    fireEvent.change(inputs[0], { target: { value: "NC-0012" } });
    fireEvent.change(document.querySelector('input[type="number"]')!, { target: { value: "1968" } });
    fireEvent.click(screen.getByText("Tạo hồ sơ"));
    await waitFor(() => expect(posted.some((p) => p.path === "/api/patients")).toBe(true));
    const body = posted.find((p) => p.path === "/api/patients")!.body;
    expect(body.fullName).toBe("NC-0012");
    expect(body.dateOfBirth).toBe("1968-07-01");
    expect(body.phone).toMatch(/^00\d{9}$/);
    expect(screen.queryByText("Số điện thoại")).toBeNull();
  });
  it("bác sĩ KHÔNG vào được /export", async () => {
    app("/export");
    await screen.findByText(/Cần vai trò Nghiên cứu/);
  });
});

describe("Lite — người chấm độc lập (Grader)", () => {
  beforeEach(() => as(["Grader"]));

  it("vào thẳng trang chấm, không thấy bệnh nhân/AI, lưu gọi vòng 2", async () => {
    app("/khong-co");
    await screen.findByText(/Ảnh 1 — còn 1 ảnh/);
    // Từ 1.3.0: người chấm chỉ thấy ảnh, không tải và không hiện thông tin lâm sàng.
    expect(screen.queryByText("Thông tin lâm sàng")).toBeNull();
    expect(screen.queryByText("8.2 %")).toBeNull();
    expect(screen.queryByText("Mã nghiên cứu")).toBeNull();
    expect(calls.some((c) => c.includes("/context"))).toBe(false);
    screen.getByText("Red-free");
    expect(screen.queryByText("Thêm bệnh nhân")).toBeNull();
    expect(calls.some((c) => c.includes("/api/diagnoses") || c.includes("/api/patients"))).toBe(false);
    fireEvent.click(screen.getAllByRole("radio")[3]); // DR nặng
    fireEvent.click(screen.getByText("Lưu & sang ảnh tiếp"));
    await waitFor(() => expect(posted.some((p) => p.path === "/api/grading/round/2/21")).toBe(true));
    expect(posted.find((p) => p.path === "/api/grading/round/2/21")!.body).toMatchObject({ gradable: true, grade: 3 });
  });
  it("không vào được khu lâm sàng", async () => {
    app("/cases");
    await screen.findByText(/Cần đồng thời hai vai trò/);
  });
});

describe("Lite — nghiên cứu (Research)", () => {
  beforeEach(() => as(["Research"]));

  it("đăng nhập xong rơi vào /export; menu chỉ có Kết xuất", async () => {
    app("/cases");            // vào khu lâm sàng
    await screen.findByText(/Cần đồng thời hai vai trò/);
    expect(screen.queryByText("Thêm bệnh nhân")).toBeNull();
  });
  it("trang kết xuất có tiến độ chấm và tải bảng chấm mù", async () => {
    app("/export");
    await screen.findByText("bất đồng cần phân xử");
    fireEvent.click(screen.getByText("Tải bảng chấm mù CSV"));
    await waitFor(() => {
      const urls = ((globalThis as any).fetch as any).mock.calls.map((c: any[]) => String(c[0]));
      expect(urls.some((u: string) => u.includes("/api/research/grading.csv"))).toBe(true);
    });
  });
  it("/ và route lạ chuyển về /export, tải CSV gọi đúng endpoint + khoảng ngày", async () => {
    app("/khong-co");
    await screen.findByText("Kết xuất dữ liệu nghiên cứu");
    const dates = document.querySelectorAll('input[type="date"]');
    fireEvent.change(dates[0], { target: { value: "2026-01-01" } });
    fireEvent.change(dates[1], { target: { value: "2026-09-30" } });
    fireEvent.click(screen.getByText("Tải dataset CSV"));
    await waitFor(() => expect(calls.length + ((globalThis as any).fetch as any).mock.calls.length).toBeGreaterThan(0));
    const urls = ((globalThis as any).fetch as any).mock.calls.map((c: any[]) => String(c[0]));
    expect(urls.some((u: string) => u.includes("/api/research/dataset.csv?from=2026-01-01&to=2026-09-30"))).toBe(true);
  });
  it("Từ ngày sau Đến ngày bị chặn ở client", async () => {
    app("/export");
    await screen.findByText("Kết xuất dữ liệu nghiên cứu");
    const dates = document.querySelectorAll('input[type="date"]');
    fireEvent.change(dates[0], { target: { value: "2026-10-01" } });
    fireEvent.change(dates[1], { target: { value: "2026-09-01" } });
    fireEvent.click(screen.getByText("Tải dataset CSV"));
    await screen.findByText(/Khoảng ngày không hợp lệ/);
    const urls = ((globalThis as any).fetch as any).mock.calls.map((c: any[]) => String(c[0]));
    expect(urls.some((u: string) => u.includes("dataset.csv"))).toBe(false);
  });
});
