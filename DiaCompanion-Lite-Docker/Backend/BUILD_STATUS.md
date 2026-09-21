# Trạng thái restore / build

## Kết quả thực thi trong môi trường hiện tại

Đã thử chạy trực tiếp các lệnh:

```bash
dotnet --info
dotnet restore DiaCompanion.sln
dotnet build DiaCompanion.sln -c Debug --no-restore
```

Môi trường xử lý hiện tại **không có .NET SDK / lệnh `dotnet`**, nên cả ba lệnh đều dừng với mã `127` (`dotnet: command not found`). Nhật ký nguyên văn nằm trong `BUILD_ATTEMPT.log`.

Vì vậy không ghi nhận sai rằng project đã được compiler xác nhận build thành công.

## Kiểm tra thay thế đã chạy

Trong giới hạn môi trường không có SDK, source đã được kiểm tra tĩnh để giảm lỗi compile phổ biến:

- Service/Controller không còn phụ thuộc EF Core hoặc `AppDbContext`.
- Không còn call trực tiếp `DbSet`, `SaveChangesAsync`, `Entry`, `DatabaseFacade` từ Service/Controller.
- Toàn bộ method `IRepository` được Service/Controller sử dụng đều tồn tại trong contract.
- Toàn bộ method `Task` trong các contract Repository có implementation tương ứng trong `EfRepository.*`.
- Không còn tham chiếu role enum cũ hoặc RoleId số cố định trong source C#.
- Không có tích hợp MedicalRecords trong source C#.
- Đã kiểm tra cấu trúc ngoặc và chuỗi/comment của toàn bộ file `.cs`; không phát hiện cấu trúc chưa đóng.

## Lệnh cần chạy trên máy có .NET 8 SDK

Tại thư mục `Backend`:

```bash
dotnet restore DiaCompanion.sln
dotnet build DiaCompanion.sln -c Debug --no-restore
```

Nếu compiler báo lỗi khi chạy trên máy có .NET 8 SDK, cần dùng chính output compiler đó để xử lý tiếp; file này không thay thế cho việc build thật.
