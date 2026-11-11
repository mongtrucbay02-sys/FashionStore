using System.Security.Claims;
using System.Text.Json;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using OutfitStudio.Models;
using OutfitStudio.Services;
using OutfitStudio.ViewModels;

namespace OutfitStudio.Controllers;

[Authorize(Roles = "Admin")]
[ResponseCache(Location = ResponseCacheLocation.None, NoStore = true)]
public sealed class AdminController(AppStore store, IWebHostEnvironment environment, InventoryService inventory, QrPaymentService payments, ProductCatalog catalogService, IConfiguration configuration) : Controller
{
    private string CurrentName => User.Identity?.Name ?? "Admin";
    private Guid CurrentUserId => Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
    private List<Category> CategoriesList() => store.All<Category>("categories").OrderBy(c => c.Id).ToList();
    private List<Order> OrdersList()
    {
        var orders = store.Read<Order>("orders.json");
        foreach (var order in orders)
            order.Status = OrderStatus.Normalize(order.Status);
        return orders;
    }

    public IActionResult Index() => RedirectToAction(nameof(Dashboard));
    public IActionResult Dashboard()
    {
        var products = store.All<Product>("products");
        var orders = OrdersList();
        var vm = new DashboardViewModel
        {
            TotalProducts = products.Count,
            TotalOrders = orders.Count,
            TotalUsers = store.Read<User>("users.json").Count(u => !u.IsDeleted),
            Revenue = orders.Where(o => o.Status == OrderStatus.Completed).Sum(o => o.Total),
            PendingOrders = orders.Count(o => o.Status == OrderStatus.Pending),
            LowStockProducts = products.Count(p => InventoryService.StatusOf(p) != InventoryService.OkLabel),
            RecentOrders = orders.OrderByDescending(o => o.CreatedAt).Take(5).ToList()
        };
        var completed = orders.Where(o => o.Status == OrderStatus.Completed).ToList();
        var today = DateTime.UtcNow.AddHours(7).Date;
        for (var day = today.AddDays(-13); day <= today; day = day.AddDays(1))
            vm.DailyRevenue.Add((day.ToString("dd/MM"), completed
                .Where(o => o.CreatedAt.ToUniversalTime().AddHours(7).Date == day).Sum(o => o.Total)));
        vm.BestSellers = completed.SelectMany(o => o.Items).GroupBy(i => i.ProductId)
            .Select(g => (Name: g.First().ProductName, Quantity: g.Sum(i => (long)i.Quantity)))
            .OrderByDescending(i => i.Quantity).ThenBy(i => i.Name).Take(5).ToList();
        return View(vm);
    }

    [HttpPost, ValidateAntiForgeryToken]
    public IActionResult SetProductVisibility(int id, bool? active)
    {
        if (!ModelState.IsValid || active is null) return BadRequest();
        var found = store.Update<Product, bool>("products", products =>
        {
            var product = products.FirstOrDefault(p => p.Id == id);
            if (product is null) return false;
            product.Active = active.Value;
            return true;
        });
        return found ? RedirectToAction(nameof(Products)) : NotFound();
    }

