namespace OutfitStudio.Models;

public static class OrderStatus
{
    public const string Pending = "Chờ xác nhận";
    public const string Confirmed = "Đã xác nhận";
    public const string Shipping = "Đang giao";
    public const string Completed = "Hoàn thành";
    public const string Cancelled = "Đã hủy";
    public static readonly string[] Flow = [Pending, Confirmed, Shipping, Completed];
    public static readonly string[] All = [Pending, Confirmed, Shipping, Completed, Cancelled];
    public static string Normalize(string value) => value == "Đang xử lý" ? Pending : value;
}
