using System.ComponentModel.DataAnnotations;

namespace OutfitStudio.Models;

public sealed class QrPaymentSettings
{
    public bool Enabled
    {
        get; set;
    }
    [Required(ErrorMessage = "Nhập mã BIN ngân hàng.")]
    [RegularExpression(@"^[0-9]{6}$", ErrorMessage = "BIN ngân hàng gồm 6 chữ số.")]
    public string BankBin { get; set; } = "";
    [Required(ErrorMessage = "Nhập tên ngân hàng."), StringLength(100)]
    public string BankName { get; set; } = "";
    [Required(ErrorMessage = "Nhập số tài khoản nhận tiền.")]
    [RegularExpression(@"^[A-Za-z0-9]{1,19}$", ErrorMessage = "Tài khoản gồm 1–19 chữ hoặc số, không có khoảng trắng.")]
    public string AccountNumber { get; set; } = "";
    [Required(ErrorMessage = "Nhập tên chủ tài khoản."), StringLength(100)]
    public string AccountName { get; set; } = "";
}

public enum QrPaymentStatus
{
    Unpaid, CustomerReported, Paid
}

// Trusted snapshot at checkout: later changes to receiving account do not change existing orders.
public sealed class OrderQrPayment
{
    public string BankBin { get; set; } = "";
    public string BankName { get; set; } = "";
    public string AccountNumber { get; set; } = "";
    public string AccountName { get; set; } = "";
    public string Reference { get; set; } = "";
    public QrPaymentStatus Status
    {
        get; set;
    }
    public DateTimeOffset? ReportedAt
    {
        get; set;
    }
    public DateTimeOffset? PaidAt
    {
        get; set;
    }
    public string ConfirmedBy { get; set; } = "";
    public string BankTransactionReference { get; set; } = "";
    [System.Text.Json.Serialization.JsonIgnore]
    public string StatusLabel => Status switch
    {
        QrPaymentStatus.Paid => "Đã xác nhận thanh toán",
        QrPaymentStatus.CustomerReported => "Chờ đối soát chuyển khoản",
        _ => "Chưa xác nhận thanh toán"
    };
}