    public IActionResult Products(string? search, int? categoryId)
    {
        var products = store.All<Product>("products").AsEnumerable();
        if (!string.IsNullOrWhiteSpace(search))
        {
            var term = search.Trim();
            products = products.Where(p =>
                p.Name.Contains(term, StringComparison.OrdinalIgnoreCase) ||
                p.Color.Contains(term, StringComparison.OrdinalIgnoreCase) ||
                p.Description.Contains(term, StringComparison.OrdinalIgnoreCase) ||
                p.Sizes.Contains(term, StringComparison.OrdinalIgnoreCase));
        }
        if (categoryId.HasValue)
            products = products.Where(p => p.CategoryId == categoryId.Value);
        return View(new ProductListViewModel { Products = products.OrderBy(p => p.Id).ToList(), Categories = CategoriesList(), Search = search, CategoryId = categoryId });
    }
    public IActionResult ProductDetails(int id)
    {
        var p = store.All<Product>("products").FirstOrDefault(p => p.Id == id);
        if (p is null)
            return NotFound();
        ViewBag.CategoryName = CategoriesList().FirstOrDefault(c => c.Id == p.CategoryId)?.Name ?? "—";
        return View(p);
    }
    [HttpGet] public IActionResult CreateProduct() => View("ProductForm", new ProductFormViewModel { Categories = CategoriesList() });
    [HttpPost, ValidateAntiForgeryToken]
    public IActionResult CreateProduct(Product product, IFormFile? imageFile, int initialStock)
    {
        var valid = ValidateProduct(product);
        if (initialStock is < 0 or > InventoryService.MaxStock)
        {
            ModelState.AddModelError("Product.Stock", $"Tồn kho ban đầu cần từ 0 đến {InventoryService.MaxStock:N0}.");
            valid = false;
        }
        if (!valid || !ApplyImage(product, imageFile))
            return ProductForm(product, initialStock);
        // Tồn kho ban đầu áp dụng cho từng size.
        product.SizeStock = product.AvailableSizes.ToDictionary(s => s, _ => initialStock);
        product.Stock = product.SizeStock.Values.Sum();
        store.Update<Product, int>("products", items => { product.Id = items.Count == 0 ? 1 : items.Max(p => p.Id) + 1; items.Add(product); return product.Id; });
        if (initialStock > 0)
            foreach (var size in product.AvailableSizes)
                inventory.Record(product, size, StockMovementType.Import, initialStock, "Tồn kho ban đầu", CurrentName);
        TempData["Success"] = "Đã thêm sản phẩm.";
        return RedirectToAction(nameof(Products));
    }
    [HttpGet]
    public IActionResult EditProduct(int id)
    {
        var p = store.All<Product>("products").FirstOrDefault(p => p.Id == id);
        return p is null ? NotFound() : ProductForm(p);
    }
    [HttpPost, ValidateAntiForgeryToken]
    public IActionResult EditProduct(Product product, IFormFile? imageFile)
    {
        if (!store.All<Product>("products").Any(p => p.Id == product.Id))
            return NotFound();
        if (!ValidateProduct(product))
            return ProductForm(product);
        var existing = store.All<Product>("products").First(p => p.Id == product.Id);
        var removed = existing.AvailableSizes.Except(product.AvailableSizes).ToArray();
        if (removed.Any(s => existing.StockOf(s) > 0) ||
            OrdersList().Any(o => o.StockDeducted && o.Status != OrderStatus.Completed && o.Items.Any(i => i.ProductId == product.Id && removed.Contains(i.Size))))
        {
            ModelState.AddModelError("Product.Sizes", "Không thể xóa size còn hàng hoặc đang có đơn xử lý. Hãy điều chỉnh kho và xử lý đơn trước.");
            return ProductForm(product);
        }
        if (!ApplyImage(product, imageFile))
            return ProductForm(product);
        // Tồn kho không sửa ở đây (quản lý ở mục Tồn kho); chỉ đồng bộ lại khi danh sách size thay đổi.
        Product? stored = null;
        var stockDelta = 0;
        store.Update<Product, bool>("products", items =>
        {
            var p = items.FirstOrDefault(p => p.Id == product.Id);
            if (p is null)
                return false;
            var before = p.Stock;
            p.Name = product.Name;
            p.Price = product.Price;
            p.CategoryId = product.CategoryId;
            p.Image = product.Image;
            p.Color = product.Color;
            p.Sizes = product.Sizes;
            p.Description = product.Description;
            p.Active = product.Active;
            p.Audience = "men";
            p.LowStockThreshold = product.LowStockThreshold;
            InventoryService.SyncSizes(p);
            stored = p;
            stockDelta = p.Stock - before;
            return true;
        });
        if (stored is not null && stockDelta != 0)
            inventory.Record(stored, "", StockMovementType.Adjust, stockDelta, "Đổi danh sách size", CurrentName);
        TempData["Success"] = "Đã cập nhật sản phẩm.";
        return RedirectToAction(nameof(Products));
    }
    [HttpPost, ValidateAntiForgeryToken]
    public IActionResult DeleteProduct(int id)
    {
        var result = store.Update<Product, string>("products", products =>
        {
            var p = products.FirstOrDefault(p => p.Id == id);
            if (p is null)
                return "";
            if (store.Read<Order>("orders.json").Any(o => o.Items.Any(i => i.ProductId == id || (i.ProductId == 0 && i.ProductName == p.Name))))
                return "Sản phẩm đã có trong đơn hàng. Hãy ẩn sản phẩm để giữ lịch sử.";
            var catalog = catalogService.ReadOutfits(environment);
            if (catalog?.Products.Any(o => o.StoreProductId == id) == true)
                return "Sản phẩm đang dùng trong phối đồ. Hãy ẩn sản phẩm thay vì xóa.";
            products.Remove(p);
            return "";
        });
        TempData[result.Length > 0 ? "Error" : "Success"] = result.Length > 0 ? result : "Đã xóa sản phẩm.";
        return RedirectToAction(nameof(Products));
    }

