using OutfitStudio.Models;

namespace OutfitStudio.ViewModels;

public class InventoryViewModel
{
    public List<Product> Products { get; set; } = new();
    public List<Category> Categories { get; set; } = new();
    public string? Search
    {
        get; set;
    }
    public int? CategoryId
    {
        get; set;
    }
    // out | low | ok | alert (hết + sắp hết)
    public string? Status
    {
        get; set;
    }
    public int TotalUnits
    {
        get; set;
    }
    public int OutOfStock
    {
        get; set;
    }
    public int LowStock
    {
        get; set;
    }
}

public class InventoryHistoryViewModel
{
    public List<StockMovement> Movements { get; set; } = new();
    public List<Product> Products { get; set; } = new();
    public int? ProductId
    {
        get; set;
    }
}
