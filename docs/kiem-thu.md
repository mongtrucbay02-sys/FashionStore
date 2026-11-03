# Kiểm tra dự án

Chạy `pwsh -File scripts/Test.ps1` tại thư mục dự án, với .NET 10 SDK, Node.js và PowerShell 7.

Bộ kiểm tra hiện tại gồm CloudflareImage.Checks (transport ảnh dùng cho thử đồ), VirtualTryOn.Checks, MensCatalog.Checks, quy tắc size, bộ request Apidog, HTTP thử đồ và HTTP mua hàng/quản trị.

Các kiểm tra hồi quy xác nhận Phối đồ có ảnh thật và gợi ý size, không có công cụ hoặc endpoint AI; Studio AI và route quản trị ảnh cũ đã gỡ; form sản phẩm vẫn tải/xem trước ảnh. Giỏ, đặt đơn, danh mục nam và Thử đồ AI riêng được kiểm tra bằng dữ liệu tách biệt.

Không gọi API AI thật trong kiểm tra tự động. Chưa đánh giá chất lượng ảnh Cloudflare thật trong lần thay đổi này. Dùng `scripts/Package.ps1` để tạo bản ZIP sạch; tài khoản/đơn/đánh giá trong bản sao đóng gói được đặt rỗng, workspace giữ nguyên dữ liệu đang sử dụng.

## Kết quả trước khi ghép tồn kho và OTP

Build Release 0 lỗi/0 cảnh báo; 134 kiểm tra tự động qua. Kiểm tra giao diện xác nhận ảnh áo/quần đổi đúng lựa chọn, lưu/mặc lại outfit, 170 cm/65 kg gợi ý áo L và quần M, áp dụng đúng size. Viewport 390 × 844 không tràn ngang. Thử đồ AI riêng vẫn qua kiểm tra mô phỏng; form quản trị vẫn có tải/xem trước ảnh và các route Studio cũ trả 404.

## Font tiếng Việt

Đã chuyển sang Be Vietnam Pro cục bộ với 6 độ đậm 300–800, đối chiếu glyph tiếng Việt trong các file TTF. Kiểm tra trình duyệt xác nhận font tải từ website, tiêu đề không dùng chữ nghiêng khác font, các ô nhập cùng font và không tràn ngang ở viewport 390 × 844 trên Phối đồ, trang chủ, sản phẩm, đăng nhập và Thử đồ. Hai layout đều nạp stylesheet font dùng chung; quản trị đối chiếu mã nguồn, chưa đăng nhập để kiểm tra trực quan trong lần sửa font.

## Ghép tồn kho và OTP — 28/10/2026

Build Release thành công (0 lỗi); có cảnh báo NU1900 vì không truy cập được dữ liệu kiểm tra lỗ hổng NuGet, không có cảnh báo biên dịch C#. Đã chạy lại `scripts/Test.ps1`: **187 kiểm tra qua** — CloudflareImage 26, VirtualTryOn 26, MensCatalog 10, Store 49, size 21, Apidog 18, HTTP thử đồ 15, HTTP mua hàng/quản trị 22.

Store.Checks sử dụng startup thật của ứng dụng, thay transport email bằng bộ gửi mô phỏng trong host kiểm thử riêng. Đã kiểm tra đăng ký/đăng nhập bắt buộc OTP, chưa xác thực không có tài khoản/cookie, không nâng quyền qua form, sai/hết hạn/5 lần sai/gửi lại/chống dùng lại/cookie phiên/CSRF/quota/khóa tài khoản sau bước mật khẩu. Không gửi email thật và chưa xác minh Gmail nhận thư thực tế; cần cấu hình mật khẩu ứng dụng của chủ website để thử.

Tồn kho: migration giữ tổng và không chạy chia lại; nhập/điều chỉnh theo size, nhập sai kiểu, hết/sắp hết, checkout trừ kho, mua đồng thời không vượt tồn, hủy đơn một lần và tương thích đơn cũ, hoàn kho không làm mất số lượng ở ngưỡng nhập tay, rollback/phục hồi journal, phân quyền và xuất Excel. Dữ liệu thật được đối chiếu hash không thay đổi.

