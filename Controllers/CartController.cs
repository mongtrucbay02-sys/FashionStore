using Microsoft.AspNetCore.Mvc;
using OutfitStudio.Helpers;
using OutfitStudio.Models;
using OutfitStudio.Services;
using System.Text.Json;

namespace OutfitStudio.Controllers;

public class CartController(ProductCatalog catalog, IWebHostEnvironment environment) : Controller
{
    public const string CartKey = "CART";
    private List<CartItem> GetCart() => HttpContext.Session.GetObject<List<CartItem>>(CartKey);
    private void SaveCart(List<CartItem> cart) => HttpContext.Session.SetObject(CartKey, cart);

    public IActionResult Index()
    {
        var cart = GetCart();
        var products = catalog.All.ToDictionary(p => p.Id);
        foreach (var item in cart)
            item.Image = products.TryGetValue(item.ProductId, out var current) && current.Active ? current.Image : "";
        if (!catalog.ValidateCart(cart, out var error))
            ViewBag.CartError = error;
        else
            SaveCart(cart);
        ViewBag.Subtotal = cart.Sum(i => i.Total);
        ViewBag.Shipping = cart.Count > 0 ? ProductCatalog.ShippingFee : 0m;
        return View(cart);
    }

    private static void AddItem(List<CartItem> cart, Product p, string size, int quantity)
    {
        var item = cart.FirstOrDefault(i => i.ProductId == p.Id && i.Size == size);
        if (item is not null)
            item.Quantity += quantity;
        else
            cart.Add(new CartItem
            {
                ProductId = p.Id,
                Name = p.Name,
                Image = p.Image,
                Emoji = p.Emoji,
                Size = size,
                Quantity = quantity,
                UnitPrice = p.Price
            });
    }

    [HttpPost, ValidateAntiForgeryToken]
    public IActionResult Add(int productId, string size, int quantity = 1)
    {
        var p = catalog.Find(productId);
        if (p is null || !p.AvailableSizes.Contains(size) || quantity < 1 || quantity > 99)
            return BadRequest();
        var cart = GetCart();
        AddItem(cart, p, size, quantity);
        if (!catalog.ValidateCart(cart, out var error))
        {
            TempData["Error"] = error;
            return RedirectToAction(nameof(Index));
        }
        SaveCart(cart);
        TempData["Success"] = "Đã thêm sản phẩm vào giỏ hàng.";
        return RedirectToAction(nameof(Index));
    }

    [HttpPost, ValidateAntiForgeryToken]
    public IActionResult AddOutfit(string? shirtId, string? pantsId, string? shoesId, string? shirtSize, string? pantsSize, string? shoesSize, string? setId, string? setSize, string mode = "separates")
    {
        var outfits = catalog.ReadOutfits(environment);
        if (mode is not ("set" or "separates"))
            return BadRequest();
        var choices = mode == "set" ? new List<(string?, string, string?)> { (setId, "set", setSize) } : new List<(string?, string, string?)> { (shirtId, "shirt", shirtSize), (pantsId, "pants", pantsSize) };
        if (mode == "separates" && !string.IsNullOrEmpty(shoesId))
            choices.Add((shoesId, "shoes", shoesSize));
        var cart = GetCart();
        foreach (var (id, category, size) in choices)
        {
            var outfit = outfits.Products.FirstOrDefault(p => p.Id == id && p.Category == category);
            var p = outfit is null ? null : catalog.Find(outfit.StoreProductId);
            if (p is null || string.IsNullOrEmpty(size) || !p.AvailableSizes.Contains(size))
                return BadRequest();
            AddItem(cart, p, size, 1);
        }
        if (!catalog.ValidateCart(cart, out var error))
        {
            TempData["Error"] = error;
            return RedirectToAction("Index", "Outfit");
        }
        SaveCart(cart);
        TempData["Success"] = mode == "set" ? "Đã thêm bộ trang phục vào giỏ hàng." : "Đã thêm các món của outfit vào giỏ hàng.";
        return RedirectToAction(nameof(Index));
    }

    [HttpPost, ValidateAntiForgeryToken]
    public IActionResult Change(int productId, string size, int delta)
    {
        if (delta is not (-1 or 1))
            return BadRequest();
        var cart = GetCart();
        var item = cart.FirstOrDefault(i => i.ProductId == productId && i.Size == size);
        if (item is not null)
        {
            item.Quantity += delta;
            if (item.Quantity <= 0)
                cart.Remove(item);
            if (delta > 0 && !catalog.ValidateCart(cart, out var error))
            {
                TempData["Error"] = error;
                return RedirectToAction(nameof(Index));
            }
            SaveCart(cart);
        }
        return RedirectToAction(nameof(Index));
    }

    [HttpPost, ValidateAntiForgeryToken]
    public IActionResult Remove(int productId, string size)
    {
        var cart = GetCart();
        cart.RemoveAll(i => i.ProductId == productId && i.Size == size);
        SaveCart(cart);
        return RedirectToAction(nameof(Index));
    }
}

