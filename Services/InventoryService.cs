using OutfitStudio.Models;

namespace OutfitStudio.Services;

// Quản lý tồn kho theo từng size: nhập kho, điều chỉnh, trừ kho khi đặt hàng, hoàn kho khi hủy đơn.
// Mọi thay đổi đều ghi lịch sử. AppStore dùng lock có thể vào lại nên gọi lồng nhau là an toàn.
public sealed class InventoryService(AppStore store)
{
    public const int MaxStock = 100000;
    public const int MaxThreshold = 1000;
    public const string OutLabel = "Hết hàng";
    public const string LowLabel = "Sắp hết";
    public const string OkLabel = "Còn hàng";
    private const string MovementsFile = "stock-movements.json";

    // ---------- Trạng thái ----------
    public static string SizeStatus(Product p, string size)
    {
        var n = p.StockOf(size);
        return n <= 0 ? OutLabel : n <= p.EffectiveThreshold ? LowLabel : OkLabel;
    }

    // Hết hàng: mọi size đều hết. Sắp hết: có ít nhất một size chạm ngưỡng (kể cả size đã hết).
    public static string StatusOf(Product p) =>
        p.Stock <= 0 ? OutLabel : p.AvailableSizes.Any(s => SizeStatus(p, s) != OkLabel) ? LowLabel : OkLabel;

    // ---------- Đồng bộ size ----------
    // Đảm bảo SizeStock có đúng các size của sản phẩm và Stock = tổng. Dữ liệu cũ (chưa có SizeStock) được chia đều.
    public static void SyncSizes(Product p)
    {
        var sizes = p.AvailableSizes;
        var next = new Dictionary<string, int>();
        if (p.SizeStock.Count == 0 && sizes.Length > 0)
        {
            var q = p.Stock / sizes.Length;
            var r = p.Stock % sizes.Length;
            for (var i = 0; i < sizes.Length; i++)
                next[sizes[i]] = q + (i < r ? 1 : 0);
        }
        else
        foreach (var s in sizes)
            next[s] = p.SizeStock.TryGetValue(s, out var v) ? v : 0;
        p.SizeStock = next;
        p.Stock = next.Values.Sum();
    }

    private static bool NeedsSync(Product p)
    {
        var sizes = p.AvailableSizes;
        return p.SizeStock.Count != sizes.Length || sizes.Any(s => !p.SizeStock.ContainsKey(s)) || p.Stock != p.SizeStock.Values.Sum();
    }

    // Chạy lúc khởi động: chuyển dữ liệu cũ sang tồn theo size (chỉ ghi file nếu có thay đổi).
    public void Migrate()
    {
        if (!store.All<Product>("products").Any(NeedsSync))
            return;
        store.Atomic(() => store.Update<Product, bool>("products", items =>
        {
            if (store.Read<Product>("inventory-before-sizes.json").Count == 0)
                store.Modify<Product>("inventory-before-sizes.json", backup => backup.AddRange(items));
            foreach (var p in items.Where(NeedsSync))
                SyncSizes(p);
            return true;
        }));
    }

    // ---------- Lịch sử ----------
    public List<StockMovement> Movements() => store.Read<StockMovement>(MovementsFile)
        .OrderByDescending(m => m.CreatedAt).ThenByDescending(m => m.Id).ToList();

    // size rỗng = thay đổi chung cho cả sản phẩm. StockAfter là tồn của size đó (hoặc tổng nếu không có size).
    public void Record(Product p, string size, string type, int change, string note, string by, int? orderId = null) =>
        store.Modify<StockMovement>(MovementsFile, items => items.Add(new StockMovement
        {
            Id = items.Count == 0 ? 1 : items.Max(m => m.Id) + 1,
            ProductId = p.Id,
            ProductName = p.Name,
            Size = size,
            Type = type,
            Change = change,
            StockAfter = size.Length > 0 ? p.StockOf(size) : p.Stock,
            Note = note,
            OrderId = orderId,
            By = by,
            CreatedAt = DateTime.Now
        }));

    // ---------- Thao tác của Admin (trả về chuỗi lỗi, rỗng nếu thành công) ----------
    public string Import(int productId, string size, int quantity, string? note, string by)
    {
        if (quantity is < 1 or > MaxStock)
            return $"Số lượng nhập phải từ 1 đến {MaxStock:N0}.";
        return store.Update<Product, string>("products", products =>
        {
            var p = products.FirstOrDefault(x => x.Id == productId);
            if (p is null)
                return "Sản phẩm không tồn tại.";
            size = (size ?? "").Trim();
            if (!p.SizeStock.TryGetValue(size, out var current))
                return "Size không hợp lệ.";
            if ((long)current + quantity > MaxStock)
                return $"Tồn kho size {size} sau khi nhập vượt quá {MaxStock:N0}.";
            p.SizeStock[size] = current + quantity;
            p.Stock = p.SizeStock.Values.Sum();
            Record(p, size, StockMovementType.Import, quantity, Clean(note, "Nhập kho"), by);
            return "";
        });
    }

