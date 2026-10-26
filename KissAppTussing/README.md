# Kết nối SQL Server Express

Ứng dụng dùng SQL Server Express tại `localhost\SQLEXPRESS` với Windows Authentication. Connection string mặc định trong `appsettings.json`:

```text
Server=.\SQLEXPRESS;Database=KissAppTussing;Trusted_Connection=True;TrustServerCertificate=True;MultipleActiveResultSets=True
```

Mở dịch vụ SQL Server Express, rồi chạy ứng dụng từ thư mục dự án:

```powershell
dotnet run
```

Khi khởi động, EF Core tự tạo database `KissAppTussing`, các bảng và danh sách sở thích mặc định. Trong SSMS, kết nối tới `localhost\SQLEXPRESS` bằng Windows Authentication rồi refresh mục **Databases**.

Nếu cần chạy migration thủ công:

```powershell
dotnet ef database update
```

Tài khoản Windows chạy ứng dụng cần quyền tạo database trên SQL Server. File SQLite cũ `kissapp.db` vẫn được giữ nguyên, nhưng dữ liệu trong file đó không tự chuyển sang SQL Server.

## Tin nhắn

Chỉ hai tài khoản đã kết đôi mới có thể nhắn tin. Chat hỗ trợ tin nhắn văn bản, PNG/JPEG/WebP tối đa 8 MB, MP4/WebM tối đa 20 MB và một bộ emoji cơ bản. Tin nhắn được lưu trong bảng `ChatMessages`; tệp đính kèm nằm trong `App_Data/chat` và chỉ người trong cuộc trò chuyện mới tải được. Giao diện kiểm tra tin nhắn mới mỗi 3 giây khi đang mở cuộc trò chuyện.