    private IActionResult ProductForm(Product p, int initialStock = 0)
    {
        // Form sửa không gửi tồn kho lên; nạp lại để hiển thị đúng khi form báo lỗi.
        if (p.Id != 0 && p.SizeStock.Count == 0)
        {
            var s = store.All<Product>("products").FirstOrDefault(x => x.Id == p.Id);
            if (s is not null)
            {
                p.Stock = s.Stock;
                p.SizeStock = s.SizeStock;
            }
        }
        return View("ProductForm", new ProductFormViewModel { Product = p, Categories = CategoriesList(), InitialStock = initialStock });
    }
    private bool ValidateProduct(Product p)
    {
        p.Audience = "men";
        var parseErrors = ModelState.Where(x => x.Key is "initialStock" or "Price" or "LowStockThreshold" or "CategoryId" or "Id").Any(x => x.Value?.Errors.Count > 0);
        ModelState.Clear();
        p.Name = (p.Name ?? "").Trim();
        p.Description ??= "";
        p.Color ??= "";
        p.Image ??= "";
        p.Sizes = (p.Sizes ?? "").Trim();
        p.Sizes = string.Join(",", p.AvailableSizes);
        if (parseErrors)
            ModelState.AddModelError("Product.Price", "Giá, ngưỡng cảnh báo và danh mục phải là số hợp lệ.");
        if (p.Name.Length is < 1 or > 200)
            ModelState.AddModelError("Product.Name", "Tên sản phẩm cần từ 1 đến 200 ký tự.");
        if (p.Price is < 1000 or > 100000000)
            ModelState.AddModelError("Product.Price", "Giá cần từ 1.000 đến 100.000.000 đ.");
        if (p.LowStockThreshold is < 0 or > InventoryService.MaxThreshold)
            ModelState.AddModelError("Product.LowStockThreshold", $"Ngưỡng cảnh báo cần từ 0 đến {InventoryService.MaxThreshold:N0}.");
        if (!CategoriesList().Any(c => c.Id == p.CategoryId))
            ModelState.AddModelError("Product.CategoryId", "Vui lòng chọn danh mục.");
        if (p.AvailableSizes.Length is < 1 or > 20 || p.Sizes.Length > 200)
            ModelState.AddModelError("Product.Sizes", "Nhập từ 1 đến 20 size, cách nhau bằng dấu phẩy.");
        return ModelState.ErrorCount == 0;
    }
    private bool ApplyImage(Product p, IFormFile? file)
    {
        if (file is { Length: > 0 })
        {
            var ext = Path.GetExtension(file.FileName).ToLowerInvariant();
            if (!new[] { ".jpg", ".jpeg", ".png", ".webp" }.Contains(ext) || file.Length > 5 * 1024 * 1024)
            {
                ModelState.AddModelError("Product.ImageUrl", "Chọn ảnh JPG, PNG hoặc WEBP, tối đa 5 MB.");
                return false;
            }
            var folder = Path.Combine(environment.WebRootPath, "images", "products");
            Directory.CreateDirectory(folder);
            var name = Guid.NewGuid().ToString("N") + ext;
            using var stream = System.IO.File.Create(Path.Combine(folder, name));
            file.CopyTo(stream);
            p.Image = "/images/products/" + name;
            return true;
        }
        p.Image = p.Image.Trim();
        var local = p.Image.StartsWith('/') && !p.Image.StartsWith("//") && !p.Image.Contains('\\');
        var remote = Uri.TryCreate(p.Image, UriKind.Absolute, out var uri) && uri.Scheme is "http" or "https";
        if (p.Image.Length > 1000 || (p.Image.Length > 0 && !local && !remote))
        {
            ModelState.AddModelError("Product.ImageUrl", "Link ảnh phải là đường dẫn /... hoặc http(s)://..., tối đa 1.000 ký tự.");
            return false;
        }
        return true;
    }

