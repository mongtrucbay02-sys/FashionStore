using System.Text.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.FileProviders;
using OutfitStudio.Controllers;
using OutfitStudio.Models;
using OutfitStudio.Services;

var root=Path.GetFullPath(Path.Combine(Directory.GetCurrentDirectory(),".test-output","menswear-checks",Guid.NewGuid().ToString("N")));
Directory.CreateDirectory(Path.Combine(root,"Data"));int count=0;
void Assert(bool value,string name){if(!value)throw new Exception(name);count++;Console.WriteLine("PASS: "+name);}
var env=new TestEnvironment(root);var config=new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string,string?> { ["Storage:Provider"]="Json" }).Build();var store=new AppStore(env,config);var catalog=new ProductCatalog(store);
try
{
    Assert(new Product().Audience=="men","New and legacy product objects default to menswear");
    var rows=new[]{new Product{Id=1,Name="Áo nam",Audience="men",CategoryId=1,Price=500000,Stock=10,Sizes="M,L",Active=true},new Product{Id=2,Name="Hidden men",Audience="men",CategoryId=1,Active=false},new Product{Id=3,Name="Non-men fixture",Audience="women",CategoryId=2,Price=100000,Stock=10,Sizes="M",Active=true},new Product{Id=4,Name="Unisex fixture",Audience="unisex",CategoryId=2,Active=true}};
    await File.WriteAllTextAsync(Path.Combine(root,"Data","products.json"),JsonSerializer.Serialize(rows));
    await File.WriteAllTextAsync(Path.Combine(root,"Data","categories.json"),JsonSerializer.Serialize(new[]{new Category{Id=1,Name="Áo nam"},new Category{Id=2,Name="Other fixture"}}));
    await File.WriteAllTextAsync(Path.Combine(root,"Data","reviews.json"),"[]");
    Assert(catalog.All.Count==2&&catalog.All.All(p=>p.Audience=="men"),"Shared catalog excludes non-men and unisex products");
    Assert(catalog.Find(3) is null&&catalog.Find(4) is null&&catalog.Find(2) is null,"Direct purchase lookup rejects non-men and hidden products");
    Assert(!catalog.ValidateCart([new(){ProductId=3,Size="M",Quantity=1,UnitPrice=1}],out _),"Forged cart entry cannot buy a non-men product");
    var cart=new List<CartItem>{new(){ProductId=1,Size="L",Quantity=1,UnitPrice=1}};
    Assert(catalog.ValidateCart(cart,out _)&&cart[0].UnitPrice==500000,"Menswear cart uses actual server price and size");
    var home=(ViewResult)new HomeController(catalog).Index();
    Assert(home.Model is List<Product> featured&&featured.Count==1&&featured[0].Id==1,"Home page only features active menswear");
    var controller=new ProductController(store,catalog);var list=(ViewResult)controller.Index(null,null,null);
    Assert(list.Model is List<Product> available&&available.Count==1&&available[0].Id==1,"Product listing only exposes active menswear");
    Assert(controller.ViewData["Categories"] is List<Category> categories&&categories.Count==1&&categories[0].Id==1,"Public category menu excludes categories without menswear");
    Assert(controller.Details(3) is NotFoundResult,"Non-men product detail is not publicly accessible");
    Assert(controller.Review(3,5,"fixture") is BadRequestResult,"Review endpoint rejects a non-men product ID");
    Console.WriteLine($"Completed {count} menswear catalog/policy checks. No external API request was made.");
}
finally{var allowed=Path.GetFullPath(Path.Combine(Directory.GetCurrentDirectory(),".test-output","menswear-checks"))+Path.DirectorySeparatorChar;if(root.StartsWith(allowed,StringComparison.OrdinalIgnoreCase))Directory.Delete(root,true);}
sealed class TestEnvironment(string root):IWebHostEnvironment
{
    public string WebRootPath{get;set;}=Path.Combine(root,"wwwroot");public IFileProvider WebRootFileProvider{get;set;}=new NullFileProvider();public string ContentRootPath{get;set;}=root;public IFileProvider ContentRootFileProvider{get;set;}=new NullFileProvider();public string EnvironmentName{get;set;}="Development";public string ApplicationName{get;set;}="MensCatalog.Checks";
}


