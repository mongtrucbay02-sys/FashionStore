namespace OutfitStudio.Models;

public sealed class OutfitCatalog
{
    public List<OutfitProduct> Products { get; set; } = [];
}

public sealed class OutfitProduct
{
    public string Image { get; set; } = "";
    public int Stock
    {
        get; set;
    }
    public string[] Sizes { get; set; } = [];
    public int StoreProductId
    {
        get; set;
    }
    public string Id { get; set; } = "";
    public string Category { get; set; } = "";
    public string Kind { get; set; } = "";
    public string Name { get; set; } = "";
    public string Color { get; set; } = "";
    public string ColorName { get; set; } = "";
    public string Hex { get; set; } = "";
    public decimal Price
    {
        get; set;
    }
}
