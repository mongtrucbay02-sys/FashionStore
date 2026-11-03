# Phân tích mua sắm trực quan và gợi ý size

## Chức năng hiện tại

Trang Phối đồ sử dụng danh mục nam đang bán: bộ vest hoặc áo + quần. Ảnh và giá được lấy từ cùng danh mục cửa hàng; hai món được hiển thị bằng ảnh sản phẩm riêng. Khách lọc kiểu/màu, chọn size, xem tổng giá, lưu/mặc lại bộ đồ trong trình duyệt rồi thêm giỏ. Không có AI tư vấn, tạo lookbook hoặc gợi ý phối màu trên trang này.

Máy chủ kiểm tra ID, loại, trạng thái, tồn kho và size. Giá lấy lại từ máy chủ. Bộ vest vào giỏ một dòng; áo và quần vào hai dòng với size độc lập. Đơn lưu tên/giá/size tại thời điểm đặt nên thay danh mục không đổi lịch sử đơn.

## Thuật toán size

Bảng áo gồm M/L/XL, quần S/M/L/XL. Tìm cỡ từ chiều cao và cân nặng riêng rồi lấy cỡ lớn hơn. Mốc áo 175 cm/75 kg ưu tiên XL; khoảng trống mốc quần xếp lên cỡ tiếp theo. Mặc rộng tăng một cỡ; vượt bảng không ép về XL. Chỉ áp dụng cỡ thực sự có ở món đang chọn. Bộ vest dùng một size chung, chỉ tự áp dụng khi hai kết quả áo/quần trùng nhau.

Ví dụ 170 cm/65 kg cho áo L, quần M. Bảng có tối đa bốn dòng nên chi phí O(k) với k nhỏ. Thông số được tính trong trình duyệt, không lưu hồ sơ hoặc gửi dịch vụ AI. Đây là gợi ý theo bảng, cần đối chiếu thêm số đo và phom sản phẩm.

## AI thử đồ riêng

Trang `/TryOn` vẫn nhận ảnh người và trang phục/mô tả để gọi Cloudflare. Vùng tô được gửi dưới dạng ảnh hướng dẫn; Canvas ghép lại với ảnh gốc ngoài vùng tô. Ảnh và kết quả chỉ trong request/bộ nhớ và phiên trình duyệt, không lưu công khai. Chức năng này độc lập với trang Phối đồ và quản trị; quản trị không có Studio AI.

## Kiểm tra và demo

Chạy `scripts/Test.ps1` để kiểm tra danh mục nam, size, ảnh Cloudflare, thử đồ, Apidog và HTTP giỏ/đơn trên dữ liệu riêng. Không gọi AI thật trong các kiểm tra tự động.

Demo: Phối đồ → chọn bộ vest hoặc áo + quần → xem ảnh/tổng giá → nhập chiều cao/cân nặng → gợi ý và áp dụng size → lưu bộ đồ → thêm giỏ → đặt COD → xem lịch sử. Có thể demo Thử đồ AI riêng sau khi cấu hình Cloudflare và có hạn mức.
