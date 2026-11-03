namespace OutfitStudio.Models;

public static class StockMovementType
{
    public const string Import = "Nhập kho";
    public const string Adjust = "Điều chỉnh";
    public const string Sale = "Bán hàng";
    public const string Restore = "Hoàn kho";
}

// Một dòng lịch sử thay đổi tồn kho (lưu ở App_Data/stock-movements.json).
public class StockMovement
{
    public int Id
    {
        get; set;
    }
    public int ProductId
    {
        get; set;
    }
    public string ProductName { get; set; } = "";
    public string Size { get; set; } = "";
    public string Type { get; set; } = "";
    public int Change
    {
        get; set;
    }
    public int StockAfter
    {
        get; set;
    }
    public string Note { get; set; } = "";
    public int? OrderId
    {
        get; set;
    }
    public string By { get; set; } = "";
    public DateTime CreatedAt { get; set; } = DateTime.Now;
}
