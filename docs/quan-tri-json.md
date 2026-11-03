> Tài liệu lịch sử bản JSON. Website hiện đã chuyển sang [SQL Server](sql-server.md); dữ liệu vận hành không ghi JSON.

# Bản ghép quản trị JSON — 13/10/2026

Người dùng chọn giữ JSON. Mã quản trị/giao diện từ FashionStore.zip được ghép vào OutfitStudio hiện tại; phần SQL Server trong ZIP được thay bằng JsonStore dùng chung với cửa hàng. Không tạo hoặc thay đổi cơ sở dữ liệu SQL Server.

## Ánh xạ chức năng

| Phần | Dữ liệu |
| --- | --- |
| Sản phẩm và danh mục | Data/products.json, Data/categories.json |
| Đánh giá khách gửi và Admin quản lý | Data/reviews.json |
| Người dùng, vai trò và trạng thái khóa | App_Data/users.json |
| Đơn hàng, trạng thái, thống kê doanh thu | App_Data/orders.json |
| Phối đồ và gợi ý | Data/outfits.json, giá/size lấy từ danh mục sản phẩm |

Giữ ID Guid của tài khoản JSON, không thay thành ID số từ model SQL. Alias ImageUrl/Image, CustomerName/FullName, OrderItems/Items, Price/UnitPrice và Comment/Content hỗ trợ giao diện quản trị gốc nhưng không ghi thêm các alias vào JSON.

Admin được bảo vệ bằng cookie role. Khách đăng ký luôn là User. Cookie được đối chiếu tài khoản mỗi request để khóa tài khoản và đổi quyền có hiệu lực ngay. Đăng xuất và tất cả thao tác thay đổi dùng POST có antiforgery. Tạo tài khoản Admin demo bằng lệnh CLI trong README, không tự nâng quyền người đăng ký đầu tiên.

Chặn xóa danh mục còn sản phẩm; chặn xóa sản phẩm đã có trong đơn hoặc dùng trong phối đồ. Có thể ẩn sản phẩm; phòng phối đồ chọn phương án mặc định còn hiển thị và cập nhật size theo danh mục. Đơn cũ có trạng thái “Đang xử lý” được nhận diện là “Chờ xác nhận” trong Admin. Chỉ chuyển đơn sang bước tiếp theo hoặc hủy khi chưa kết thúc.

Đã ghép quản lý tồn theo size: checkout trừ kho, hủy đơn mới hoàn kho một lần, đơn cũ không cộng kho. Xem [quản lý tồn kho](ton-kho.md). Ảnh tải lên có giới hạn 5 MB, nhận JPG/JPEG/PNG/WEBP; link ảnh không nhận javascript:. Không tự xóa ảnh cũ để tránh mất ảnh dùng chung.

## Kiểm tra

Build Release: 0 lỗi, 0 cảnh báo; cú pháp JavaScript hợp lệ. Kiểm tra với catalog và tài khoản/đơn thử riêng trong .test-output, không đưa dữ liệu thử vào bản bàn giao:

- Guest vào Admin chuyển đến đăng nhập; User nhận 403; Admin đăng nhập vào Dashboard.
- Đăng ký có tham số Role=Admin vẫn chỉ tạo User.
- Các trang Dashboard, sản phẩm/chi tiết/form, danh mục/form, đơn, người dùng và đánh giá trả 200.
- Thêm danh mục/sản phẩm lưu JSON và xuất hiện ở cửa hàng; trùng danh mục bị từ chối.
- Không xóa được danh mục có sản phẩm; sản phẩm trong đơn được bảo vệ.
- Giá/stock nhập sai kiểu và link ảnh javascript: bị từ chối.
- Sửa giá và size sản phẩm cập nhật dữ liệu phòng phối đồ; ẩn sản phẩm loại khỏi cửa hàng và phối đồ.
- Khách gửi đánh giá được lưu cùng tài khoản; Admin xóa được đánh giá.
- Checkout lưu đơn “Chờ xác nhận”; Admin xem được, không nhảy thẳng sang hoàn thành, chuyển đúng từng bước.
- Đơn hoàn thành không đổi tiếp; doanh thu Dashboard khớp tổng đơn hoàn thành.
- Đổi quyền có hiệu lực với cookie hiện tại; khóa tài khoản làm mất phiên và chặn đăng nhập; Admin không tự khóa.
- POST quản trị không có token bị từ chối.
- Trình duyệt: ẩn Polo đen trong Admin, phòng phối đồ tự chọn Polo trắng và vẫn hoạt động, không có lỗi console.
- Tải ảnh demo thành công, file phục vụ HTTP 200; xóa sản phẩm không tham chiếu và danh mục rỗng hoạt động.
- Thêm outfit vào giỏ và checkout vẫn hoạt động; hủy đơn đã trừ kho hoàn lại đúng một lần; đơn cũ chưa trừ giữ nguyên kho.
- Admin ở viewport 390 × 844 không tràn ngang toàn trang; bảng người dùng cuộn trong vùng bảng. Nút đăng xuất POST hoạt động. Đã khôi phục viewport desktop.

Ảnh `quan-tri-json.png` dùng dữ liệu thử. Bản ZIP sạch đã được giải nén vào thư mục khác và build lại thành công.

Xem README để tạo Admin demo và chạy. Bản ZIP trước chỉ có chức năng khách hàng vẫn được giữ riêng trong artifacts.

## Ảnh sản phẩm

Quản trị đã gỡ Studio AI và công cụ làm sạch ảnh trong form sản phẩm. Tải ảnh JPG/PNG/WEBP, nhập đường dẫn, xem trước ảnh và lưu sản phẩm vẫn hoạt động.

## OTP email

Đăng ký và mỗi lần đăng nhập quản trị/khách đều yêu cầu OTP. Xem [cấu hình Gmail](otp-email.md). Email demo .local cũ không nhận được thư thật.


