using OutfitStudio.Models;
using OutfitStudio.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using System.Security.Claims;
namespace OutfitStudio.Controllers;

public class ProductController(AppStore db, ProductCatalog catalog) : Controller
{
    public IActionResult Index(string? q, int? category, string? sort)
    {
        var available = catalog.All.Where(x => x.Active).ToList();
        IEnumerable<Product> data = available;
        if (!string.IsNullOrWhiteSpace(q))
        {
            var term = q.Trim();
            data = data.Where(x =>
                x.Name.Contains(term, StringComparison.OrdinalIgnoreCase) ||
                x.Color.Contains(term, StringComparison.OrdinalIgnoreCase) ||
                x.Description.Contains(term, StringComparison.OrdinalIgnoreCase) ||
                x.Sizes.Contains(term, StringComparison.OrdinalIgnoreCase));
        }
        if (category.HasValue)
            data = data.Where(x => x.CategoryId == category.Value);
        data = sort switch
        {
            "price_asc" => data.OrderBy(x => x.Price),
            "price_desc" => data.OrderByDescending(x => x.Price),
            _ => data.OrderByDescending(x => x.Id)
        };
        ViewBag.Categories = db.All<Category>("categories").Where(c => available.Any(p => p.CategoryId == c.Id)).ToList();
        ViewBag.Q = q;
        ViewBag.Category = category;
        ViewBag.Sort = sort;
        return View(data.ToList());
    }
    public IActionResult Details(int id)
    {
        var product = catalog.Find(id);
        if (product == null)
            return NotFound();
        ViewBag.Reviews = db.All<Review>("reviews").Where(x => x.ProductId == id).OrderByDescending(x => x.Id).ToList();
        return View(product);
    }

    [Authorize, HttpPost, ValidateAntiForgeryToken]
    public IActionResult Review(int productId, int rating, string content)
    {
        if (rating is < 1 or > 5 || string.IsNullOrWhiteSpace(content) || content.Length > 2000 || catalog.Find(productId) is null)
            return BadRequest();
        db.Update<Review, int>("reviews", items =>
        {
            var review = new Review { Id = items.Count == 0 ? 1 : items.Max(r => r.Id) + 1, ProductId = productId, UserKey = Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!), UserName = User.Identity?.Name ?? "", Rating = rating, Content = content.Trim(), CreatedAt = DateTime.Now };
            items.Add(review);
            return review.Id;
        });
        TempData["Success"] = "Đã gửi đánh giá sản phẩm.";
        return RedirectToAction(nameof(Details), new
        {
            id = productId
        });
    }
}


