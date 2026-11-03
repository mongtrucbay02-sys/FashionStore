# Gợi ý size theo bảng cửa hàng

## Bảng size cửa hàng

Dữ liệu theo ảnh người dùng cung cấp, lưu tại `wwwroot/data/size-guide.json`. Ảnh nguồn: `docs/size-guide-source.png`.

### Áo

| Size | Chiều cao | Cân nặng |
| --- | --- | --- |
| M | Dưới 170 cm | Dưới 67 kg |
| L | 170–175 cm | 67–75 kg |
| XL | 175–184 cm | 75–85 kg |

### Quần

| Size | Chiều cao | Cân nặng |
| --- | --- | --- |
| S | Dưới 168 cm | Dưới 59 kg |
| M | 168–173 cm | 60–69 kg |
| L | 174–177 cm | 70–77 kg |
| XL | 178–185 cm | 78–85 kg |

Tính cỡ theo chiều cao và cân nặng riêng, lấy cỡ lớn hơn cho từng bảng. Chính sách cho mốc chưa rõ trong ảnh: áo ở đúng 175 cm/75 kg ưu tiên XL; khoảng trống giữa các mốc quần xếp lên cỡ kế tiếp, ví dụ 59 kg → M và 173,5 cm → L. Chính sách này được ghi ngay dưới bảng trên website. Ngoài bảng không ép về XL. Tùy chọn mặc rộng tăng một cỡ, nếu vượt XL sẽ yêu cầu đối chiếu thêm số đo.

Ví dụ 170 cm / 65 kg → **áo L, quần M**; 167 cm / 58 kg → **áo M, quần S**; 180 cm / 80 kg → **áo XL, quần XL**. Đây là gợi ý theo bảng, cần đối chiếu thêm vòng ngực/eo và phom sản phẩm để chọn độ vừa.

## Áp dụng size

Gợi ý có ở trang Phối đồ và chi tiết sản phẩm. Nhập cm/kg, bấm Gợi ý, xem hai cỡ rồi bấm Áp dụng. Chi tiết áo chỉ áp dụng cỡ áo; chi tiết quần chỉ áp dụng cỡ quần; outfit hai món áp dụng từng cỡ tương ứng. Chỉ áp dụng khi sản phẩm có đúng cỡ, không tự thay bằng cỡ gần nhất.

Bộ vest dùng một size chung. Chỉ tự áp dụng nếu cả áo và quần cùng được gợi ý một cỡ và sản phẩm có cỡ đó. Khi hai cỡ khác nhau, giải thích để khách đối chiếu bộ hoặc chọn mua hai món riêng. Danh mục hiện tại đã cập nhật áo M/L/XL, quần S/M/L/XL, bộ M/L/XL; size của đơn hàng cũ giữ nguyên.

Thông số chỉ tính trong bộ nhớ trang, không lưu hồ sơ/localStorage và không gửi tới dịch vụ AI. Khi đổi chiều cao/cân nặng/kiểu mặc, kết quả cũ ẩn để tránh dùng nhầm. Máy chủ vẫn xác minh size mua hàng với danh mục, độc lập thông số người dùng.

## Kiểm tra

- Build Release 0 lỗi/0 cảnh báo, JavaScript qua kiểm tra cú pháp.
- 21 kiểm tra quy tắc: ranh giới nhỏ hơn/bao gồm, giao điểm áo 175 cm/75 kg, khoảng trống quần, hai thông số lệch nhau, ngoài bảng, mặc rộng và dữ liệu không hợp lệ.
- Chi tiết áo/quần đề xuất đúng loại. Outfit 170/65 áp dụng áo L/quần M và giỏ giữ đúng hai size. Cỡ không có trong sản phẩm bị chặn áp dụng. Viewport 390 px không tràn ngang, không có lỗi console.
- 20 kiểm tra HTTP hồi quy cho danh mục/ảnh/quyền AI/giỏ/đơn sử dụng JSON thử riêng, không thay đổi tài khoản và đơn thật.

```powershell
node tests/size-guide.checks.cjs
```

AI tạo ảnh trên người mẫu và Studio nền trắng vẫn cần API key; gợi ý size hoạt động độc lập với AI.

