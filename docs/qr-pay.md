# Thanh toán QR Pay (VietQR)

## Bật và cấu hình

1. Dừng website cũ và chạy lại bản mới. Đăng nhập Admin bằng OTP email như [hướng dẫn Gmail](otp-email.md).
2. Mở **Quản trị → Thanh toán QR** (`/PaymentSettings`).
3. Bật QR Pay, nhập BIN ngân hàng 6 chữ số, tên ngân hàng, tài khoản nhận tiền (chữ/số, tối đa 19 ký tự) và tên chủ tài khoản. [Danh sách ngân hàng/BIN của VietQR](https://api.vietqr.io/v2/banks).
4. Lưu, kiểm tra kỹ thông tin nhận tiền. Không nhập mật khẩu ngân hàng hoặc OTP vào form này.
5. Nếu chưa cấu hình hợp lệ, phương thức QR không xuất hiện tại checkout và request giả mạo chọn QR cũng bị từ chối. Không có tài khoản nhận tiền mẫu được bật sẵn.

Cấu hình tại `dbo.QrPaymentSettings` trong SQL Server, chỉ Admin được sửa bằng POST + CSRF. Tắt QR chỉ ảnh hưởng đơn mới. Đơn QR đang tồn tại giữ ảnh chụp thông tin nhận tiền lúc đặt, tránh đổi ngân hàng giữa chừng.

## Khách hàng thanh toán

- Thêm hàng → thanh toán → chọn **QR Pay — quét mã VietQR** → đặt hàng.
- Website tạo đơn và trừ tồn theo size, chuyển ngay đến chi tiết đơn có QR, số tiền gồm phí ship, ngân hàng, tài khoản nhận và nội dung thanh toán riêng.
- Mở ứng dụng ngân hàng, quét QR (hoặc lưu ảnh rồi quét từ thư viện). Kiểm tra người nhận, số tiền và nội dung trước khi chuyển. Có nút sao chép tài khoản, số tiền và nội dung.
- Sau khi chuyển, bấm **Tôi đã chuyển khoản**. Đơn chuyển sang **Chờ đối soát**, chưa được ghi là đã trả tiền. Tải lại chi tiết đơn/lịch sử để xem kết quả.
- Không tự động tạo lại đơn khi xem QR. Không chuyển lần hai nếu đã chuyển. QR của đơn đã hủy hoặc đã được xác nhận thanh toán không còn hiển thị trong website.

## Quản trị đối soát

Mở chi tiết đơn, đối chiếu **tài khoản nhận + số tiền + nội dung** trên sao kê/app ngân hàng. Nhập mã giao dịch thực tế, đánh dấu đã kiểm tra nhận đủ tiền, rồi bấm **Xác nhận đã nhận tiền**. Website lưu thời điểm/người xác nhận/mã giao dịch. Cùng một mã giao dịch không được dùng để xác nhận hai đơn.

Nếu khách báo đã chuyển nhưng chưa có giao dịch, dùng **Chưa thấy giao dịch — yêu cầu khách kiểm tra lại**. Đơn QR chưa được xác nhận nhận tiền không chuyển sang xác nhận/giao hàng/hoàn thành; có thể hủy để hoàn lại tồn kho. Đơn cũ COD/chuyển khoản giữ luồng đang có.

Không có kết nối sao kê/webhook ngân hàng trong tính năng này; trạng thái được quản trị xác nhận thủ công. Khách bấm nút hay quét QR không chứng minh tiền đã vào ngân hàng. Hủy đơn đã nhận tiền không tự hoàn tiền: trạng thái nhận tiền/audit vẫn giữ, Admin cần đối soát và hoàn tiền bên ngoài website. Không tự hết hạn/hủy các đơn chờ QR; Admin xử lý các đơn bỏ dở ở trang đơn hàng.

## Cách tạo mã và dữ liệu

Website dùng [VietQR Quick Link](https://www.vietqr.io/qr-dong) với ảnh **SVG**, tài khoản nhận lấy từ cấu hình Admin, `amount` lấy từ tổng đơn đã lưu, `addInfo` là mã `FS...` riêng của đơn, `accountName` là chủ tài khoản cửa hàng. Không tin dữ liệu số tiền/tài khoản/trạng thái trả tiền do trình duyệt gửi lên. Chỉ chủ đơn xem/báo chuyển khoản của đơn đó; Admin quản trị đối soát.

Ảnh được tải từ `https://img.vietqr.io` nên cần Internet và phụ thuộc dịch vụ VietQR.io. Khi trình duyệt tải ảnh, dịch vụ nhận tài khoản ngân hàng cửa hàng, số tiền, nội dung và tên chủ tài khoản; website không đưa tên/điện thoại/email/địa chỉ khách vào URL. Trình duyệt vẫn kết nối trực tiếp nhà cung cấp ảnh; dùng `referrerpolicy=no-referrer`. Khi không tải được QR, thông tin chuyển khoản vẫn hiện để khách chuyển thủ công.

Dùng SVG theo [thông báo cập nhật VietQR tháng 10/2026](https://www.vietqr.io/thong-bao-cap-nhat); không dùng API Generate hay khóa API. Điều kiện/hạn mức của nhà cung cấp có thể thay đổi. QR chỉ hỗ trợ số tiền VND nguyên dương tối đa 13 chữ số.

## Kiểm thử

Bộ Store.Checks thêm 31 kiểm tra QR với ngân hàng/tài khoản/đơn mô phỏng trong thư mục tách biệt: cấu hình/CSRF/phân quyền, giấu QR khi chưa cấu hình, dữ liệu giả mạo, tổng tiền và mã đơn trong URL, quyền chủ đơn, báo chuyển không đánh dấu paid, đối soát/trùng giao dịch, giữ snapshot khi đổi ngân hàng, hủy/hoàn kho và tương thích COD/đơn cũ. Không tải QR của tài khoản mô phỏng và không chuyển tiền thật. Cần chủ website tự cấu hình tài khoản nhận và thử quét trên app ngân hàng trước khi sử dụng thực tế.

