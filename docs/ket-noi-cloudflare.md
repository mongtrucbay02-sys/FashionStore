# Kết nối Cloudflare Workers AI

FashionStore hiện dùng Cloudflare cho tính năng thử đồ trên ảnh cá nhân. Không dùng khóa hoặc gọi OpenAI/Gemini trong luồng hiện tại.

## Lấy cấu hình

1. Đăng nhập https://dash.cloudflare.com/ .
2. Vào Workers AI, chọn Use REST API.
3. Chọn Create a Workers AI API Token, kiểm tra quyền và tài khoản rồi tạo token. Nếu tự tạo custom token, cần Workers AI Read và Workers AI Edit trên đúng tài khoản. Chỉ sao chép token vào máy của bạn.
4. Sao chép Account ID từ cùng trang. Account ID gồm 32 ký tự hex, không phải Zone ID hoặc email.
5. Để dùng hạn mức miễn phí, giữ tài khoản Workers Free. Cloudflare cấp 10.000 Neurons/ngày dùng chung các mô hình; đây không phải 10.000 ảnh. Hết hạn mức thì phải đợi lượt cấp mới. Nếu tài khoản đã là Workers Paid, vượt phần miễn phí có thể phát sinh phí; website không tự kiểm tra gói hoặc số dư và không bảo đảm chặn đúng ngưỡng miễn phí.

## Chạy website

Trong file `.env` ở thư mục dự án, điền:

```env
CLOUDFLARE_ACCOUNT_ID=
CLOUDFLARE_API_TOKEN=
```

`CLOUDFLARE_ACCOUNT_ID` là ID 32 ký tự hex. Token phải được tạo trong đúng tài khoản Cloudflare và có quyền Workers AI. Không gửi token trong chat hoặc đưa vào `.env.example`.

Dừng phiên cũ, mở PowerShell tại thư mục dự án và chạy:

```powershell
cd 'C:\Users\Admin\Downloads\FashionStore_SQLServer\FashionStore_SQLServer'
dotnet run --urls http://localhost:5080
```

Mở http://localhost:5080/TryOn và đăng nhập. “Đã cấu hình Cloudflare” chỉ xác nhận có token và Account ID đúng dạng; tạo ảnh thử để xác nhận token có quyền dùng model và còn hạn mức.

Không gửi token trong chat, không đặt trong JavaScript, source code hoặc file JSON xuất từ Apidog. Không cần domain, đổi DNS hoặc deploy website lên Cloudflare để gọi Workers AI REST API.

## Cấu hình kỹ thuật

- CLOUDFLARE_API_TOKEN và CLOUDFLARE_ACCOUNT_ID: biến môi trường của tiến trình website.
- CloudflareAI__ApiKey và CloudflareAI__AccountId: cấu hình thay thế, ưu tiên hơn các biến trên.
- CloudflareAI__Model: mặc định @cf/black-forest-labs/flux-2-dev; cần tương thích multipart/multi-reference khi thay model.
- Endpoint https://api.cloudflare.com/client/v4/accounts/{accountId}/ai/run/{model}, header Bearer.
- Ảnh gửi qua multipart input_image_0..3; mỗi ảnh thu nhỏ giữ tỷ lệ, cạnh dài tối đa 512 px. Ảnh đầu tiên là người trong thử đồ, và các ảnh quần áo theo sau.
- 20 steps; ảnh vuông 512x512, ảnh dọc 512x768; thử đồ tự giữ tỷ lệ gần nhất theo bội số 16, cạnh dài tối đa 768. Mục đích là giảm tài nguyên dùng cho demo. Không hứa số lượt cố định/ngày, vì dung lượng ảnh đầu vào/đầu ra và số bước quyết định mức dùng.
- Vùng tô không dùng trường mask chuyên biệt. Server tạo ảnh hướng dẫn phủ đỏ vùng được đổi, gửi ở vị trí cuối. Trình duyệt vẫn ghép ảnh gốc ngoài vùng tô; API gọi trực tiếp bằng Apidog không chạy bước ghép này.
- Kết quả Cloudflare có thể là JPEG/PNG; server giải mã và chuẩn hóa thành PNG thật, không đổi đuôi file giả.
- SkiaSharp (MIT) dùng cho chuẩn hóa ảnh. Nếu deploy Linux cần bổ sung native assets SkiaSharp tương ứng với môi trường; bản hiện tại đã kiểm tra trên Windows.

## Riêng tư và kết quả

Nhấn tạo ảnh sẽ gửi ảnh người, ảnh sản phẩm, ảnh vùng tô và mô tả tới Cloudflare Workers AI. Không dùng URL công khai cho ảnh đầu vào. Ảnh thử đồ chỉ trong request/bộ nhớ và phiên trình duyệt; trang Phối đồ và quản trị không gọi AI. AI có thể đổi chi tiết hoặc vị trí trang phục; không bảo đảm gương mặt, nếp vải hoặc sản phẩm giống tuyệt đối. Kiểm tra ảnh trước khi dùng.

## Kiểm tra và lỗi

- 503: thiếu token/Account ID hoặc Account ID sai dạng. Khởi động lại trong cùng terminal đã đặt cấu hình.
- 502: token/quyền/model/mạng/ảnh đầu ra lỗi; website không lộ thông báo thô hoặc token.
- 429: có tác vụ ảnh khác, vượt tốc độ hoặc hết hạn mức. Không tự gửi lại yêu cầu.
- Cổng 5080/EXE bị khóa: dừng phiên ứng dụng cũ, không chạy chồng nhiều phiên.
- Bộ Apidog ở docs/apidog dùng được với endpoint website hiện tại; không cần đưa token Cloudflare vào Apidog.

Đã kiểm tra bằng transport mô phỏng. Chưa xác minh ảnh thật hoặc hạn mức tài khoản Cloudflare của bạn khi chưa cấu hình.

Tài liệu:
- https://developers.cloudflare.com/workers-ai/get-started/rest-api/
- https://developers.cloudflare.com/workers-ai/platform/pricing/
- https://developers.cloudflare.com/workers-ai/models/flux-2-dev/
- https://blog.cloudflare.com/flux-2-workers-ai/
- https://developers.cloudflare.com/workers-ai/features/json-mode/
