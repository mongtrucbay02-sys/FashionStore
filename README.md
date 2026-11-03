# FashionStore — SQL Server / Supabase

Website ASP.NET Core MVC .NET 10, thời trang nam. Dữ liệu vận hành có thể dùng **SQL Server** local hoặc **Supabase PostgreSQL**: tài khoản, danh mục, sản phẩm/tồn size, đơn hàng, đánh giá, lịch sử kho, cấu hình/đối soát QR và phối đồ. Ảnh/font vẫn là file; Session ở bộ nhớ máy chủ.

## Chạy trên máy hiện tại

Database local mặc định: **FashionStoreDB**, server **.\SQLEXPRESS**, Windows Authentication. Supabase được cấu hình bằng `DATABASE_URL` trong `.env`.

```powershell
cd 'C:\Users\Admin\Downloads\FashionStore_SQLServer\FashionStore_SQLServer'
dotnet run --urls http://localhost:5080
```

Mở http://localhost:5080. Đăng ký và đăng nhập cần OTP gửi qua Gmail SMTP; cấu hình theo [hướng dẫn OTP](docs/otp-email.md).

## Cài bản ZIP trên máy khác

Cần .NET 10 SDK và SQL Server 2017+ (khuyên dùng SQL Server Express 2022/2025). Sửa server/database trong `appsettings.json` theo máy đó. Trước lần chạy đầu:

```powershell
pwsh -NoProfile -File .\scripts\Initialize-SqlServer.ps1 -Server '.\SQLEXPRESS' -Database 'FashionStoreDB'
```

Script tạo database/schema và nhập dữ liệu nguồn trong `Data/` và `App_Data/`. Chỉ nhận database trống, không nhập đè và không xóa JSON nguồn. ZIP sạch chứa danh mục/ảnh, không chứa tài khoản/đơn/đánh giá hoặc tài khoản ngân hàng cá nhân. Nếu khởi tạo database khác tên mặc định, cập nhật cùng tên đó trong `appsettings.json` hoặc biến `ConnectionStrings__FashionStore` trước khi chạy website.

SQL thủ công: `database/CreateDatabase.sql`, sau đó chạy `database/Schema.sql` trong FashionStoreDB bằng SSMS. Dùng lệnh `--init-sql` để nhập dữ liệu; không cần tạo bảng bằng tay khi dùng script PowerShell.

## Chức năng

- 31 sản phẩm nam: 15 áo, 9 quần, 7 bộ vest; ảnh thật và font Be Vietnam Pro cục bộ.
- Đăng ký/đăng nhập bằng email, mật khẩu và OTP Gmail; tài khoản/quyền/khóa, giỏ, COD/chuyển khoản/QR Pay, lịch sử đơn và đánh giá.
- Tồn theo size, nhập/điều chỉnh, cảnh báo, lịch sử và xuất Excel; đặt hàng trừ kho và hủy đơn hoàn kho một lần trong giao dịch SQL.
- VietQR theo số tiền/mã đơn, thông tin nhận tiền giữ theo từng đơn, Admin đối soát và xác nhận thủ công. Không tự kết nối sao kê/hoàn tiền ngân hàng.
- Phối áo + quần hoặc bộ vest bằng ảnh thật, lưu bộ đồ, thêm giỏ và gợi ý size theo chiều cao/cân nặng. Trang Phối đồ/quản trị không có Studio AI.
- Trang Thử đồ AI riêng với Cloudflare Workers AI, dùng ảnh người và trang phục/mô tả. Không lưu ảnh cá nhân/kết quả vào thư mục công khai.

## Tài liệu

- [SQL Server, sơ đồ và chuyển dữ liệu](docs/sql-server.md)
- [Chuyển dữ liệu lên Supabase](docs/supabase.md)
- [Schema quan hệ PostgreSQL cho Supabase](database/SupabaseRelationalSchema.sql)
- [OTP Gmail](docs/otp-email.md)
- [QR Pay](docs/qr-pay.md)
- [Quản lý tồn kho](docs/ton-kho.md)
- [Cloudflare](docs/ket-noi-cloudflare.md)
- [Thử đồ](docs/thu-do-ao.md) · [Phối đồ/size](docs/phoi-do-ai.md)
- [Apidog](docs/huong-dan-apidog.md) · [Kết quả kiểm thử](docs/kiem-thu.md)

Tạo Admin demo trên database đã khởi tạo: `dotnet run --urls http://localhost:5080 -SeedAdmin`. Dùng email nhận được thư và chưa đăng ký; mật khẩu demo `AdminDemo!2026`. Seed không đổi mật khẩu/nâng quyền tài khoản đã có.

## Kiểm thử và đóng gói

```powershell
pwsh -NoProfile -File .\scripts\Test.ps1 -SqlServer
pwsh -NoProfile -File .\scripts\Package.ps1
```

Kiểm thử SQL dùng database `FashionStore_Test_<GUID>` riêng trên .\SQLEXPRESS, xóa đúng database tạm sau khi xong; không đụng dữ liệu thật, không gửi email/chuyển tiền/gọi AI thật. Các kiểm tra hồi quy cũ dùng backend JSON được bật riêng trong host kiểm thử. Chạy `Test.ps1` không có `-SqlServer` để bỏ phần cần SQL cục bộ.

Kết quả ZIP: `artifacts/FashionStore_SQLServer.zip`. Các ZIP cũ và nguồn ghép lịch sử được giữ riêng, không đưa vào bản này. Không phát hành `.keys`, tài khoản thật, SMTP/API key hoặc file database cá nhân.

## Cấu trúc

- `database/`: bảng, khóa, index, stored procedures và view tồn kho.
- `Services/AppStore.cs`, `SqlServerStore.cs`: truy cập SQL theo tham số và giao dịch; mặc định không chuyển sang JSON khi SQL lỗi.
- `Services/SqlDatabase.cs`: khởi tạo và nhập dữ liệu một lần, giữ ID/hash mật khẩu/lịch sử.
- `Data/`, `App_Data/*.json`: dữ liệu nguồn nhập/đối chiếu; website SQL không ghi các file này.
- `wwwroot/data/size-guide.json`: bảng size tĩnh cho trình duyệt, không phải dữ liệu giao dịch.
- `.keys/`: khóa bảo vệ cookie; giữ lại trên máy đang dùng khi nâng cấp.

SQL dùng giao dịch/khóa dữ liệu chung để tránh bán vượt tồn giữa các kết nối. Cơ chế hiện tải danh sách để phục vụ đồ án nhỏ; khi dữ liệu lớn cần truy vấn/phân trang và cập nhật theo từng bản ghi. Session vẫn ở bộ nhớ, cần giữ một phiên ứng dụng hoặc triển khai session dùng chung nếu mở rộng nhiều máy.
