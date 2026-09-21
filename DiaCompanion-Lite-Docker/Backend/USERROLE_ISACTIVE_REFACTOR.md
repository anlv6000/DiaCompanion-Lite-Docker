# Refactor trạng thái tài khoản sang UserRoles.IsActive

## Nghiệp vụ sau khi sửa

- `Users.IsActive` không còn được map trong entity `User` và không còn được backend đọc/ghi.
- Quyền đăng nhập/authorization lấy từ `UserRoles.IsActive && Roles.IsActive`.
- Khóa staff chỉ khóa `Doctor`/`Receptionist` trong `UserRoles`; role `Patient` của cùng User giữ nguyên.
- Thu hồi hồ sơ Patient chỉ tắt `UserRoles.Patient.IsActive`; Doctor/Receptionist của cùng User không bị ảnh hưởng.
- Danh sách `/api/Users` lấy cả staff role active và inactive để tài khoản đã khóa vẫn xuất hiện; loại Patient-only và Admin đang active.
- `StaffUserDto.IsActive` được map từ `UserRoles.IsActive`, không phải từ Users.

## Các file chính đã sửa

- `Entities/User.cs`: bỏ property `IsActive` khỏi entity.
- `Repositories/EfRepository.Users.cs`: list/filter staff, đổi role, khóa/mở staff đều dùng UserRoles.
- `Repositories/IRepository.Users.cs`: đồng bộ interface theo dữ liệu role có trạng thái.
- `Services/UsersService.cs`: sửa Create/Update/SetActive/MapStaff; mỗi staff chỉ có một role Doctor hoặc Receptionist.
- `Repositories/EfRepository.Auth.cs` + `IRepository.Auth.cs`: auth snapshot không còn `UserIsActive`.
- `Services/AuthService.cs` + `Program.cs`: login/JWT chỉ yêu cầu còn ít nhất một role active.
- `Repositories/EfRepository.Patients.cs` + `PatientsService.cs`: không dùng Users.IsActive; Phone unique theo User.
- `Services/VoidService.cs`: void Patient chỉ deactivate role Patient.
- `Repositories/EfRepository.Reception.cs`: bác sĩ trực được kiểm bằng role Doctor active.
- `Data/AppDbContext.cs`: unique Email/Phone không còn filter theo Users.IsActive.

## Database

Chạy `DatabasePatches/2026-UserRole-IsActive-Only.sql` để đổi unique index Email/Phone.
Script sẽ dừng nếu dữ liệu hiện tại có Email hoặc Phone bị trùng giữa nhiều User.

Cột `dbo.Users.IsActive` có thể giữ lại trong DB như legacy column. Backend mới không map và không sử dụng cột này.
