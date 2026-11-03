using OutfitStudio.ViewModels;
using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using OutfitStudio.Helpers;
using OutfitStudio.Models;
using OutfitStudio.Services;

namespace OutfitStudio.Controllers;

[Authorize]
[ResponseCache(Location = ResponseCacheLocation.None, NoStore = true)]
public class OrderController : Controller
{
    private const string OrdersFile = "orders.json";
    private readonly AppStore _store;
    private readonly ProductCatalog _catalog;
    private readonly InventoryService _inventory;
    private readonly QrPaymentService _payments;

    public OrderController(AppStore store, ProductCatalog catalog, InventoryService inventory, QrPaymentService payments)
    {
        _store = store;
        _catalog = catalog;
        _inventory = inventory;
        _payments = payments;
    }

    private string CurrentEmail => User.FindFirstValue(ClaimTypes.Email) ?? "";
    private List<CartItem> GetCart() => HttpContext.Session.GetObject<List<CartItem>>(CartController.CartKey);

    // Trang thông tin nhận hàng
    [HttpGet]
    public IActionResult Checkout()
    {
        var cart = GetCart();
        if (!cart.Any())
            return RedirectToAction("Index", "Cart");
        if (!_catalog.ValidateCart(cart, out var error))
        {
            TempData["Error"] = error;
            return RedirectToAction("Index", "Cart");
        }
        return View(BuildVM(new CheckoutVM { FullName = User.Identity?.Name ?? "" }, cart));
    }

    // Đặt hàng
    [HttpPost, ValidateAntiForgeryToken]
    public IActionResult Checkout(CheckoutVM vm)
    {
        var cart = GetCart();
        if (!cart.Any())
            return RedirectToAction("Index", "Cart");

        if (!_catalog.ValidateCart(cart, out var error))
        {
            TempData["Error"] = error;
            return RedirectToAction("Index", "Cart");
        }
        if (vm.PaymentMethod != "COD" && vm.PaymentMethod != QrPaymentService.Method)
            ModelState.AddModelError(nameof(vm.PaymentMethod), "Phương thức không hợp lệ");
        if (vm.PaymentMethod == QrPaymentService.Method && (!_payments.IsConfigured || !QrPaymentService.ValidAmount(cart.Sum(i => i.Total) + ProductCatalog.ShippingFee)))
            ModelState.AddModelError(nameof(vm.PaymentMethod), "QR Pay chưa sẵn sàng cho đơn này. Vui lòng chọn COD hoặc liên hệ cửa hàng.");

        if (!ModelState.IsValid)
            return View(BuildVM(vm, cart));

        var subtotal = cart.Sum(i => i.Total);
        var order = new Order
        {
            UserEmail = CurrentEmail,
            FullName = vm.FullName.Trim(),
            Phone = vm.Phone.Trim(),
            Address = vm.Address.Trim(),
            PaymentMethod = vm.PaymentMethod,
            Subtotal = subtotal,
            ShippingFee = ProductCatalog.ShippingFee,
            Total = subtotal + ProductCatalog.ShippingFee,
            Items = cart.Select(i => new OrderItem
            {
                ProductId = i.ProductId,
                ProductName = i.Name,
                Size = i.Size,
                Quantity = i.Quantity,
                UnitPrice = i.UnitPrice
            }).ToList()
        };

        var stockError = "";
        _store.Modify<Order>(OrdersFile, orders =>
        {
            order.Id = orders.Any() ? orders.Max(o => o.Id) + 1 : 1;
            if (order.PaymentMethod == QrPaymentService.Method)
            {
                order.QrPayment = _payments.Create(order.Id);
                if (order.QrPayment is null)
                {
                    stockError = "Cửa hàng vừa tắt QR Pay. Vui lòng chọn phương thức khác.";
                    return;
                }
            }
            stockError = _inventory.Deduct(cart, order.Id); // trừ kho cùng lúc với lưu đơn
            if (stockError.Length > 0)
                return;
            order.StockDeducted = true;
            orders.Add(order);
        });
        if (stockError.Length > 0)
        {
            TempData["Error"] = stockError;
            return RedirectToAction("Index", "Cart");
        }

        HttpContext.Session.Remove(CartController.CartKey);
        TempData["Success"] = $"Đặt hàng thành công! Mã đơn #{order.Id:D3}";
        if (order.PaymentMethod == QrPaymentService.Method)
            return RedirectToAction(nameof(Detail), new
            {
                id = order.Id
            });
        return RedirectToAction(nameof(History));
    }

    // Lịch sử đơn
    public IActionResult History()
    {
        var orders = _store.Read<Order>(OrdersFile)
            .Where(o => o.UserEmail == CurrentEmail)
            .OrderByDescending(o => o.CreatedAt)
            .ToList();
        return View(orders);
    }

    // Chi tiết đơn
    public IActionResult Detail(int id)
    {
        var order = _store.Read<Order>(OrdersFile)
            .FirstOrDefault(o => o.Id == id && o.UserEmail == CurrentEmail); // chỉ xem đơn của chính mình
        if (order == null)
            return NotFound();
        return View(order);
    }

    [HttpPost, ValidateAntiForgeryToken]
    public IActionResult ReportTransfer(int id)
    {
        if (!_store.Read<Order>(OrdersFile).Any(o => o.Id == id && o.UserEmail == CurrentEmail))
            return NotFound();
        var error = _payments.Report(id, CurrentEmail);
        TempData[error.Length > 0 ? "Error" : "Success"] = error.Length > 0 ? error : "Đã ghi nhận thông báo. Cửa hàng sẽ đối soát, đơn chưa tự động được xác nhận thanh toán.";
        return RedirectToAction(nameof(Detail), new
        {
            id
        });
    }

    private CheckoutVM BuildVM(CheckoutVM vm, List<CartItem> cart)
    {
        vm.Items = cart;
        vm.Subtotal = cart.Sum(i => i.Total);
        vm.ShippingFee = ProductCatalog.ShippingFee;
        vm.QrPayAvailable = _payments.IsConfigured && QrPaymentService.ValidAmount(vm.Total);
        return vm;
    }
}



