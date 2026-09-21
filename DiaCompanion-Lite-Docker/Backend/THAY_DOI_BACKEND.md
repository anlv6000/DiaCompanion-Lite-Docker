# Nhật ký refactor Backend DiaCompanion

## 1. Mục tiêu của lần sửa này

Lần sửa này tập trung đúng vào ranh giới `Service -> Repository` và cơ chế phân quyền mới theo `Roles` / `UserRoles` trong cơ sở dữ liệu.

Nguyên tắc sau refactor:

- `Controller` giữ `[Authorize(Roles = ...)]` để phân quyền endpoint.
- `Service` chỉ xử lý nghiệp vụ, kiểm tra dữ liệu đầu vào, điều phối luồng xử lý và gọi `IRepository`.
- `Service` và `Controller` **không** nhận `AppDbContext`, không sử dụng `DbSet`, `IQueryable`, `Include`, `AsNoTracking`, `ToListAsync`, `FirstOrDefaultAsync`, `AnyAsync`, `SaveChangesAsync` hoặc `DatabaseFacade`.
- Toàn bộ LINQ truy vấn cơ sở dữ liệu nằm trong `Repositories/EfRepository.*.cs`.
- Một `EfRepository` scoped dùng chung `AppDbContext` trong request và đồng thời đóng vai trò Unit of Work.
- Không dùng `UserRole` enum để quyết định quyền và không gắn cứng `RoleId` trong business logic.
- Một `User` có thể có nhiều role đang hoạt động.
- `MedicalRecords` **chưa được tích hợp vào backend trong lần này** theo yêu cầu. Không thêm entity, `DbSet`, configuration hay `MedicalRecordId` vào `Visit`.

## 2. Kiến trúc Repository / Unit of Work

### `Repositories/IRepository.cs`

Đã bỏ toàn bộ việc expose trực tiếp:

- `DbSet<T>`
- `DatabaseFacade`
- `EntityEntry<T>`
- `SaveChangesAsync`

`IRepository` hiện chỉ expose các thao tác persistence ở mức abstraction và các method nghiệp vụ truy vấn được định nghĩa trong các file partial `IRepository.*.cs`.

### `Repositories/IUnitOfWork.cs`

Thêm abstraction Unit of Work gồm:

- `CommitAsync`
- `TryCommitAsync`
- `ExecuteInTransactionAsync`
- `ExecuteInTransactionAsync<TResult>`

`IRepository : IUnitOfWork`, do đó Service vẫn chỉ cần inject một `IRepository`; không thay đổi kiểu DI đang dùng.

### `Repositories/EfRepository.cs`

Đây là implementation duy nhất giữ `AppDbContext`. File này thực hiện:

- `Add`, `AddRange`, `Remove`
- optimistic concurrency qua `ApplyOriginalRowVersion`
- commit
- kiểm tra concurrency
- transaction
- execution strategy của EF Core

Các file `EfRepository.*.cs` chứa toàn bộ LINQ/EF truy vấn của từng module.

## 3. Repository đã tách cho toàn bộ module

Đã thêm interface + implementation Repository theo module:

- `IRepository.Admin.cs` / `EfRepository.Admin.cs`
- `IRepository.Auth.cs` / `EfRepository.Auth.cs`
- `IRepository.Blog.cs` / `EfRepository.Blog.cs`
- `IRepository.Diagnoses.cs` / `EfRepository.Diagnoses.cs`
- `IRepository.Engagement.cs` / `EfRepository.Engagement.cs`
- `IRepository.Export.cs` / `EfRepository.Export.cs`
- `IRepository.Images.cs` / `EfRepository.Images.cs`
- `IRepository.Monitoring.cs` / `EfRepository.Monitoring.cs`
- `IRepository.Patients.cs` / `EfRepository.Patients.cs`
- `IRepository.Prescriptions.cs` / `EfRepository.Prescriptions.cs`
- `IRepository.Reception.cs` / `EfRepository.Reception.cs`
- `IRepository.Recheck.cs` / `EfRepository.Recheck.cs`
- `IRepository.Reminders.cs` / `EfRepository.Reminders.cs`
- `IRepository.Support.cs` / `EfRepository.Support.cs`
- `IRepository.Triage.cs` / `EfRepository.Triage.cs`
- `IRepository.Users.cs` / `EfRepository.Users.cs`
- `IRepository.Visits.cs` / `EfRepository.Visits.cs`
- `IRepository.Void.cs` / `EfRepository.Void.cs`

