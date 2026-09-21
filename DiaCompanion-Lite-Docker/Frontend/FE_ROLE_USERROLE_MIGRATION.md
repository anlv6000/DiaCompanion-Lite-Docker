# Cập nhật Frontend theo Roles / UserRoles

Frontend đã được rà soát để không còn phụ thuộc vào `Users.Role` hoặc RoleId số cố định.

## Các thay đổi chính

- `Role` dùng tên (`Admin`, `Doctor`, `Receptionist`, `Patient`) thay vì số 0/1/4.
- `LoginResponse` và `StaffUserDto` hỗ trợ `roles[]` cho một User nhiều role; `role` chỉ còn trường tương thích.
- Route guard, menu, permission và landing page kiểm toàn bộ `roles[]`.
- Tạo/cập nhật nhân viên gửi `roles[]`; cập nhật có thể chọn nhiều role nhân viên.
- Danh sách nhân viên hiển thị toàn bộ role active.
- Tạo Patient có thể liên kết User có sẵn; khi chọn User, họ tên bị khóa, phone cũng bị khóa nếu User đã có phone.
- Danh sách Patient và User dùng metadata phân trang do backend trả (`page`, `pageSize`, `totalItems`, `totalPages`, `rangeLabel`).
- `Pagination` được làm an toàn với trang vượt giới hạn.
- Hiển thị chi tiết lỗi validation 400 từ ASP.NET Core thay vì chỉ `Yêu cầu thất bại (400)`.

## API kỳ vọng

### POST /api/users

```json
{
  "email": "doctor@example.com",
  "fullName": "Nguyễn Văn A",
  "role": "Doctor",
  "roles": ["Doctor"],
  "licenseNo": "CCHN-001"
}
```

`role` được gửi để tương thích; `roles` là nguồn chính cho backend mới.

### Login / Me

Frontend ưu tiên `roles`, nhưng vẫn tự chuẩn hóa nếu backend cũ chỉ trả `role`.
