# Quản lý tồn kho theo size

Đã ghép chức năng từ `tonkho.zip` vào dự án hiện tại, dùng SQL Server, giữ danh mục nam, ảnh, font, giỏ hàng, phối đồ và gợi ý size.

Admin mở **Tồn kho** để lọc theo tên/danh mục/trạng thái, nhập thêm từng size, đặt lại số lượng, lưu ngưỡng cảnh báo, xem lịch sử và xuất Excel. Mỗi dòng Excel là một sản phẩm/size; lịch sử có thể xuất riêng. Nhập lại cùng một phiếu vẫn là một lần nhập mới, cần tránh gửi lặp khi mạng chậm.

- `Stock` là tổng `SizeStock`. Khi chạy bản mới lần đầu, tổng cũ được chia đều cho các size đang có, phần dư chia từ size đầu tiên, giữ nguyên tổng số lượng. Đây là phân bổ ban đầu để demo, Admin cần kiểm kê lại thực tế.
- Ở bản JSON cũ, bản chụp trước khi chia size lưu tại `App_Data/inventory-before-sizes.json`; migration chạy lại không chia kho lần nữa.
- Thêm sản phẩm: tồn ban đầu áp dụng cho **mỗi size**. Sửa sản phẩm không nhận tồn từ form; nhập/điều chỉnh ở trang Tồn kho. Không xóa size còn hàng hoặc đang có đơn xử lý.
- Hết hàng nếu tổng bằng 0; sắp hết nếu có size bằng/dưới ngưỡng (kể cả size đã hết); ngưỡng mặc định 2/size. Sản phẩm ẩn vẫn xuất hiện trong quản trị kho.
- Giỏ kiểm tra số lượng theo size, lấy giá máy chủ. Checkout kiểm tra và trừ lại trong khóa chung, ghi đơn và lịch sử cùng giao dịch. Không giữ chỗ khi thêm giỏ; khách mua trước có thể làm hết hàng trước khi khách khác thanh toán.
- Hủy đơn hoàn kho đúng một lần khi `StockDeducted=true`. Đơn cũ chưa từng trừ tồn không được cộng kho. Đơn hoàn thành/hủy không chuyển trạng thái tiếp.
- Các thay đổi đơn/kho/lịch sử dùng cùng giao dịch SQL; rollback khi có lỗi. Khóa SQL bảo vệ giữa các kết nối. Các file JSON cũ chỉ là nguồn chuyển dữ liệu/đối chiếu.

Dữ liệu: `dbo.Products`, `dbo.ProductSizes`, `dbo.StockMovements`, `dbo.Orders`. Các trang/thao tác kho chỉ cho Admin, thay đổi dùng POST và CSRF. Excel sử dụng chuỗi OOXML, không xuất công thức từ tên/ghi chú của người dùng.

Kiểm thử chức năng dùng danh mục sao chép và tài khoản mô phỏng, không sửa dữ liệu đang dùng. Bộ kiểm thử bao gồm mua đồng thời khi còn 1 sản phẩm, trừ nhiều dòng thất bại, rollback giao dịch SQL (hồi quy JSON kiểm tra riêng), đặt/hủy đơn, dữ liệu đơn cũ, phân quyền, số lượng sai kiểu và file Excel.