    public string SetStock(int productId, string size, int newStock, string? note, string by)
    {
        if (newStock is < 0 or > MaxStock)
            return $"Tồn kho phải từ 0 đến {MaxStock:N0}.";
        return store.Update<Product, string>("products", products =>
        {
            var p = products.FirstOrDefault(x => x.Id == productId);
            if (p is null)
                return "Sản phẩm không tồn tại.";
            size = (size ?? "").Trim();
            if (!p.SizeStock.TryGetValue(size, out var current))
                return "Size không hợp lệ.";
            if (current == newStock)
                return "Tồn kho không thay đổi.";
            p.SizeStock[size] = newStock;
            p.Stock = p.SizeStock.Values.Sum();
            Record(p, size, StockMovementType.Adjust, newStock - current, Clean(note, "Điều chỉnh thủ công"), by);
            return "";
        });
    }

    // threshold = null: quay về ngưỡng mặc định.
    public string SetThreshold(int productId, int? threshold)
    {
        if (threshold is < 0 or > MaxThreshold)
            return $"Ngưỡng cảnh báo phải từ 0 đến {MaxThreshold:N0}.";
        return store.Update<Product, string>("products", products =>
        {
            var p = products.FirstOrDefault(x => x.Id == productId);
            if (p is null)
                return "Sản phẩm không tồn tại.";
            p.LowStockThreshold = threshold;
            return "";
        });
    }

    // ---------- Theo đơn hàng ----------
    // Trừ kho theo từng (sản phẩm, size). Kiểm tra lại và trừ trong cùng một lần khóa để tránh bán vượt tồn.
    public string Deduct(IEnumerable<CartItem> items, int orderId)
    {
        var groups = items.GroupBy(i => (i.ProductId, i.Size)).Select(g => (g.Key.ProductId, g.Key.Size, Qty: g.Sum(i => i.Quantity))).ToList();
        return store.Update<Product, string>("products", products =>
        {
            foreach (var g in groups)
            {
                var p = products.FirstOrDefault(x => x.Id == g.ProductId && x.Active);
                if (p is null || p.Audience != "men")
                    return "Có sản phẩm không còn bán. Vui lòng kiểm tra lại giỏ hàng.";
                if (g.Qty < 1 || !p.AvailableSizes.Contains(g.Size))
                    return "Size hoặc số lượng không hợp lệ.";
                var have = p.StockOf(g.Size);
                if (have < g.Qty)
                    return $"Sản phẩm \"{p.Name}\" size {g.Size} chỉ còn {have} trong kho. Vui lòng kiểm tra lại giỏ hàng.";
            }
            foreach (var g in groups)
            {
                var p = products.First(x => x.Id == g.ProductId);
                p.SizeStock[g.Size] = p.StockOf(g.Size) - g.Qty;
                p.Stock = p.SizeStock.Values.Sum();
                Record(p, g.Size, StockMovementType.Sale, -g.Qty, $"Đơn #{orderId:000}", "Khách hàng", orderId);
            }
            return "";
        });
    }

    // Hoàn kho khi hủy đơn đã được trừ kho.
    public void Restore(Order order, string by) =>
        store.Update<Product, bool>("products", products =>
        {
            foreach (var g in order.Items.Where(i => i.ProductId != 0).GroupBy(i => (i.ProductId, i.Size)))
            {
                var p = products.FirstOrDefault(x => x.Id == g.Key.ProductId);
                if (p is null)
                    continue;
                var qty = g.Sum(i => i.Quantity);
                var before = p.StockOf(g.Key.Size);
                // Returns must preserve every unit even if admin replenished this size to the manual-entry limit.
                p.SizeStock[g.Key.Size] = checked(before + qty);
                p.Stock = p.SizeStock.Values.Sum();
                Record(p, g.Key.Size, StockMovementType.Restore, p.StockOf(g.Key.Size) - before, $"Hủy đơn #{order.Id:000}", by, order.Id);
            }
            return true;
        });

    private static string Clean(string? note, string fallback)
    {
        note = (note ?? "").Trim();
        if (note.Length == 0)
            return fallback;
        return note.Length > 200 ? note[..200] : note;
    }
}

