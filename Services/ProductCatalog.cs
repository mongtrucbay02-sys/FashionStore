using OutfitStudio.Models;

namespace OutfitStudio.Services;

public sealed class ProductCatalog(AppStore store)
{
    public OutfitCatalog ReadOutfits(IWebHostEnvironment environment)
    {
        if (store.IsSqlServer)
            return new OutfitCatalog { Products = store.All<OutfitProduct>("outfit-products") };
        var path = Path.Combine(environment.ContentRootPath, "Data", "outfits.json");
        return System.Text.Json.JsonSerializer.Deserialize<OutfitCatalog>(File.ReadAllText(path), new System.Text.Json.JsonSerializerOptions { PropertyNameCaseInsensitive = true }) ?? new();
    }

    public const decimal ShippingFee = 30000;
    public List<Product> All => store.All<Product>("products")
        .Where(p => string.Equals(p.Audience, "men", StringComparison.OrdinalIgnoreCase))
        .ToList();
    public Product? Find(int id) => All.FirstOrDefault(p => p.Id == id && p.Active);

    public bool ValidateCart(List<CartItem> cart, out string error)
    {
        var all = All;
        foreach (var group in cart.GroupBy(i => i.ProductId))
        {
            var p = all.FirstOrDefault(p => p.Id == group.Key && p.Active);
            if (p is null || group.Any(i => !p.AvailableSizes.Contains(i.Size) || i.Quantity < 1))
            {
                error = "Sản phẩm hoặc số lượng trong giỏ không còn phù hợp. Vui lòng kiểm tra lại giỏ hàng.";
                return false;
            }
            foreach (var bySize in group.GroupBy(i => i.Size))
            {
                var have = p.StockOf(bySize.Key);
                if (bySize.Sum(i => (long)i.Quantity) > Math.Min(have, 99))
                {
                    error = $"\"{p.Name}\" size {bySize.Key} chỉ còn {have} sản phẩm. Vui lòng kiểm tra lại giỏ hàng.";
                    return false;
                }
            }
            foreach (var item in group)
            {
                item.Name = p.Name;
                item.Image = p.Image;
                item.Emoji = p.Emoji;
                item.UnitPrice = p.Price;
            }
        }
        error = "";
        return true;
    }
}



