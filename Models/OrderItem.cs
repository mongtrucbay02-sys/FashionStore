
namespace OutfitStudio.Models;

public class OrderItem
{
    [System.Text.Json.Serialization.JsonIgnore] public decimal Price => UnitPrice;
    public int ProductId
    {
        get; set;
    }
    public string ProductName { get; set; } = "";
    public string Size { get; set; } = "";
    public int Quantity
    {
        get; set;
    }
    public decimal UnitPrice
    {
        get; set;
    }
    public decimal Total => UnitPrice * Quantity;
}
