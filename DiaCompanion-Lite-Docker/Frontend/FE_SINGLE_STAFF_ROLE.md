# Điều chỉnh màn hình tài khoản nhân viên về một vai trò chính

## Mục tiêu

Màn hình **Tạo/Cập nhật tài khoản nhân viên** chỉ cho Admin chọn **một vai trò nhân viên chính** cho mỗi tài khoản:

- Admin
- Doctor
- Receptionist

Frontend không còn cho tick đồng thời `Doctor + Receptionist` hoặc `Admin + Doctor` trên màn hình này.

## Lý do

`Roles` + `UserRoles` vẫn được giữ ở backend để hỗ trợ mô hình nhiều vai trò khi cần, đặc biệt trường hợp một User nhân viên (ví dụ Doctor) đồng thời được liên kết với hồ sơ Patient. Tuy nhiên nghiệp vụ tạo nhân viên không nên cho phép một email nhân viên nhận nhiều role staff cùng lúc nếu hệ thống không có nhu cầu đó.

Do đó:

- Tạo nhân viên: dropdown chọn đúng 1 role staff.
- Cập nhật nhân viên: dropdown chọn đúng 1 role staff.
- Request vẫn gửi `roles: [selectedRole]` để tương thích backend `UserRoles`.
- Nếu User đồng thời có role `Patient`, frontend không đưa role đó vào dropdown. Backend cần bảo toàn role Patient khi cập nhật staff role.

## File đã sửa

- `src/pages/UsersPage.tsx`
- `src/types/api.ts`

## Kiểm tra

Đã chạy:

```bash
node node_modules/typescript/bin/tsc -b --pretty false
```

Kết quả: PASS.
