
namespace OutfitStudio.Models;

public class Order
{
    [System.Text.Json.Serialization.JsonIgnore] public string CustomerName => FullName;
    [System.Text.Json.Serialization.JsonIgnore] public List<OrderItem> OrderItems => Items;
    public int Id
    {
        get; set;
    }
    public string UserEmail { get; set; } = "";
    public List<OrderItem> Items { get; set; } = new();
    public decimal Subtotal
    {
        get; set;
    }
    public decimal ShippingFee
    {
        get; set;
    }
    public decimal Total
    {
        get; set;
    }
    public string FullName { get; set; } = "";
    public string Phone { get; set; } = "";
    public string Address { get; set; } = "";
    public string PaymentMethod { get; set; } = "";
    public OrderQrPayment? QrPayment { get; set; }
    public bool StockDeducted { get; set; }
    public string Status { get; set; } = OrderStatus.Pending;
    public DateTime CreatedAt { get; set; } = DateTime.Now;
}

