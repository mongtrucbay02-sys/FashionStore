using System.Net;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.DataProtection;
using OutfitStudio.Controllers;
using OutfitStudio.Services;

// Dedicated test host: real MVC/JS, synthetic provider response, isolated JSON and cookie names.
// No test switch or fake provider is added to the production application.
internal static class BrowserChecks
{
    public static async Task RunAsync()
    {
        var root=Directory.GetCurrentDirectory();var testRoot=Path.Combine(root,".test-output","tryon-browser-checks");Directory.CreateDirectory(Path.Combine(testRoot,"catalog"));
        foreach(var name in new[]{"products","categories","reviews"})File.Copy(Path.Combine(root,"Data",name+".json"),Path.Combine(testRoot,"catalog",name+".json"),true);
        var builder=WebApplication.CreateBuilder(new WebApplicationOptions{ApplicationName=typeof(TryOnController).Assembly.GetName().Name,ContentRootPath=root,WebRootPath=Path.Combine(root,"wwwroot"),Args=["--urls","http://localhost:5087"]});
        builder.Configuration.AddInMemoryCollection(new Dictionary<string,string?>{["Storage:Provider"]="Json",["Storage:RuntimeDirectory"]=Path.Combine(testRoot,"state"),["Storage:CatalogDirectory"]=Path.Combine(testRoot,"catalog"),["Logging:LogLevel:Default"]="Warning"});
        builder.Services.AddControllersWithViews().AddApplicationPart(typeof(TryOnController).Assembly);builder.Services.AddSingleton<AppStore>();builder.Services.AddSingleton<InventoryService>();builder.Services.AddSingleton<QrPaymentService>();builder.Services.AddSingleton<TimeProvider>(TimeProvider.System);builder.Services.AddSingleton<ProductCatalog>();
        var fixture=await File.ReadAllBytesAsync(Path.Combine(root,"tests","VirtualTryOn.Checks","Fixtures","edited.png"));
        builder.Services.AddSingleton<IHttpClientFactory>(new ClientFactory(new FakeHandler((request,_)=>{
            if(request.RequestUri!.AbsoluteUri!="https://api.cloudflare.com/client/v4/accounts/0123456789abcdef0123456789abcdef/ai/run/@cf/black-forest-labs/flux-2-dev")throw new Exception("Unexpected test request");
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK){Content=new StringContent(JsonSerializer.Serialize(new { success=true, result=new { image=Convert.ToBase64String(fixture) } }),Encoding.UTF8,"application/json")});
        })));
        builder.Services.Configure<CloudflareAIOptions>(o=>{o.ApiKey="simulated-browser-check-only";o.AccountId="0123456789abcdef0123456789abcdef";});builder.Services.AddSingleton<CloudflareImageService>();builder.Services.AddSingleton<VirtualTryOnService>();
        builder.Services.AddDistributedMemoryCache();builder.Services.AddSession(o=>o.Cookie.Name="TryOnChecks.Session");builder.Services.AddDataProtection().PersistKeysToFileSystem(new DirectoryInfo(Path.Combine(testRoot,"keys")));
        builder.Services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme).AddCookie(o=>{o.LoginPath="/Account/Login";o.Cookie.Name="TryOnChecks.Auth";});builder.Services.AddAuthorization();
        var app=builder.Build();app.UseStaticFiles();app.UseRouting();app.UseSession();app.UseAuthentication();app.UseAuthorization();app.MapControllerRoute("default","{controller=TryOn}/{action=Index}/{id?}");
        Console.WriteLine("SYNTHETIC BROWSER CHECK HOST :5087 — results are solid-color fixtures, never AI-generated photos.");await app.RunAsync();
    }
}




