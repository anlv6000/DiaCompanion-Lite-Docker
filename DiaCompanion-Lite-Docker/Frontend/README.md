# DiaCompanion — Console lâm sàng (Web bệnh viện)

Frontend React chuẩn (Vite + npm + TypeScript + react-router-dom) cho hệ thống
sàng lọc bệnh võng mạc đái tháo đường. Đây là **web dành cho nhân viên bệnh
viện** (Bác sĩ / Điều dưỡng / Quản trị) — không phải app bệnh nhân.

## Chạy

```bash
npm install
npm run dev        # http://localhost:5173  (proxy /api -> localhost:5080)
npm run build      # tsc -b && vite build  -> dist/
npm run typecheck  # kiểm tra kiểu, không phát sinh file
```

Backend .NET chạy ở `localhost:5080`. Đổi địa chỉ khi deploy trong
`public/config.js` (không cần build lại) — xem mục Cấu hình bên dưới.

## Năm điều đã làm theo yêu cầu

1. **Contexts đầy đủ** — `src/contexts/` gồm `AuthContext` (phiên đăng nhập),
   `DataContext` (dữ liệu), `ToastContext` (thông báo).

2. **Dữ liệu từ backend vào DataContext trước** — `DataContext` là cửa DUY NHẤT
   tới backend. Mọi nhóm nghiệp vụ (patients, visits, images, diagnoses,
   triage, prescriptions, appointments, monitoring, engagement, blog, admin,
   exports…) được bọc tại đây. Không page nào import `@/api/services` trực tiếp.

3. **Component không lấy dữ liệu, chỉ page gọi DataContext** — các page gọi
   `useData()` rồi truyền dữ liệu xuống component qua props. Component trong
   `src/components/` (ui, charts) chỉ nhận props, không tự fetch.

4. **`routes.tsx` quản lý toàn bộ route** — `src/routes.tsx` dùng
   `react-router-dom`, gác đăng nhập, ép đổi mật khẩu tạm, và phân quyền theo
   vai trò cho từng đường dẫn (kèm màn "Không đủ quyền").

5. **`API_BASE` đẩy ra folder config** — `src/config/index.ts` giữ `API_BASE`,
   `STORAGE_KEYS`, `DEFAULT_ROUTE`. `client.ts` và mọi nơi khác import từ đây,
   không đọc `window` trực tiếp, không hardcode URL rải rác.

Ngoài ra: **bỏ đăng nhập bệnh nhân** — chỉ còn đăng nhập nhân viên bằng email
(đã gỡ OTP / đăng nhập bằng số điện thoại / quên mật khẩu tự phục vụ). Quên mật
khẩu do Admin cấp lại trong màn Tài khoản.

## Ẩn nút theo quyền (khớp `[Authorize(Roles=...)]` của backend)

`src/lib/permissions.ts` là bản đồ quyền phía client, dùng để **ẩn nút** cho
người không có quyền (server vẫn là chốt chặn thật). Đáng chú ý: nhiều nút
**void** Admin KHÔNG được dùng:

| Thao tác                      | Vai trò được phép         |
| ----------------------------- | ------------------------- |
| Void hồ sơ bệnh nhân          | Bác sĩ, Admin             |
| Void lượt khám                | **Chỉ Bác sĩ**            |
| Void ảnh                      | Bác sĩ, Admin             |
| Void kết quả AI (diagnosis)   | Bác sĩ, Admin             |
| Void đơn thuốc                | **Chỉ Bác sĩ**            |
| Void review (triage)          | **Chỉ Bác sĩ**            |
| Phê duyệt / ghi đè kết quả AI | **Chỉ Bác sĩ**            |
| Kê đơn thuốc                  | **Chỉ Bác sĩ**            |
| Nạp ảnh / kiểm chất lượng     | Bác sĩ, Điều dưỡng, Admin |
| Cấp lại mật khẩu bệnh nhân    | Bác sĩ, Điều dưỡng, Admin |

## Cấu trúc

```
src/
  config/        API_BASE + hằng số tập trung (yêu cầu 5)
  api/           client.ts (HTTP), services.ts (endpoint thô)
  contexts/      Auth, Data (cửa backend), Toast (yêu cầu 1,2)
  components/    ui.tsx, charts.tsx, AppShell.tsx — nhận props (yêu cầu 3)
  pages/         mọi màn hình, chỉ page gọi useData() (yêu cầu 3)
  lib/           hooks (useAsync/useDebounce), format, enums, permissions
  types/         api.ts — DTO khớp backend
  routes.tsx     bảng route + phân quyền (yêu cầu 4)
  app/App.tsx    thứ tự provider: Router → Auth → Data → Toast
  main.tsx       điểm vào
```

## Cấu hình khi deploy

`public/config.js`:

```js
window.__DIACOMPANION_API__ = "https://api.benhvien.example";
```

File này nạp trước bundle nên đổi origin backend không cần build lại.
