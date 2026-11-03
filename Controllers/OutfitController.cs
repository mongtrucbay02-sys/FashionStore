using System.Text.Json;
using Microsoft.AspNetCore.Mvc;
using OutfitStudio.Models;
using OutfitStudio.Services;

namespace OutfitStudio.Controllers;

public sealed class OutfitController(IWebHostEnvironment environment, ProductCatalog products) : Controller
{
    private OutfitCatalog ReadCatalog()
    {
        var catalog = products.ReadOutfits(environment);
        var all = products.All;
        catalog.Products.RemoveAll(p => !all.Any(s => s.Id == p.StoreProductId && s.Active));
        foreach (var p in catalog.Products)
        {
            var item = all.First(s => s.Id == p.StoreProductId);
            p.Price = item.Price;
            p.Sizes = item.AvailableSizes.Where(size => item.StockOf(size) > 0).ToArray();
            p.Stock = item.Stock;
            p.Image = item.Image;
        }
        return catalog;
    }

    public IActionResult Index() => View(ReadCatalog());

}