Các Service tương ứng đã bỏ EF/LINQ truy vấn database trực tiếp và chuyển sang gọi các method trên Repository.

## 4. Role / UserRole và hỗ trợ một User nhiều Role

### Entity và EF mapping

Đã thêm:

- `Entities/Role.cs`
- `Entities/UserRole.cs`

Đã sửa:

- `Entities/User.cs`: bỏ thuộc tính role enum cũ, thêm navigation `UserRoles`.
- `Data/AppDbContext.cs`: thêm `DbSet<Role>` và `DbSet<UserRole>`, cấu hình khóa ghép `(UserId, RoleId)`, navigation và index theo schema hiện tại.
- `Common/Enums.cs`: bỏ enum role cũ.
- `Common/Roles.cs`: chỉ giữ **tên role nghiệp vụ** (`Admin`, `Doctor`, `Receptionist`, `Patient`) và các chuỗi dùng cho `[Authorize]`; không chứa `RoleId` số.
- `Common/CurrentUser.cs`: đọc toàn bộ role claim thay vì một role duy nhất.

`RoleId` chỉ được Repository sử dụng sau khi đã truy vấn `Roles.Name` từ database để lấy đúng khóa ngoại. Service không so sánh hay gắn cứng `RoleId`.

## 5. Login, refresh token và authorization

### Login

`AuthService` gọi Repository để lấy `User` và danh sách role hiện đang active. User chỉ đăng nhập thành công khi:

- có ít nhất một `UserRoles.IsActive = 1`
- role tương ứng có `Roles.IsActive = 1`

`Users.IsActive` không còn được backend map hoặc dùng để quyết định trạng thái tài khoản. Email/Phone được coi là định danh duy nhất của `User` bất kể role đang khóa hay mở.

### JWT access token

`JwtTokenService` ghi **nhiều** `ClaimTypes.Role`, một claim cho mỗi role active.

### Refresh token

Thêm:

- `Dtos/RefreshTokenRequest.cs`
- endpoint `POST /api/auth/refresh`

Khi refresh, backend không tin role cũ trong token. Backend lấy lại User + role active từ Repository trước khi phát access token mới.

### Authorization mỗi request

Trong `Program.cs`, `JwtBearerEvents.OnTokenValidated` truy vấn lại trạng thái hiện tại của User và Roles/UserRoles.

Các role claim trong JWT được xóa và thay bằng danh sách role **đang active trong database tại thời điểm request**. Sau đó `[Authorize(Roles = ...)]` ở Controller mới thực hiện kiểm quyền.

Kết quả: nếu tắt `UserRoles.IsActive` hoặc `Roles.IsActive` thì role tương ứng bị gỡ khỏi principal ở request kế tiếp, không cần chờ JWT hết hạn. Nếu User không còn role active nào thì request bị từ chối.

## 6. Quản lý nhân viên nhiều Role

Đã sửa:

- `Dtos/CreateStaffRequest.cs`
- `Dtos/UpdateStaffRequest.cs`
- `Dtos/StaffUserDto.cs`
- `Services/UsersService.cs`
- `Repositories/IRepository.Users.cs`
- `Repositories/EfRepository.Users.cs`
- `Controllers/UsersController.cs`

API giữ cả `Role` và `Roles` để tương thích client cũ, nhưng màn quản lý staff chỉ chấp nhận đúng **một** role: `Doctor` hoặc `Receptionist`.

Khi sửa/khóa tài khoản nhân viên, API chỉ quản lý `Doctor`/`Receptionist`. Role `Patient` của cùng User được giữ nguyên. Admin không nằm trong danh sách staff này.

Việc gán role thực hiện theo quy trình:

1. Service nhận tên role và kiểm business rule.
2. Repository truy vấn `Roles` theo `Name` và `IsActive`.
3. Repository lấy `Role.Id` thực tế từ DB.
4. Repository bật/tắt các dòng `UserRoles` tương ứng.

Không có đoạn business logic kiểu `RoleId == 0`, `RoleId == 1`, ...

