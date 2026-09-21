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
  if (path === "/api/diagnoses/by-image/21") return [dx];
  if (path === "/api/diagnoses/by-image/15") return [dxOld];
  if (path.startsWith("/api/diagnoses/progression/7")) return { points: [{ date: "2026-06-01", visitId: 9, confirmedGrade: 2, fractalDimension: 1.4, hbA1c: 9.1 }], trendWarning: null };
  return {};
}

afterEach(() => { cleanup(); sessionStorage.clear(); });
beforeEach(() => {
  calls.length = 0;
  sessionStorage.setItem("diacompanion.token", "t");
  (globalThis as any).fetch = vi.fn(async (url: string, init: any) => {
    const u = String(url);
    if (u.includes("/content") || u.includes("dataset.csv"))
      return new Response(new Blob(["a,b\n1,2\n"], { type: "text/csv" }), { status: 200 });
    return new Response(JSON.stringify(route(u, init)), { status: 200, headers: { "Content-Type": "application/json" } });
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
  it("tab Lượt hiện tại: chỉ số, hai mắt, tóm tắt AI, đóng lượt", async () => {
    app("/cases/7");
    await screen.findByText(/1\. Chỉ số lâm sàng/);
    await screen.findByText(/Mắt phải/); await screen.findByText(/Mắt trái/);
    await screen.findByText("Nạp ảnh OS");
    await screen.findByText("Bất đồng cao");
    await screen.findByText("3. Đóng lượt");
  });
  it("tab Kết quả đã xác nhận chỉ liệt kê kết quả đã duyệt", async () => {
    app("/cases/7");
    fireEvent.click(await screen.findByRole("tab", { name: "Kết quả đã xác nhận" }));
    await screen.findByText("Phê duyệt");
    expect(screen.queryByText("Bất đồng cao")).toBeNull(); // ca chưa xác nhận không có ở đây
  });
  it("tab Diễn tiến gọi API progression", async () => {
    app("/cases/7");
    fireEvent.click(await screen.findByRole("tab", { name: "Diễn tiến" }));
    await screen.findByText("Bảng số liệu đã xác nhận");
    await waitFor(() => expect(calls.some((c) => c.startsWith("GET /api/diagnoses/progression/7"))).toBe(true));
  });
  it("tab Chỉ số theo thời gian gom HbA1c/HA theo lượt", async () => {
    app("/cases/7");
    fireEvent.click(await screen.findByRole("tab", { name: "Chỉ số theo thời gian" }));
    await screen.findByText("9.1");
    await screen.findByText("150");
    await screen.findByText("90");
  });
  it("bác sĩ KHÔNG vào được /export", async () => {
    app("/export");
    await screen.findByText(/Cần vai trò Nghiên cứu/);
  });
});

describe("Lite — nghiên cứu (Research)", () => {
  beforeEach(() => as(["Research"]));

  it("đăng nhập xong rơi vào /export; menu chỉ có Kết xuất", async () => {
    app("/cases");            // vào khu lâm sàng
    await screen.findByText(/Cần đồng thời hai vai trò/);
    expect(screen.queryByText("Thêm bệnh nhân")).toBeNull();
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