Các kiểm tra HTTP hồi quy cũ dùng tài khoản CLI seed ở thư mục tách biệt và cấu hình tắt OTP đăng nhập **chỉ cho host hồi quy đó**; bộ Store.Checks chạy bật OTP cho toàn bộ vòng đời đăng ký/đăng nhập, không dùng cấu hình bỏ qua. Không gọi Cloudflare thật. Giao diện mới được kiểm tra HTML qua HTTP; chưa đánh giá trực quan OTP trong trình duyệt ở lần ghép này.

## QR Pay (VietQR) — 28/10/2026

Đã chạy toàn bộ `scripts/Test.ps1` sau khi thêm thanh toán QR: **218 kiểm tra qua**. Store.Checks hiện 80 (thêm 31 QR), các bộ còn lại giữ 26 + 26 + 10 + 21 + 18 + 15 + 22. Build Release thành công.

Kiểm tra QR dùng thông tin ngân hàng/đơn giả lập, không tải ảnh QR từ nhà cung cấp và không chuyển tiền thật. Đã xác minh HTML/form/ràng buộc số tiền/tài khoản từ máy chủ, CSRF/phân quyền, chủ đơn, báo chuyển khoản không đánh dấu paid, đối soát thủ công/cấm mã giao dịch trùng, chặn xử lý QR chưa trả tiền, snapshot tài khoản nhận và hủy đơn/hoàn kho/audit. Các page dữ liệu đơn và cấu hình ngân hàng dùng no-store. Chưa thử quét bằng app ngân hàng hoặc nhận tiền thực tế; chủ website cần cấu hình tài khoản và thử trước khi dùng thật. Chưa kiểm tra trực quan giao diện QR trong trình duyệt ở lần này.

## SQL Server — 28/10/2026

Đã chuyển mặc định website sang SQL Server và tạo `FashionStoreDB` trên `.\SQLEXPRESS` (Windows Authentication). Database có 14 bảng. Đã nhập và đối chiếu nội dung: 3 danh mục, 31 sản phẩm, 3 tài khoản, 9 đơn/15 dòng đơn, 31 món phối đồ, 3 lịch sử kho; dữ liệu nguồn JSON giữ nguyên. Đối chiếu bao gồm ID/GUID, hash mật khẩu, thông tin đơn/giá/size và snapshot thanh toán, không in dữ liệu cá nhân trong báo cáo.

Toàn bộ hồi quy trước chuyển đổi: 218 kiểm tra JSON tách biệt. Bộ SQL Server riêng: **83 kiểm tra qua**, bao gồm OTP/giỏ/đặt và hủy đơn/QR/khóa tài khoản, transaction rollback, khóa giữa hai store độc lập, chặn import đè, rollback import lỗi và FK danh mục. Tổng **301 kiểm tra**. SQL test tạo/xóa database GUID riêng, không tác động FashionStoreDB thật. Build Release 0 lỗi/0 cảnh báo; đã cập nhật phụ thuộc mật mã của driver để tránh cảnh báo gói có lỗ hổng mà NuGet báo khi thêm SQL client.

Smoke test chỉ đọc với thư mục catalog/runtime JSON rỗng: trang chủ, sản phẩm, phối đồ, Thử đồ và đăng nhập đều HTTP 200; Phối đồ có 31 sản phẩm từ SQL. Không gửi email, gọi AI hoặc chuyển tiền thật. Chưa đánh giá ảnh AI/Gmail thực tế trong thay đổi này.

Có thể chạy `dotnet run -- --verify-sql-import` ngay sau nhập, trước khi sử dụng website tiếp, để đối chiếu nguồn JSON với SQL; sau khi phát sinh dữ liệu mới trong SQL, chênh lệch với JSON nguồn là bình thường.
