# Danh mục thời trang nam

31 sản phẩm hiện tại gồm 15 áo, 9 quần, 7 bộ vest, nhãn `Audience=men`. Các danh mục giữ ID cũ nhưng hiển thị Áo nam, Quần nam, Bộ vest nam. Tên, giá, ảnh và ID 2001–2031 theo bộ ảnh cửa hàng; nguồn và tọa độ cắt được giữ trong `collection-2026-manifest.json`.

Danh mục dùng chung lọc thời trang nam cho trang chủ, danh sách/chi tiết, giỏ, phối đồ và thử đồ. ID bị ẩn hoặc không thuộc đồ nam không mua được bằng request trực tiếp. Quản trị tạo/sửa sản phẩm gắn nhãn nam.

Thử đồ cá nhân chỉ thay trang phục nam và yêu cầu giữ nguyên người trong ảnh, không biến đổi danh tính hay biểu hiện giới của khách hàng. Gợi ý size sử dụng bảng cửa hàng; thông số không lưu hồ sơ hoặc gửi AI.

Kết nối AI hiện tại là [Cloudflare Workers AI](ket-noi-cloudflare.md). Tài khoản/đơn cũ giữ nguyên, các dòng đơn đã lưu tiếp tục giữ tên/giá/size tại thời điểm đặt. Bộ kiểm tra `MensCatalog.Checks` xác minh lọc danh mục và mua hàng bằng ID. Trang Phối đồ chỉ dùng ảnh sản phẩm và gợi ý size; AI chỉ còn ở trang Thử đồ riêng.
