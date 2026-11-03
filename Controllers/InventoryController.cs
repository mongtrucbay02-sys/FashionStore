using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using OutfitStudio.Models;
using OutfitStudio.Services;
using OutfitStudio.ViewModels;

namespace OutfitStudio.Controllers;

// Quản lý tồn kho theo size (chỉ Admin). Tách riêng khỏi AdminController.
[Authorize(Roles = "Admin")]
public sealed class InventoryController(AppStore store, InventoryService inventory) : Controller
{
    private string CurrentName => User.Identity?.Name ?? "Admin";
    private List<Category> CategoriesList() => store.All<Category>("categories").OrderBy(c => c.Id).ToList();

    // Lọc dùng chung cho trang danh sách và xuất Excel. status: out | low | ok | alert (hết + sắp hết)
    private List<Product> Filter(string? search, int? categoryId, string? status)
    {
        var items = store.All<Product>("products").AsEnumerable();
        if (!string.IsNullOrWhiteSpace(search))
            items = items.Where(p => p.Name.Contains(search.Trim(), StringComparison.OrdinalIgnoreCase));
        if (categoryId.HasValue)
            items = items.Where(p => p.CategoryId == categoryId.Value);
        items = status switch
        {
            "out" => items.Where(p => InventoryService.StatusOf(p) == InventoryService.OutLabel),
            "low" => items.Where(p => InventoryService.StatusOf(p) == InventoryService.LowLabel),
            "ok" => items.Where(p => InventoryService.StatusOf(p) == InventoryService.OkLabel),
            "alert" => items.Where(p => InventoryService.StatusOf(p) != InventoryService.OkLabel),
            _ => items
        };
        static int Rank(Product p) => InventoryService.StatusOf(p) switch
        {
            InventoryService.OutLabel => 0,
            InventoryService.LowLabel => 1,
            _ => 2
        };
        return items.OrderBy(Rank).ThenBy(p => p.Stock).ThenBy(p => p.Id).ToList();
    }

    public IActionResult Index(string? search, int? categoryId, string? status)
    {
        var all = store.All<Product>("products");
        return View(new InventoryViewModel
        {
            Products = Filter(search, categoryId, status),
            Categories = CategoriesList(),
            Search = search,
            CategoryId = categoryId,
            Status = status,
            TotalUnits = all.Sum(p => p.Stock),
            OutOfStock = all.Count(p => InventoryService.StatusOf(p) == InventoryService.OutLabel),
            LowStock = all.Count(p => InventoryService.StatusOf(p) == InventoryService.LowLabel)
        });
    }

    // Quay lại trang danh sách và giữ nguyên bộ lọc đang dùng (chuỗi truy vấn do form gửi lên).
    private IActionResult Back(string? back) =>
        Redirect(Url.Action(nameof(Index))! + (back is { Length: > 0 and < 300 } && back[0] == '?' ? back : ""));

    [HttpPost, ValidateAntiForgeryToken]
    public IActionResult Import(int id, string size, int amount, string? note, string? back)
    {
        var error = ModelState.IsValid ? inventory.Import(id, size, amount, note, CurrentName) : "Số lượng phải là số nguyên hợp lệ.";
        TempData[error.Length > 0 ? "Error" : "Success"] = error.Length > 0 ? error : "Đã nhập kho.";
        return Back(back);
    }

    [HttpPost, ValidateAntiForgeryToken]
    public IActionResult Adjust(int id, string size, int amount, string? note, string? back)
    {
        var error = ModelState.IsValid ? inventory.SetStock(id, size, amount, note, CurrentName) : "Số lượng phải là số nguyên hợp lệ.";
        TempData[error.Length > 0 ? "Error" : "Success"] = error.Length > 0 ? error : "Đã điều chỉnh tồn kho.";
        return Back(back);
    }

    [HttpPost, ValidateAntiForgeryToken]
    public IActionResult SetThreshold(int id, int? threshold, string? back)
    {
        var error = ModelState.IsValid ? inventory.SetThreshold(id, threshold) : "Ngưỡng cảnh báo phải là số nguyên.";
        TempData[error.Length > 0 ? "Error" : "Success"] = error.Length > 0 ? error : "Đã lưu ngưỡng cảnh báo.";
        return Back(back);
    }

    public IActionResult History(int? productId)
    {
        var movements = inventory.Movements().AsEnumerable();
        if (productId.HasValue)
            movements = movements.Where(m => m.ProductId == productId.Value);
        return View(new InventoryHistoryViewModel
        {
            Movements = movements.Take(300).ToList(),
            ProductId = productId,
            Products = store.All<Product>("products").OrderBy(p => p.Name).ToList()
        });
    }

    // Xuất Excel: mỗi dòng là một (sản phẩm, size), áp dụng bộ lọc đang chọn.
    public IActionResult Export(string? search, int? categoryId, string? status)
    {
        var cats = CategoriesList().ToDictionary(c => c.Id, c => c.Name);
        var rows = new List<object?[]>();
        foreach (var p in Filter(search, categoryId, status))
            foreach (var size in p.AvailableSizes)
                rows.Add([p.Id, p.Name, cats.GetValueOrDefault(p.CategoryId, ""), size, p.StockOf(size), p.EffectiveThreshold,
                          InventoryService.SizeStatus(p, size), p.Active ? "Có" : "Không"]);
        var bytes = XlsxWriter.Build("Tồn kho",
            ["Mã SP", "Sản phẩm", "Danh mục", "Size", "Tồn kho", "Ngưỡng cảnh báo", "Trạng thái", "Đang bán"], rows);
        return File(bytes, XlsxWriter.ContentType, $"ton-kho-{DateTime.Now:yyyyMMdd-HHmm}.xlsx");
    }

    // Xuất toàn bộ lịch sử (không giới hạn 300 dòng như trên màn hình).
    public IActionResult ExportHistory(int? productId)
    {
        var movements = inventory.Movements().AsEnumerable();
        if (productId.HasValue)
            movements = movements.Where(m => m.ProductId == productId.Value);
        var rows = movements.Select(m => new object?[]
        {
            m.CreatedAt, m.ProductId, m.ProductName, m.Size, m.Type, m.Change, m.StockAfter, m.Note,
            m.OrderId.HasValue ? $"#{m.OrderId:000}" : "", m.By
        });
        var bytes = XlsxWriter.Build("Lịch sử tồn kho",
            ["Thời gian", "Mã SP", "Sản phẩm", "Size", "Loại", "Thay đổi", "Tồn sau", "Ghi chú", "Đơn hàng", "Người thực hiện"], rows);
        return File(bytes, XlsxWriter.ContentType, $"lich-su-ton-kho-{DateTime.Now:yyyyMMdd-HHmm}.xlsx");
    }
}

