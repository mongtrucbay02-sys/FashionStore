# Thử đồ trên ảnh cá nhân

Cấu hình theo [Cloudflare Workers AI](ket-noi-cloudflare.md), đăng nhập và mở `/TryOn`.

1. Tải ảnh người rõ phần trang phục cần đổi. Chấp nhận JPG/PNG/WEBP tối đa 5 MB ở giao diện; trình duyệt chuẩn hóa JPG/PNG, cạnh dài tối đa 1.536 px, giữ ảnh gốc trong bộ nhớ.
2. Chọn áo/quần/cả bộ và nguồn: sản phẩm cửa hàng, tối đa hai ảnh trang phục tải lên hoặc mô tả tối đa 1.500 ký tự.
3. Để AI tự xác định vùng hoặc tô vùng quần áo. Có cọ, tẩy, hoàn tác và xóa vùng tô. Tránh tô mặt, tóc, tay, phụ kiện và nền.
4. Bấm tạo, so sánh trước–sau và tải PNG. Đổi lựa chọn sẽ ẩn kết quả cũ để tránh dùng nhầm.

Ảnh người luôn là tham chiếu đầu tiên. Server thu nhỏ mỗi ảnh để gửi Cloudflare FLUX.2 dev; nếu có vùng tô, gửi thêm ảnh người với vùng sửa phủ đỏ làm hướng dẫn, không sử dụng trường mask chuyên biệt. Mô tả cố định yêu cầu giữ danh tính, tóc, vóc dáng, tư thế, da, phụ kiện, nền và ánh sáng.

Khi tô vùng, trình duyệt ghép ảnh AI qua mask lên ảnh gốc để giữ pixel ngoài vùng tô; kết quả giữ kích thước ảnh gốc. Đây là bảo vệ phần ngoài, không bảo đảm chất lượng hình học/vải trong vùng AI sửa. Chế độ tự động có thể thay đổi các vùng khác. Ảnh tạo sinh không cam kết thử vừa theo số đo.

Ảnh người, trang phục, hướng dẫn vùng sửa và mô tả được gửi Cloudflare khi nhấn tạo. Endpoint cần đăng nhập và CSRF, giới hạn dung lượng, trả PNG với no-store. Ảnh cá nhân/kết quả không được lưu trong hồ sơ hoặc thư mục public; kết quả ở phiên trình duyệt, cần tải về để giữ. Chiều cao/cân nặng không gửi tới AI.

Gọi trực tiếp bằng Apidog không thực hiện bước ghép Canvas ngoài vùng tô. Xem [hướng dẫn Apidog](huong-dan-apidog.md). Bộ kiểm tra service và HTTP nằm trong `tests/VirtualTryOn.Checks` và `tests/Http.Checks.ps1`; máy chủ browser-check tùy chọn chỉ dùng ảnh màu tổng hợp và dữ liệu riêng, không có chế độ giả lập trong website chính.
