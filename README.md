# KissAppTussing

KissAppTussing là ứng dụng web giúp người dùng tạo hồ sơ, khám phá và kết nối với những người khác. Dự án gồm giao diện tiếng Việt, API phía máy chủ và cơ sở dữ liệu SQL Server.

## Ứng dụng cung cấp gì?

- **Tài khoản và hồ sơ:** đăng ký, đăng nhập, đăng xuất thu hồi phiên, đổi mật khẩu, xác minh email OTP, quản lý thiết bị và quyền riêng tư.
- **Khám phá:** tìm kiếm, lọc khoảng cách, dùng GPS riêng tư và chọn 10, 20 hoặc 30 hồ sơ mỗi trang.
- **Thích và kết đôi:** Like, Super Like, hoàn tác bỏ qua, hủy kết đôi và xem lại hồ sơ đã thích/bỏ qua.
- **Bạn bè và kết nối:** tìm người dùng, gửi lời mời kết nối, chấp nhận hoặc từ chối lời mời và quản lý các kết nối.
- **Tin nhắn:** trạng thái online/đã xem, thu hồi và xóa tin nhắn ở phía mình, emoji, lời nhắn quà tặng miễn phí, gợi ý mở đầu theo sở thích, ảnh và video.
- **Hành trình:** bài viết, chia sẻ liên kết, trả lời bình luận và story tự ẩn sau 24 giờ.
- **Hẹn hò và cộng đồng:** lời mời, lịch hẹn, nhắc trước 24 giờ, đánh giá buổi hẹn và sự kiện cộng đồng.
- **Thông báo và an toàn:** thông báo trong ứng dụng, tùy chọn theo loại, chặn/báo cáo và công cụ kiểm duyệt.
- **Quản trị:** phân vai trò, xử lý báo cáo, gửi thông báo hệ thống, nhật ký hoạt động và biểu đồ 30 ngày.
- **Quản trị viên:** người được cấp quyền có thể xem số liệu, quản lý tài khoản và tải dữ liệu được phép xuất dưới dạng JSON.
- **Hồ sơ mẫu:** ứng dụng có dữ liệu demo để trình bày giao diện. Các script SQL trong thư mục `scripts` có thể bổ sung hồ sơ mẫu để tổng số tài khoản đạt 120.

## Công nghệ

- **Backend:** ASP.NET Core Minimal API trên .NET 10; xác thực bằng JWT.
- **Giao diện:** HTML, CSS và JavaScript thuần, được phục vụ từ `wwwroot`.
- **Cơ sở dữ liệu:** SQL Server, truy cập qua Entity Framework Core và các migration.
- **API:** OpenAPI được bật trong môi trường Development.

## Cấu trúc dự án

| Thư mục/tệp | Nội dung và vai trò |
| --- | --- |
| `KissAppTussing/` | Mã nguồn chính của ứng dụng web. |
| `KissAppTussing/Endpoints/` | Các API cho tài khoản, hồ sơ, khám phá, lượt thích, kết đôi, tin nhắn, hành trình và quản trị. |
| `KissAppTussing/Models/`, `Contracts/` | Mô hình dữ liệu và cấu trúc yêu cầu/phản hồi của API. |
| `KissAppTussing/Data/`, `Migrations/` | Cấu hình Entity Framework Core, khởi tạo dữ liệu và lịch sử thay đổi cơ sở dữ liệu. |
| `KissAppTussing/Services/` | Logic nghiệp vụ như hồ sơ, kết nối, trò chuyện và bài viết. |
| `KissAppTussing/wwwroot/` | Giao diện web, CSS, JavaScript và ảnh hồ sơ demo được phân phối cùng ứng dụng. |
| `KissAppTussing/App_Data/` | Tệp dữ liệu do ứng dụng lưu cục bộ, chẳng hạn nội dung đính kèm tin nhắn. |
| `KissAppTussing/ADMIN-DATA.md` | Hướng dẫn cấp quyền Quản trị viên và mô tả dữ liệu quản trị. |
| `KissAppTussing/PASSWORD-RESET.md` | Hướng dẫn cấu hình gửi email đặt lại mật khẩu. |
| `SQL/` | Tài liệu và câu lệnh SQL dùng khi làm việc với SQL Server. |
| `scripts/` | Script SQL tạo/bổ sung hồ sơ demo và tiện ích tạo ảnh demo. |
| `KissAppTussing.slnx` | Solution để mở dự án bằng .NET CLI hoặc Visual Studio. |

