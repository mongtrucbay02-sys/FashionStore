# OTP Gmail cho đăng ký và đăng nhập

Đăng ký chỉ lưu tài khoản sau khi xác minh OTP. Đăng nhập kiểm tra mật khẩu trước, gửi OTP tới Gmail, rồi mới cấp cookie đăng nhập. Mã có 6 chữ số, hết hạn sau 5 phút, tối đa 5 lần nhập và giới hạn gửi lại 60 giây.

## Cấu hình

1. Sao chép `.env.example` thành `.env` trong thư mục dự án.
2. Điền `SMTP_USER` bằng địa chỉ Gmail gửi thư và `SMTP_PASS` bằng Google App Password. Tài khoản Google cần bật xác minh 2 bước; không dùng mật khẩu Gmail thông thường.
3. Chạy ứng dụng từ thư mục dự án. Ứng dụng đọc `.env`; biến môi trường đã có trên máy được ưu tiên.

Không commit `.env`. Chỉ commit `.env.example`, không đặt thông tin thật vào file mẫu.
