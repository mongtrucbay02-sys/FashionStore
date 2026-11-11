using System.ComponentModel.DataAnnotations;
using System.Globalization;
using System.Security.Cryptography;
using OutfitStudio.Models;

namespace OutfitStudio.Services;

public sealed class QrPaymentService(AppStore store)
{
    public const string Method = "QR Pay";
    private const string SettingsFile = "qr-payment-settings.json";
    public QrPaymentSettings Settings => store.Read<QrPaymentSettings>(SettingsFile).FirstOrDefault() ?? new();
    public bool IsConfigured => IsValid(Settings);
    public static bool ValidAmount(decimal amount) => amount > 0 && amount == decimal.Truncate(amount) && amount <= 9999999999999m;

    public static bool IsValid(QrPaymentSettings settings) => settings.Enabled &&
        Validator.TryValidateObject(settings, new ValidationContext(settings), new List<ValidationResult>(), true);

    public void Save(QrPaymentSettings settings) => store.Modify<QrPaymentSettings>(SettingsFile, items =>
    {
        items.Clear();
        items.Add(settings);
    });

    public OrderQrPayment? Create(int orderId)
    {
        var settings = Settings;
        if (!IsValid(settings))
            return null;
        return new OrderQrPayment
        {
            BankBin = settings.BankBin,
            BankName = settings.BankName,
            AccountNumber = settings.AccountNumber,
            AccountName = settings.AccountName,
            Reference = $"FS{orderId:D6}{Convert.ToHexString(RandomNumberGenerator.GetBytes(4))}"
        };
    }

    // Fixed provider/format, encoded query values, amount from stored order only. No customer contact data in URL.
    public static string? ImageUrl(Order order)
    {
        if (order.PaymentMethod != Method || order.QrPayment is not { } payment ||
            payment.Status == QrPaymentStatus.Paid || OrderStatus.Normalize(order.Status) == OrderStatus.Cancelled ||
            !ValidAmount(order.Total))
            return null;
        return $"https://img.vietqr.io/image/{Uri.EscapeDataString(payment.BankBin)}-{Uri.EscapeDataString(payment.AccountNumber)}-compact2.svg" +
            $"?amount={order.Total.ToString("0", CultureInfo.InvariantCulture)}&addInfo={Uri.EscapeDataString(payment.Reference)}&accountName={Uri.EscapeDataString(payment.AccountName)}";
    }

    public string Report(int orderId, string email)
    {
        var error = "";
        store.Modify<Order>("orders.json", orders =>
        {
            var order = orders.FirstOrDefault(o => o.Id == orderId && o.UserEmail == email);
            if (order?.QrPayment is not { } qr || order.PaymentMethod != Method)
            {
                error = "Không tìm thấy đơn thanh toán QR của bạn.";
                return;
            }
            if (OrderStatus.Normalize(order.Status) == OrderStatus.Cancelled)
            {
                error = "Đơn đã hủy. Hãy liên hệ cửa hàng nếu bạn đã chuyển tiền.";
                return;
            }
            if (qr.Status != QrPaymentStatus.Unpaid)
                return;
            qr.Status = QrPaymentStatus.CustomerReported;
            qr.ReportedAt = DateTimeOffset.UtcNow;
        });
        return error;
    }

    public string Confirm(int orderId, string transactionReference, string admin)
    {
        transactionReference = (transactionReference ?? "").Trim();
        if (transactionReference.Length is < 3 or > 100 || transactionReference.Any(char.IsControl))
            return "Nhập mã giao dịch ngân hàng từ 3 đến 100 ký tự.";
        var error = "";
        store.Modify<Order>("orders.json", orders =>
        {
            var order = orders.FirstOrDefault(o => o.Id == orderId && o.PaymentMethod == Method);
            if (order?.QrPayment is not { } qr)
            {
                error = "Đơn hàng không sử dụng QR Pay.";
                return;
            }
            if (OrderStatus.Normalize(order.Status) == OrderStatus.Cancelled)
            {
                error = "Không thể xác nhận đơn đã hủy. Cần đối soát và xử lý hoàn tiền riêng.";
                return;
            }
            if (qr.Status == QrPaymentStatus.Paid)
            {
                error = "Đơn đã được xác nhận thanh toán.";
                return;
            }
            if (orders.Any(o => o.Id != orderId && o.QrPayment?.Status == QrPaymentStatus.Paid &&
                o.QrPayment.BankTransactionReference.Equals(transactionReference, StringComparison.OrdinalIgnoreCase)))
            {
                error = "Mã giao dịch này đã được dùng để xác nhận một đơn khác.";
                return;
            }
            qr.Status = QrPaymentStatus.Paid;
            qr.PaidAt = DateTimeOffset.UtcNow;
            qr.ConfirmedBy = admin;
            qr.BankTransactionReference = transactionReference;
        });
        return error;
    }

    public bool Reconcile(SePayTransfer transfer)
    {
        var reference = transfer.ReferenceCode?.Trim();
        if (transfer.TransferType != "in" || !ValidAmount(transfer.TransferAmount) ||
            reference is null || reference.Length is < 3 or > 100 || reference.Any(char.IsControl))
            return false;

        var codes = System.Text.RegularExpressions.Regex.Matches(transfer.Content ?? "", "[A-Za-z0-9]+")
            .Select(m => m.Value).Append(transfer.Code ?? "")
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        var matched = false;
        store.Modify<Order>("orders.json", orders =>
        {
            var candidates = orders.Where(o => o.PaymentMethod == Method && o.QrPayment is { } qr &&
                qr.AccountNumber == transfer.AccountNumber && codes.Contains(qr.Reference)).Take(2).ToList();
            if (candidates.Count != 1) return;
            var order = candidates[0];
            var payment = order.QrPayment!;
            if (order.Total != transfer.TransferAmount || OrderStatus.Normalize(order.Status) == OrderStatus.Cancelled)
                return;
            if (payment.Status == QrPaymentStatus.Paid)
            {
                matched = payment.BankTransactionReference.Equals(reference, StringComparison.OrdinalIgnoreCase);
                return;
            }
            if (orders.Any(o => o.QrPayment?.Status == QrPaymentStatus.Paid &&
                o.QrPayment.BankTransactionReference.Equals(reference, StringComparison.OrdinalIgnoreCase)))
                return;

            payment.Status = QrPaymentStatus.Paid;
            payment.PaidAt = DateTimeOffset.UtcNow;
            payment.ConfirmedBy = "SePay";
            payment.BankTransactionReference = reference;
            matched = true;
        });
        return matched;
    }

    public string Reopen(int orderId)
    {
        var error = "";
        store.Modify<Order>("orders.json", orders =>
        {
            var order = orders.FirstOrDefault(o => o.Id == orderId && o.PaymentMethod == Method);
            if (order?.QrPayment is not { Status: QrPaymentStatus.CustomerReported } qr || OrderStatus.Normalize(order.Status) == OrderStatus.Cancelled)
            {
                error = "Chỉ yêu cầu kiểm tra lại đơn QR đang chờ đối soát và chưa hủy.";
                return;
            }
            qr.Status = QrPaymentStatus.Unpaid;
            qr.ReportedAt = null;
        });
        return error;
    }
}