## 7. Receptionist

`ReceptionController` trước đây truy cập `AppDbContext` trực tiếp đã được tách thành:

- `Controllers/ReceptionController.cs`: chỉ nhận `IReceptionService`.
- `Services/IReceptionService.cs`
- `Services/ReceptionService.cs`: business logic.
- `Repositories/IRepository.Reception.cs`
- `Repositories/EfRepository.Reception.cs`: LINQ/EF và database access.

Phân quyền Receptionist vẫn đặt tại Controller.

`PatientsController.Create` và `VisitsController.Create` vẫn dành cho `Receptionist` theo yêu cầu hiện tại.

## 8. Các Service đã bỏ truy cập EF/DbContext trực tiếp

Đã refactor các file chính sau:

- `Services/AdherenceService.cs`
- `Services/AdminService.cs`
- `Services/AuditService.cs`
- `Services/AuthService.cs`
- `Services/BlogService.cs`
- `Services/ClinicalReminderWorker.cs`
- `Services/ConfigService.cs`
- `Services/DiagnosesService.cs`
- `Services/EngagementService.cs`
- `Services/ExportService.cs`
- `Services/ImagesService.cs`
- `Services/MonitoringService.cs`
- `Services/NotificationService.cs`
- `Services/OtpService.cs`
- `Services/PatientsService.cs`
- `Services/PrescriptionsService.cs`
- `Services/RecheckService.cs`
- `Services/TriageService.cs`
- `Services/UsersService.cs`
- `Services/VisitsService.cs`
- `Services/VoidService.cs`

`ClinicalReminderWorker` cũng không còn tự query `DbContext`; worker tạo scope và gọi Repository.

## 9. Optimistic concurrency

Đã chuyển thao tác set original `RowVer` vào Repository để Service không cần `EntityEntry`/EF Core.

Các file liên quan:

- `Common/ConcurrencyExtensions.cs`
- `Common/RowVersionCodec.cs`
- `Repositories/IRepository.cs`
- `Repositories/EfRepository.cs`

## 10. DI

Giữ kiểu DI hiện tại:

```csharp
builder.Services.AddDbContext<AppDbContext>(...);
builder.Services.AddScoped<IRepository, EfRepository>();
```

`AppDbContext` chỉ được inject vào `EfRepository`. Application Service inject `IRepository`.

Các Service mới/tách lại như `IReceptionService` / `ReceptionService` được đăng ký scoped như các Service khác.

## 11. MedicalRecords

**Không triển khai MedicalRecords trong lần sửa này.**

Cụ thể source C# hiện tại không có:

- entity `MedicalRecord`
- `DbSet<MedicalRecord>`
- EF configuration cho MedicalRecord
- `Visit.MedicalRecordId`
- nghiệp vụ tự tạo MedicalRecord khi tạo Patient

Phần schema MedicalRecords được giữ nguyên ở database và sẽ xử lý ở bước riêng sau.

## 12. File ghi chú cũ đã bỏ

Đã bỏ hai file ghi chú trong `Controllers` vì chứa hướng dẫn cũ / RoleId enum cũ và không phải source runtime:

- `Controllers/DbContextChanges.cs`
- `Controllers/RolesToAdd.cs`

## 13. Kiểm tra tĩnh đã thực hiện

Đã quét source để xác nhận:

- không có `Microsoft.EntityFrameworkCore` / `AppDbContext` / `DbSet` / `IQueryable` / async LINQ EF trong `Services` và `Controllers`;
- không còn `UserRole.Admin`, `UserRole.Doctor`, `UserRole.Receptionist`, `UserRole.Patient`;
- không có business logic so sánh `RoleId` với số cố định;
- không có source C# nào tham chiếu `MedicalRecord`, `MedicalRecords`, `MedicalRecordId`;
- các method Repository được Service/Controller gọi đều có khai báo trong contract;
- các method Repository trong contract đều có implementation;
- kiểm tra cân bằng `{}`, `[]`, `()` và trạng thái chuỗi/comment trên toàn bộ source C# không phát hiện lỗi cấu trúc.

Trạng thái `dotnet restore` / `dotnet build` xem tại `BUILD_STATUS.md` và `BUILD_ATTEMPT.log`.
