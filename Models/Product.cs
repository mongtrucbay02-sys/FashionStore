using System.ComponentModel.DataAnnotations;

namespace OutfitStudio.Models;

public class Product
{
    [System.Text.Json.Serialization.JsonIgnore]
    public string ImageUrl
    {
        get => Image; set => Image = value;
    }
    public int Id
    {
        get; set;
    }
    [Required] public string Name { get; set; } = "";
    public string Description { get; set; } = "";
    [Range(1000, 100000000)]
    public decimal Price
    {
        get; set;
    }
    public int CategoryId
    {
        get; set;
    }
    public string Image { get; set; } = "";
    public string Color { get; set; } = "";
    public string Sizes { get; set; } = "S,M,L,XL";
    public const int DefaultLowStockThreshold = 2;
    // Tổng tồn = tổng SizeStock (luôn được đồng bộ bởi InventoryService).
    public int Stock
    {
        get; set;
    }
    // Tồn theo từng size, khóa là tên size (S, M, 39...).
    public Dictionary<string, int> SizeStock { get; set; } = new();
    // Ngưỡng cảnh báo "sắp hết" áp dụng cho từng size; null = dùng mặc định.
    public int? LowStockThreshold
    {
        get; set;
    }
    [System.Text.Json.Serialization.JsonIgnore] public int EffectiveThreshold => LowStockThreshold ?? DefaultLowStockThreshold;
    // Dữ liệu cũ chưa có SizeStock thì coi tồn chung áp dụng cho mọi size.
    public int StockOf(string size) => SizeStock.Count == 0 ? Stock : (SizeStock.TryGetValue(size, out var n) ? n : 0);
    public bool Active { get; set; } = true;
    public string Audience { get; set; } = "men";
    public string Emoji => CategoryId switch { 2 => "👖", 3 => "👟", _ => "👕" };
    public string[] AvailableSizes => Sizes.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).Distinct().ToArray();
}