    public IActionResult Categories()
    {
        ViewBag.ProductCounts = store.All<Product>("products").GroupBy(p => p.CategoryId).ToDictionary(g => g.Key, g => g.Count());
        return View(CategoriesList());
    }
    [HttpGet] public IActionResult CreateCategory() => View("CategoryForm", new Category());
    [HttpGet]
    public IActionResult EditCategory(int id)
    {
        var c = CategoriesList().FirstOrDefault(c => c.Id == id);
        return c is null ? NotFound() : View("CategoryForm", c);
    }
    [HttpPost, ValidateAntiForgeryToken] public IActionResult CreateCategory(Category category) => SaveCategory(category, true);
    [HttpPost, ValidateAntiForgeryToken] public IActionResult EditCategory(Category category) => SaveCategory(category, false);
    private IActionResult SaveCategory(Category category, bool create)
    {
        category.Name = (category.Name ?? "").Trim();
        category.Description ??= "";
        if (category.Name.Length is < 1 or > 100)
        {
            ModelState.AddModelError("Name", "Tên danh mục cần từ 1 đến 100 ký tự.");
            return View("CategoryForm", category);
        }
        var result = store.Update<Category, string>("categories", items =>
        {
            if (items.Any(c => c.Name.Equals(category.Name, StringComparison.OrdinalIgnoreCase) && (create || c.Id != category.Id)))
                return "Tên danh mục đã tồn tại.";
            if (create)
            {
                category.Id = items.Count == 0 ? 1 : items.Max(c => c.Id) + 1;
                items.Add(category);
            }
            else
            {
                var c = items.FirstOrDefault(c => c.Id == category.Id);
                if (c is null)
                    return "Danh mục không tồn tại.";
                c.Name = category.Name;
                c.Description = category.Description;
            }
            return "";
        });
        if (result.Length > 0)
        {
            ModelState.AddModelError("Name", result);
            return View("CategoryForm", category);
        }
        TempData["Success"] = "Đã lưu danh mục.";
        return RedirectToAction(nameof(Categories));
    }
    [HttpPost, ValidateAntiForgeryToken]
    public IActionResult DeleteCategory(int id)
    {
        var deleted = store.Update<Category, bool>("categories", items =>
        {
            if (store.All<Product>("products").Any(p => p.CategoryId == id))
                return false;
            items.RemoveAll(c => c.Id == id);
            return true;
        });
        TempData[deleted ? "Success" : "Error"] = deleted ? "Đã xóa danh mục." : "Danh mục còn sản phẩm nên không thể xóa.";
        return RedirectToAction(nameof(Categories));
    }

    public IActionResult Orders(string? status)
    {
        ViewBag.Status = status;
        var orders = OrdersList().AsEnumerable();
        if (!string.IsNullOrEmpty(status))
            orders = orders.Where(o => o.Status == status);
        return View(orders.OrderByDescending(o => o.CreatedAt).ToList());
    }
    public IActionResult OrderDetails(int id)
    {
        var order = OrdersList().FirstOrDefault(o => o.Id == id);
        return order is null ? NotFound() : View(order);
    }
    [HttpPost, ValidateAntiForgeryToken]
    public IActionResult UpdateOrderStatus(int id, string status)
    {
        if (!OrderStatus.All.Contains(status))
            return BadRequest();
        var error = "";
        store.Modify<Order>("orders.json", orders =>
        {
            var order = orders.FirstOrDefault(o => o.Id == id);
            if (order is null)
            {
                error = "Đơn hàng không tồn tại.";
                return;
            }
            order.Status = OrderStatus.Normalize(order.Status);
            if (order.PaymentMethod == QrPaymentService.Method && order.QrPayment?.Status != QrPaymentStatus.Paid && status != OrderStatus.Cancelled)
            {
                error = "Cần đối soát và xác nhận đã nhận đủ tiền QR trước khi xử lý đơn.";
                return;
            }
            var index = Array.IndexOf(OrderStatus.Flow, order.Status);
            var next = index >= 0 && index < OrderStatus.Flow.Length - 1 ? OrderStatus.Flow[index + 1] : "";
            if (order.Status is OrderStatus.Completed or OrderStatus.Cancelled || (status != OrderStatus.Cancelled && status != next))
            {
                error = "Chỉ được chuyển sang bước tiếp theo hoặc hủy đơn đang xử lý.";
                return;
            }
            // Chỉ hoàn kho cho đơn đã được trừ kho; đơn cũ chưa từng trừ nên không cộng lại.
            if (status == OrderStatus.Cancelled && order.StockDeducted)
            {
                inventory.Restore(order, CurrentName);
                order.StockDeducted = false;
            }
            order.Status = status;
        });
        TempData[error.Length > 0 ? "Error" : "Success"] = error.Length > 0 ? error : "Đã cập nhật trạng thái đơn hàng.";
        return RedirectToAction(nameof(OrderDetails), new
        {
            id
        });
    }

