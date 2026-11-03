using System.Globalization;
using System.Text.Json;
using OutfitStudio.Models;

namespace OutfitStudio.Services;

public static class SqlImportVerification
{
    public static Dictionary<string, int> Verify(AppStore store, IConfiguration config, string contentRoot)
    {
        if (!store.IsSqlServer) throw new InvalidOperationException("SQL Server required.");
        var catalog = config["Storage:CatalogDirectory"] ?? Path.Combine(contentRoot, "Data");
        var runtime = config["Storage:RuntimeDirectory"] ?? Path.Combine(contentRoot, "App_Data");
        var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
        List<T> Read<T>(string folder, string file) => File.Exists(Path.Combine(folder, file))
            ? JsonSerializer.Deserialize<List<T>>(File.ReadAllText(Path.Combine(folder, file)), options) ?? [] : [];
        var counts = new Dictionary<string, int>();
        void Compare<T>(string name, IEnumerable<T> expected, IEnumerable<T> actual)
        {
            var left = expected.ToList(); var right = actual.ToList();
            var a = Canonical(JsonSerializer.SerializeToElement(left));
            var b = Canonical(JsonSerializer.SerializeToElement(right));
            if (a != b) throw new InvalidOperationException("SQL import verification failed for " + name + ". Source files remain unchanged.");
            counts[name] = left.Count;
        }
        return store.Atomic(() =>
        {
            Compare("Categories", Read<Category>(catalog,"categories.json").OrderBy(x=>x.Id), store.All<Category>("categories").OrderBy(x=>x.Id));
            var products = Read<Product>(catalog,"products.json");
            foreach (var product in products) { InventoryService.SyncSizes(product); product.Sizes=string.Join(',',product.AvailableSizes); }
            Compare("Products", products.OrderBy(x=>x.Id), store.All<Product>("products").OrderBy(x=>x.Id));
            Compare("Users", Read<User>(runtime,"users.json").OrderBy(x=>x.Id), store.Read<User>("users.json").OrderBy(x=>x.Id));
            Compare("Orders", Read<Order>(runtime,"orders.json").OrderBy(x=>x.Id), store.Read<Order>("orders.json").OrderBy(x=>x.Id));
            Compare("Reviews", Read<Review>(catalog,"reviews.json").OrderBy(x=>x.Id), store.All<Review>("reviews").OrderBy(x=>x.Id));
            Compare("StockMovements", Read<StockMovement>(runtime,"stock-movements.json").OrderBy(x=>x.Id), store.Read<StockMovement>("stock-movements.json").OrderBy(x=>x.Id));
            Compare("QrPaymentSettings", Read<QrPaymentSettings>(runtime,"qr-payment-settings.json"), store.Read<QrPaymentSettings>("qr-payment-settings.json"));
            var outfitPath=Path.Combine(catalog,"outfits.json");
            var outfits=JsonSerializer.Deserialize<OutfitCatalog>(File.ReadAllText(outfitPath),options)?.Products ?? [];
            object Shape(OutfitProduct p)=>new{p.Id,p.StoreProductId,p.Category,p.Kind,p.Name,p.Color,p.ColorName,p.Hex};
            Compare("OutfitProducts",outfits.OrderBy(x=>x.Id).Select(Shape),store.All<OutfitProduct>("outfit-products").OrderBy(x=>x.Id).Select(Shape));
            return counts;
        });
    }

    private static string Canonical(JsonElement node, string? name = null) => node.ValueKind switch
    {
        JsonValueKind.Object => "{" + string.Join(',',node.EnumerateObject().OrderBy(p=>p.Name,StringComparer.Ordinal).Select(p=>JsonSerializer.Serialize(p.Name)+":"+Canonical(p.Value,p.Name))) + "}",
        JsonValueKind.Array => "[" + string.Join(',',node.EnumerateArray().Select(x=>Canonical(x))) + "]",
        JsonValueKind.Number => node.GetDecimal().ToString("G29",CultureInfo.InvariantCulture),
        JsonValueKind.String when name == "CreatedAt" => JsonSerializer.Serialize(node.GetDateTime().ToString("yyyy-MM-ddTHH:mm:ss.fffffff",CultureInfo.InvariantCulture)),
        JsonValueKind.String when name is "EmailVerifiedAt" or "ReportedAt" or "PaidAt" => JsonSerializer.Serialize(node.GetDateTimeOffset().UtcDateTime.ToString("O",CultureInfo.InvariantCulture)),
        _ => node.GetRawText()
    };
}
