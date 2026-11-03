
namespace OutfitStudio.Models;

public class CartItem
{
    public string Image { get; set; } = "";
    public int ProductId
    {
        get; set;
    }
    public string Name { get; set; } = "";
    public string Emoji { get; set; } = "";
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
