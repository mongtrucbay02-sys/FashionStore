using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Authentication.Cookies;
using OutfitStudio.Services;
using System.Globalization;
using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using OutfitStudio.Models;


namespace OutfitStudio.Services;

public static class StoreApplication
{
    public static WebApplication Build(string[] args, Action<WebApplicationBuilder>? configure = null)
    {
        LoadLocalDotEnv(Directory.GetCurrentDirectory());
        CultureInfo.DefaultThreadCurrentCulture = new CultureInfo("vi-VN");
        CultureInfo.DefaultThreadCurrentUICulture = new CultureInfo("vi-VN");

        var builder = WebApplication.CreateBuilder(new WebApplicationOptions { Args = args, ApplicationName = typeof(StoreApplication).Assembly.GetName().Name });
        builder.Services.AddControllersWithViews().AddApplicationPart(typeof(OutfitStudio.Controllers.AccountController).Assembly);
        builder.Services.AddSingleton<AppStore>();
        builder.Services.AddMemoryCache();
        builder.Services.AddScoped<OtpEmailService>();
        builder.Services.Configure<EmailSettings>(builder.Configuration.GetSection("EmailSettings"));
        builder.Services.PostConfigure<EmailSettings>(settings =>
        {
            settings.Host = builder.Configuration["SMTP_HOST"] ?? settings.Host;
            if (int.TryParse(builder.Configuration["SMTP_PORT"], out var port)) settings.Port = port;
            settings.FromEmail = builder.Configuration["SMTP_USER"] ?? settings.FromEmail;
            settings.Password = builder.Configuration["SMTP_PASS"] ?? settings.Password;
        });
        builder.Services.AddSingleton<ProductCatalog>();
        builder.Services.AddSingleton<InventoryService>();
        builder.Services.AddSingleton<QrPaymentService>();
        builder.Services.AddSingleton(TimeProvider.System);
        builder.Services.Configure<CloudflareAIOptions>(builder.Configuration.GetSection("CloudflareAI"));
        builder.Services.AddHttpClient(nameof(CloudflareImageService), client =>
        {
            client.Timeout = TimeSpan.FromMinutes(4);
            client.MaxResponseContentBufferSize = 48 * 1024 * 1024;
        });
        builder.Services.AddSingleton<CloudflareImageService>();
        builder.Services.AddSingleton<VirtualTryOnService>();
        builder.Services.AddDistributedMemoryCache();
        builder.Services.AddSession(options =>
        {
            options.IdleTimeout = TimeSpan.FromHours(2);
            options.Cookie.HttpOnly = true;
            options.Cookie.IsEssential = true;
        });
        var protection = builder.Services.AddDataProtection()
            .PersistKeysToFileSystem(new DirectoryInfo(Path.Combine(builder.Environment.ContentRootPath, ".keys")));
        if (OperatingSystem.IsWindows())
            protection.ProtectKeysWithDpapi();
        builder.Services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme).AddCookie(options =>
        {
            options.LoginPath = "/Account/Login";
            options.AccessDeniedPath = "/Account/AccessDenied";
            options.ExpireTimeSpan = TimeSpan.FromDays(92);
            options.SlidingExpiration = true;
            options.Events.OnValidatePrincipal = async context =>
            {
                var store = context.HttpContext.RequestServices.GetRequiredService<AppStore>();
                var id = context.Principal?.FindFirstValue(ClaimTypes.NameIdentifier);
                var user = store.Read<User>("users.json").FirstOrDefault(u => u.Id.ToString() == id);
                if (user is null || user.IsLocked || user.IsDeleted ||
                    context.Principal?.FindFirstValue("password_version") != user.PasswordVersion)
                {
                    context.RejectPrincipal();
                    await context.HttpContext.SignOutAsync();
                    return;
                }
                if (context.Principal?.FindFirstValue(ClaimTypes.Role) != user.Role)
                {
                    context.ReplacePrincipal(AccountIdentity.Create(user));
                    context.ShouldRenew = true;
                }
            };
        });

        configure?.Invoke(builder);
        var app = builder.Build();
        app.UseStaticFiles();
        app.UseRouting();
        app.UseSession();
        app.UseAuthentication();
        app.UseAuthorization();
        app.MapControllerRoute(name: "default", pattern: "{controller=Home}/{action=Index}/{id?}");
        return app;



    }

    private static void LoadLocalDotEnv(string contentRoot)
    {
        var path = Path.Combine(contentRoot, ".env");
        if (!File.Exists(path)) return;
        foreach (var line in File.ReadLines(path))
        {
            var entry = line.Trim();
            if (entry.Length == 0 || entry.StartsWith('#')) continue;
            var separator = entry.IndexOf('=');
            if (separator <= 0) continue;
            var key = entry[..separator].Trim();
            if (string.IsNullOrEmpty(key) || Environment.GetEnvironmentVariable(key) is not null) continue;
            var value = entry[(separator + 1)..].Trim();
            if (value.Length >= 2 && ((value[0] == '"' && value[^1] == '"') || (value[0] == '\'' && value[^1] == '\'')))
                value = value[1..^1];
            Environment.SetEnvironmentVariable(key, value);
        }
    }

}