## Chạy ứng dụng trên máy Windows

Cần cài .NET 10 SDK và chạy SQL Server, ví dụ SQL Server Express với instance mặc định `SQLEXPRESS`. Chuỗi kết nối hiện được khai báo trong `KissAppTussing/appsettings.json`, trỏ tới database `KissAppTussing` bằng Windows Authentication.

Mở PowerShell tại thư mục gốc của repository:

```powershell
cd .\KissAppTussing
$env:Jwt__Key = "thay-bang-chuoi-bi-mat-rieng-it-nhat-32-ky-tu"
dotnet run
```

Mở địa chỉ `http://localhost:5000`. Khi chạy lần đầu, ứng dụng áp dụng migration để tạo/cập nhật database và chuẩn bị dữ liệu sở thích, hồ sơ demo. Tài khoản Windows chạy ứng dụng cần kết nối và có quyền cần thiết trên SQL Server.

> Hãy dùng khóa JWT riêng cho từng môi trường và giữ bí mật khóa, mật khẩu SMTP cùng dữ liệu cá nhân. Không đưa các giá trị thật này vào Git.

### Các tích hợp cần cấu hình riêng

Một số mục trong bảng cần dịch vụ hoặc quy trình vận hành chưa có trong repository: gọi thoại/video và push khi website đóng cần hạ tầng realtime/push; xác minh danh tính, xác minh/kiểm duyệt ảnh và phát hiện tài khoản giả cần nhà cung cấp hoặc quy trình duyệt; Premium, mở khóa người đã thích, boost, lượt Super Like trả phí, giao dịch/gia hạn và quảng cáo cần mô hình gói cùng cổng thanh toán/quảng cáo. Dịch tin nhắn và AI matching cũng cần dịch vụ riêng. Các chức năng này chưa xử lý giao dịch hay xác minh thật.

Trong bản hiện tại, gợi ý hồ sơ và câu mở đầu được tạo bằng quy tắc cục bộ (không gọi AI); địa điểm hẹn là các gợi ý loại địa điểm để người dùng tự điền (chưa tìm kiếm địa điểm theo bản đồ); quà tặng trong chat là lời nhắn emoji miễn phí, không phải vật phẩm hay giao dịch. Báo cáo và ẩn nội dung hiện có công cụ kiểm duyệt thủ công trong phần quản trị. OTP email dùng cấu hình SMTP ở trên.

### Bổ sung tài khoản demo trong SQL Server

Nếu cần thêm hồ sơ demo để tổng số người dùng đạt 120, mở và chạy `scripts/SeedDemoUsersTo120.sql` trên đúng instance SQL Server. Script sử dụng database `KissAppTussing`; hãy chạy ứng dụng ít nhất một lần trước để database và các bảng được tạo. Những hồ sơ này là dữ liệu minh họa, không phải tài khoản người dùng thật.

## Cấp quyền Quản trị viên

Tạo tài khoản và đăng nhập trước. Lấy ID tài khoản của mình từ `GET /api/profiles/me`, sau đó cấu hình ID này khi khởi động ứng dụng:

```powershell
$env:Admin__UserIds__0 = "GUID-tai-khoan-cua-ban"
dotnet run
```

Khi đăng nhập bằng tài khoản đã được cấp quyền, mục **Quản trị viên** sẽ xuất hiện trong giao diện. Xem [hướng dẫn quản trị và xuất dữ liệu](KissAppTussing/ADMIN-DATA.md) để biết thêm chi tiết.

## Tài liệu liên quan

- [Hướng dẫn kết nối SQL Server và nhắn tin](KissAppTussing/README.md)
- [Quản trị viên và dữ liệu xuất](KissAppTussing/ADMIN-DATA.md)
- [Đặt lại mật khẩu](KissAppTussing/PASSWORD-RESET.md)
