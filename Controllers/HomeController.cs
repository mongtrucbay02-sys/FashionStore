using OutfitStudio.Models;
using OutfitStudio.Services;
using Microsoft.AspNetCore.Mvc;
namespace OutfitStudio.Controllers;

public class HomeController(ProductCatalog catalog) : Controller
{
    public IActionResult Index()
    {
        return View(catalog.All.Where(x => x.Active).Take(8).ToList());
    }
    public IActionResult Error() => View();
}