    [HttpPost, ValidateAntiForgeryToken]
    public IActionResult ConfirmQrPayment(int id, string? transactionReference, bool received)
    {
        var error = !ModelState.IsValid || !received ? "Hãy kiểm tra và xác nhận đã nhận đủ tiền trong tài khoản ngân hàng." : payments.Confirm(id, transactionReference ?? "", CurrentName);
        TempData[error.Length > 0 ? "Error" : "Success"] = error.Length > 0 ? error : "Đã xác nhận thanh toán QR sau đối soát.";
        return RedirectToAction(nameof(OrderDetails), new
        {
            id
        });
    }

    [HttpPost, ValidateAntiForgeryToken]
    public IActionResult ReopenQrPayment(int id)
    {
        var error = payments.Reopen(id);
        TempData[error.Length > 0 ? "Error" : "Success"] = error.Length > 0 ? error : "Đã yêu cầu khách kiểm tra lại giao dịch chuyển khoản.";
        return RedirectToAction(nameof(OrderDetails), new
        {
            id
        });
    }

    private bool IsRoot(User user) => user.Email.Equals(
        (configuration["Admin:RootEmail"] ?? configuration["AdminSeed:Email"] ?? "admin@fashionstore.local").Trim(),
        StringComparison.OrdinalIgnoreCase);

    public IActionResult Users()
    {
        var users = store.Read<User>("users.json").Where(u => !u.IsDeleted).OrderBy(u => u.Email).ToList();
        ViewBag.RootId = users.FirstOrDefault(u => u.Role == "Admin" && IsRoot(u))?.Id;
        ViewBag.CurrentUserId = CurrentUserId;
        ViewBag.CanManageAdmins = users.Any(u => u.Id == CurrentUserId && IsRoot(u));
        return View(users);
    }

    [HttpPost, ValidateAntiForgeryToken]
    public IActionResult DeleteUser(Guid id) => ManageUser(id, user =>
    {
        user.IsLocked = true;
        user.Role = "User";
        user.PasswordHash = "";
        user.FullName = "";
        user.Phone = "";
        user.Address = "";
        user.EmailVerifiedAt = null;
    }, "Đã xóa tài khoản.");

    private IActionResult ManageUser(Guid id, Action<User> change, string success, bool changesRole = false)
    {
        var changed = false;
        store.Modify<User>("users.json", users =>
        {
            var target = users.FirstOrDefault(u => u.Id == id && !u.IsDeleted);
            var actor = users.FirstOrDefault(u => u.Id == CurrentUserId && !u.IsDeleted && !u.IsLocked && u.Role == "Admin");
            if (actor is null || target is null || target.Id == actor.Id || IsRoot(target) ||
                ((changesRole || target.Role == "Admin") && !IsRoot(actor)))
                return;
            change(target);
            changed = true;
        });
        TempData[changed ? "Success" : "Error"] = changed ? success : "Bạn không thể thao tác với tài khoản đã chọn.";
        return RedirectToAction(nameof(Users));
    }
    public IActionResult UserDetails(Guid id)
    {
        var user = store.Read<User>("users.json").FirstOrDefault(u => u.Id == id && !u.IsDeleted);
        if (user is null)
            return NotFound();
        ViewBag.Orders = OrdersList().Where(o => o.UserEmail == user.Email).OrderByDescending(o => o.CreatedAt).ToList();
        return View(user);
    }
    [HttpPost, ValidateAntiForgeryToken]
    public IActionResult ToggleLock(Guid id) =>
        ManageUser(id, user => user.IsLocked = !user.IsLocked, "Đã thay đổi trạng thái khóa tài khoản.");
    [HttpPost, ValidateAntiForgeryToken]
    public IActionResult ChangeRole(Guid id, string role) =>
        role is "Admin" or "User"
            ? ManageUser(id, user => user.Role = role, "Đã cập nhật quyền tài khoản.", changesRole: true)
            : BadRequest();
    public IActionResult Reviews()
    {
        ViewBag.ProductNames = store.All<Product>("products").ToDictionary(p => p.Id, p => p.Name);
        return View(store.All<Review>("reviews").OrderByDescending(r => r.CreatedAt).ToList());
    }
    [HttpPost, ValidateAntiForgeryToken]
    public IActionResult DeleteReview(int id)
    {
        store.Update<Review, int>("reviews", items => items.RemoveAll(r => r.Id == id));
        TempData["Success"] = "Đã xóa đánh giá.";
        return RedirectToAction(nameof(Reviews));
    }
}




