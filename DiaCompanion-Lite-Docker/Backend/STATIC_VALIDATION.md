# Static validation

Các kiểm tra đã chạy sau refactor:

- Không còn tham chiếu code tới `User.IsActive`, `Users.IsActive` hoặc `AuthorizationSnapshot.UserIsActive`.
- Không còn method cũ `GetActiveUserByIdAsync`, `GetActiveUserByPhoneAsync`, `ActiveUserPhoneExists*`, `SyncUserRolesAsync`, `SetUserRolesActiveAsync`.
- Service chỉ gọi các method đã khai báo trên các partial `IRepository`.
- `SetActive()` chỉ gọi `SetStaffRoleActiveAsync()` và không sửa role Patient.
- `VoidPatient` chỉ deactivate `UserRoles.Patient`.

Không thể chạy `dotnet build` trong môi trường xử lý vì executable `dotnet` không được cài đặt. Hãy chạy trên máy dev:

```bash
dotnet restore
dotnet build DiaCompanion.sln
```
