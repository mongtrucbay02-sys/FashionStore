# Dùng Apidog kiểm tra AI thử đồ

Apidog gửi request đến website FashionStore; website gọi Cloudflare. Apidog không thay thế mô hình AI hoặc cung cấp miễn phí hạn mức Cloudflare.

## Nhập bộ request

1. Chạy website tại http://localhost:5080 với CLOUDFLARE_API_TOKEN và CLOUDFLARE_ACCOUNT_ID được cấu hình trên máy chủ như docs/ket-noi-cloudflare.md.
2. Trong Apidog chọn Settings > Import Data > Postman (hoặc Import Project > Postman).
3. Nhập hai file trong docs/apidog: FashionStore.postman_collection.json và FashionStore.local.postman_environment.json. Chọn môi trường FashionStore Local.
4. Kiểm tra baseUrl đúng cổng website. Giữ cùng host localhost cho tất cả request. Nếu Apidog tách Base URL khi nhập, bảo đảm địa chỉ cuối cùng chỉ có một http://localhost:5080.
5. Nhập email/password tài khoản FashionStore hiện có vào biến cục bộ của Apidog; không xuất/chia sẻ file có mật khẩu hoặc cookie. Bản cung cấp có thông tin đăng nhập trống. Không cần tài khoản Admin. Không đặt Cloudflare API key trong Apidog cho bộ request này.
6. Bật cookie jar/lưu cookie tự động, giữ cookie giữa request. Apidog và trình duyệt có phiên đăng nhập riêng. Dùng Apidog desktop hoặc agent cục bộ để gọi localhost.

## Gửi theo thứ tự

- 01 GET /Account/Login: script sau phản hồi lấy loginCsrf từ HTML.
- 02 POST /Account/Login: gửi email/password cùng CSRF. Mật khẩu đúng sẽ tạo cookie đăng nhập ngay; giữ cookie jar cho các bước tiếp theo.
- 05 GET /TryOn: lấy token CSRF mới sau đăng nhập; cần data-authenticated="true". Không tái sử dụng token trước đăng nhập.
- 06 POST /TryOn/Generate không ảnh: mong đợi 400 JSON thiếu ảnh, không gọi Cloudflare.
- 07 chỉ gửi thủ công: Body > form-data > person kiểu File, chọn ảnh PNG/JPG; giữ source=description, scope=top, mô tả áo. Máy chủ yêu cầu ảnh người 128–2048 px mỗi cạnh, tỷ lệ từ 1:3 đến 3:1, tối đa 5 MB. JPEG/PNG phù hợp; trang web có bước chuẩn hóa ảnh, request trực tiếp thì bạn cần chuẩn bị kích thước sẵn. Chọn thời gian chờ ít nhất 240 giây nếu client hết thời gian sớm.

Để dùng ảnh trang phục: trong request 07 đổi source=upload và thêm 1 hoặc 2 trường File có cùng tên garments. Không gửi garments khi source=description. Scope nhận top, bottom hoặc outfit. Mask là tùy chọn, để disabled khi không dùng. Khi dùng mask: PNG có alpha, dưới 4 MB, cùng kích thước ảnh person; vùng trong suốt được chỉnh sửa.

Không tự đặt Content-Type cho multipart: Apidog tạo boundary khi gửi form-data. Thành công trả 200 image/png; lưu response thành PNG để xem.

Lưu ý: gửi API trực tiếp trả PNG nguyên từ nhà cung cấp. Bước ghép ảnh gốc ngoài vùng tô nằm ở JavaScript trên trang /TryOn; test trực tiếp bằng Apidog không chạy bước đó. Dùng giao diện website nếu cần kiểm tra tính năng giữ pixel ngoài vùng tô.

## Xử lý lỗi

- 302 tới /Account/Login hoặc 200 HTML form đăng nhập: cookie chưa được giữ hoặc phiên hết hạn; gửi lại 01–05.
- 400: kiểm tra CSRF, đăng nhập, file và kích thước. CSRF lỗi có thể trả trang HTML không có message JSON.
- 503: website chưa nhận Cloudflare key; cấu hình lại và khởi động website từ đúng terminal.
- 429: hạn mức/tốc độ từ nhà cung cấp hoặc đang có tác vụ ảnh khác. Không gửi lặp liên tục.
- 502: key, quyền mô hình, kết nối hoặc kết quả từ nhà cung cấp có lỗi.

Đã kiểm tra cấu trúc JSON và script trên phản hồi HTML giả lập. Chưa nhập/gửi trực tiếp trong Apidog và chưa gọi Cloudflare thật trong lần chuẩn bị này.

Tài liệu nhập Postman v2.1 vào Apidog: https://docs.apidog.com/en/import-from-postman-635043m0

