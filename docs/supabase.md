# Chuyển database cửa hàng sang Supabase

Ứng dụng có thể dùng Supabase PostgreSQL thay SQL Server. Toàn bộ collection cửa hàng (tài khoản, danh mục, sản phẩm/tồn size, phối đồ, đơn hàng, đánh giá, lịch sử kho và cấu hình QR) được giữ trong bảng JSONB `app_private.store_collections`. Schema `app_private` không được đưa vào Supabase Data API mặc định. Ảnh/font và các file tĩnh vẫn được phục vụ từ `wwwroot`.

`database/CreateDatabase.sql` và `database/Schema.sql` là script T-SQL dành cho SQL Server, không chạy trong Supabase SQL Editor. Script PostgreSQL dùng cho cấu trúc lưu trữ của ứng dụng là `database/SupabaseSchema.sql`. Lệnh `--init-supabase` bên dưới cũng tự tạo cấu trúc này trước khi nhập dữ liệu.

Đã có thêm `database/SupabaseRelationalSchema.sql`, chuyển các bảng, ràng buộc, chỉ mục và view tồn kho sang PostgreSQL trong schema `fashion_store`. Đây là schema quan hệ riêng để chuẩn bị chuyển backend. Backend hiện tại vẫn đọc/ghi `app_private.store_collections`; chạy file quan hệ này không tự chuyển dữ liệu hay làm ứng dụng chuyển sang các bảng mới.

## Di chuyển dữ liệu hiện tại

1. Tạo project Supabase và mở **Connect** trong Dashboard. Sao chép URI PostgreSQL; nếu mạng chỉ có IPv4, chọn **Session pooler**.
2. Điền URI vào `DATABASE_URL` trong `.env` local. Không gửi password database trong chat, không đưa URI vào mã nguồn hoặc trình duyệt.
3. Có thể chạy `database/SupabaseSchema.sql` trong Supabase **SQL Editor** để tạo bảng trước; có thể bỏ qua bước này vì lệnh nhập tự tạo schema và bảng nếu chưa có.
4. Dừng website để tránh dữ liệu SQL Server thay đổi trong lúc sao chép.
5. Chạy một lần:

   ```powershell
   dotnet run --project OutfitStudio.csproj -- --init-supabase
   ```

Lệnh này tạo schema Supabase, đọc snapshot nhất quán từ SQL Server đã cấu hình trong `appsettings.json`, rồi nhập dữ liệu vào database Supabase đang trống. Nếu đích đã có dữ liệu, lệnh dừng và không ghi đè. Database SQL Server nguồn và các file dữ liệu không bị xóa.

6. So sánh số bản ghi bằng:

   ```powershell
   dotnet run --project OutfitStudio.csproj -- --verify-supabase-import
   ```

Sau khi xác minh, chạy website bình thường; ứng dụng tự chọn Supabase khi `DATABASE_URL` có giá trị, nếu không thì dùng SQL Server local như trước.

## Kết nối

Supabase cung cấp URI ở **Dashboard → Connect**. Dùng kết nối trực tiếp cho backend chạy lâu dài nếu mạng hỗ trợ IPv6; dùng **Session pooler** khi cần IPv4. [Tài liệu kết nối PostgreSQL Supabase](https://supabase.com/docs/guides/database/connecting-to-postgres). Mã nguồn chuyển URI sang cấu hình Npgsql, bật xác minh TLS và chỉ dùng kết nối ở backend.
