using Microsoft.Data.SqlClient;
using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.IO.Compression;
using System.Xml.Linq;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.DependencyInjection.Extensions;
using OutfitStudio.Models;
using OutfitStudio.Services;

var directory = new DirectoryInfo(AppContext.BaseDirectory);
while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "OutfitStudio.csproj"))) directory = directory.Parent;
var source = directory?.FullName ?? throw new InvalidOperationException("Source root not found.");
Directory.SetCurrentDirectory(source);
var root = Path.Combine(source, ".test-output", "store-checks-" + Guid.NewGuid().ToString("N"));
Directory.CreateDirectory(Path.Combine(root, "catalog"));
foreach (var file in new[] { "products.json", "categories.json", "reviews.json", "outfits.json" })
    File.Copy(Path.Combine(source, "Data", file), Path.Combine(root, "catalog", file));
var hashes = new[] { "Data/products.json", "App_Data/users.json", "App_Data/orders.json" }
    .Where(File.Exists).ToDictionary(f => f, f => SHA256.HashData(File.ReadAllBytes(f)));
var sqlMode = args.Contains("--sql");
var testDatabase = "FashionStore_Test_" + Guid.NewGuid().ToString("N");
var testConnection = $"Server=.\\SQLEXPRESS;Database={testDatabase};Integrated Security=True;Encrypt=True;TrustServerCertificate=True;Connect Timeout=10";
var clock = new TestClock();
var app = StoreApplication.Build(["--contentRoot", source, "--urls", "http://127.0.0.1:0"], builder =>
{
    builder.Configuration.AddInMemoryCollection(new Dictionary<string, string?>
    {
        ["DATABASE_URL"] = "",
        ["Storage:Provider"] = sqlMode ? "SqlServer" : "Json",
        ["ConnectionStrings:FashionStore"] = testConnection,
        ["Storage:RuntimeDirectory"] = Path.Combine(root, "state"),
        ["Storage:CatalogDirectory"] = Path.Combine(root, "catalog"),
        ["Logging:LogLevel:Default"] = "Warning",
        ["Admin:RootEmail"] = "root@example.test",
        ["SePay:WebhookApiKey"] = "synthetic-webhook-key",

    });
    builder.Services.RemoveAll<TimeProvider>();
    builder.Services.AddSingleton<TimeProvider>(clock);
    builder.Services.AddDataProtection().PersistKeysToFileSystem(new DirectoryInfo(Path.Combine(root, "keys")));
});
var store = app.Services.GetRequiredService<AppStore>();
var inventory = app.Services.GetRequiredService<InventoryService>();
int count = 0;
void Assert(bool ok, string name) { if (!ok) throw new Exception(name); count++; Console.WriteLine("PASS: " + name); }
try
{
    if (sqlMode)
    {
        SqlDatabase.Initialize(app.Configuration, source);
        var categoryPath = Path.Combine(root, "catalog", "categories.json");
        var originalCategories = File.ReadAllText(categoryPath);
        File.WriteAllText(categoryPath, "[]");
        try { SqlDatabase.ImportJson(store, app.Configuration, source, false); throw new Exception("Invalid import must fail"); }
        catch (SqlException) { Assert(store.All<Category>("categories").Count == 0 && store.All<Product>("products").Count == 0, "Failed SQL import rolls back all imported collections"); }
        File.WriteAllText(categoryPath, originalCategories);
        SqlDatabase.ImportJson(store, app.Configuration, source, includeRuntime: true);
        try { store.Update<Category,bool>("categories", rows => { rows.Clear(); return true; }); throw new Exception("FK must reject orphan products"); }
        catch (SqlException) { Assert(store.All<Category>("categories").Count > 0, "SQL foreign keys reject deleting categories that contain products"); }
    }
    inventory.Migrate();
    var original = JsonSerializer.Deserialize<List<Product>>(File.ReadAllText(Path.Combine(source, "Data/products.json")), new JsonSerializerOptions { PropertyNameCaseInsensitive = true })!;
    Assert(store.All<Product>("products").All(p => p.Stock == p.SizeStock.Values.Sum()), "Migration synchronizes total stock and per-size stock");
    Assert(store.All<Product>("products").Sum(p => p.Stock) == original.Sum(p => p.Stock), "Migration preserves original total quantities");
    var initialSizes = JsonSerializer.Serialize(store.All<Product>("products").Select(p => p.SizeStock));
    inventory.Migrate();
    Assert(JsonSerializer.Serialize(store.All<Product>("products").Select(p => p.SizeStock)) == initialSizes, "Repeated migration does not redistribute quantities");
    var first = store.All<Product>("products")[0];
    var size = first.AvailableSizes[0];
    var other = first.AvailableSizes[1];
    var before = first.StockOf(size);
    Assert(inventory.Import(first.Id, size, 3, "Test receipt", "Synthetic admin") == "" && store.All<Product>("products")[0].StockOf(size) == before + 3, "Import changes only selected size");
    Assert(inventory.Import(first.Id, "BAD", 1, "", "Test") != "" && inventory.SetStock(first.Id, size, -1, "", "Test") != "", "Invalid size and negative stock rejected");
    inventory.SetStock(first.Id, size, 1, "", "Test");
    inventory.SetStock(first.Id, other, 0, "", "Test");
    Assert(InventoryService.StatusOf(store.All<Product>("products")[0]) == InventoryService.LowLabel, "Low-stock status includes an empty size");
    var cart = new List<CartItem> { new() { ProductId = first.Id, Size = size, Quantity = 1 } };
    Assert(inventory.Deduct([cart[0], new() { ProductId = first.Id, Size = other, Quantity = 1 }], 101) != "" && store.All<Product>("products")[0].StockOf(size) == 1, "Failed multi-line deduction leaves all sizes unchanged");
    var outcomes = await Task.WhenAll(Enumerable.Range(0, 8).Select(i => Task.Run(() => inventory.Deduct(cart, 200 + i))));
    Assert(outcomes.Count(e => e == "") == 1 && store.All<Product>("products")[0].StockOf(size) == 0, "Concurrent purchases cannot oversell last unit");
    var oldHistory = inventory.Movements().Count;
    try
    {
        store.Atomic<bool>(() => { inventory.Import(first.Id, size, 7, "Rollback", "Test"); throw new InvalidOperationException("test rollback"); });
    }
    catch (InvalidOperationException) { }
    Assert(store.All<Product>("products")[0].StockOf(size) == 0 && inventory.Movements().Count == oldHistory, "Transaction rollback discards stock and history together");
    inventory.SetStock(first.Id, size, InventoryService.MaxStock, "", "Test");
    inventory.Restore(new Order { Id = 300, Items = [new() { ProductId = first.Id, Size = size, Quantity = 2 }] }, "Test");
    Assert(store.All<Product>("products")[0].StockOf(size) == InventoryService.MaxStock + 2, "Returns do not silently lose units at manual stock-entry limit");
    inventory.SetStock(first.Id, size, 0, "", "Test");
    if (!sqlMode)
    {
    // Simulate an interrupted commit: next read replays the complete private redo journal.
    var replay = Path.Combine(root, "state", "recovered.json");
    File.WriteAllText(Path.Combine(root, "state", ".store-transaction.json"), JsonSerializer.Serialize(new Dictionary<string, string> { [replay] = "[3]" }));
    Assert(store.Read<int>("recovered.json").Single() == 3 && !File.Exists(Path.Combine(root, "state", ".store-transaction.json")), "Interrupted JSON commit recovers on next operation");

    }
    else
    {
        inventory.SetStock(first.Id, size, 1, "", "Test");
        var independent = new InventoryService(new AppStore(app.Environment, app.Configuration));
        var parallel = await Task.WhenAll(Task.Run(() => inventory.Deduct(cart, 701)), Task.Run(() => independent.Deduct(cart, 702)));
        Assert(parallel.Count(e => e == "") == 1 && store.All<Product>("products")[0].StockOf(size) == 0, "SQL transaction lock prevents overselling across independent store instances");
        try { SqlDatabase.ImportJson(store, app.Configuration, source, false); throw new Exception("Repeat import must fail"); }
        catch (InvalidOperationException) { Assert(true, "SQL import refuses to overwrite a populated database"); }
    }
    await app.StartAsync();
    var address = app.Services.GetRequiredService<IServer>().Features.Get<IServerAddressesFeature>()!.Addresses.Single();
    HttpClient Client() => new(new HttpClientHandler { CookieContainer = new(), AllowAutoRedirect = false }) { BaseAddress = new Uri(address) };
    using var client = Client();
    async Task<HttpResponseMessage> Post(HttpClient browser, string path, string form, Dictionary<string, string> fields)
    {
        var html = await browser.GetStringAsync(form);
        var token = WebUtility.HtmlDecode(Regex.Match(html, "name=\"__RequestVerificationToken\"[^>]*value=\"([^\"]+)\"").Groups[1].Value);
        fields["__RequestVerificationToken"] = token;
        return await browser.PostAsync(path, new FormUrlEncodedContent(fields));
    }
    if (args.Contains("--admin-commerce"))
    {
        await AdminCommerceChecks.RunAsync(app, Client, Post, Assert);
        foreach (var (file, hash) in hashes)
            Assert(SHA256.HashData(File.ReadAllBytes(file)).SequenceEqual(hash), "Real data unchanged: " + file);
        return;
    }

    async Task<HttpResponseMessage> Register(string email) => await Post(client, "/Account/Register", "/Account/Register", new()
    { ["FullName"] = "OTP synthetic customer", ["Email"] = email, ["Password"] = "Synthetic!2026", ["ConfirmPassword"] = "Synthetic!2026", ["Role"] = "Admin" });
    var email = "customer@example.test";
    var result = await Register(email);
    Assert(result.StatusCode == HttpStatusCode.Redirect && (await client.GetAsync("/Order/History")).StatusCode == HttpStatusCode.OK, "Registration signs in without OTP");
    Assert(store.Read<User>("users.json").Single().Role == "User", "Registration cannot inject Admin role");
    Assert(store.Read<User>("users.json").Single().EmailVerifiedAt is null, "Email is not falsely marked verified");
    await Register(email);
    Assert(store.Read<User>("users.json").Count == 1, "Duplicate registration rejected");
    using var stranger = Client();
    Assert((await stranger.GetAsync("/Account/VerifyEmail")).StatusCode == HttpStatusCode.NotFound, "OTP route removed");
    var noCsrf = await stranger.PostAsync("/Account/Login", new FormUrlEncodedContent(new Dictionary<string, string> { ["Email"] = email, ["Password"] = "Synthetic!2026" }));
    Assert(noCsrf.StatusCode == HttpStatusCode.BadRequest, "Login requires CSRF token");
    await Post(client, "/Account/Logout", "/", new());
    var login = await Post(client, "/Account/Login", "/Account/Login", new() { ["Email"] = email, ["Password"] = "wrong" });
    Assert(login.StatusCode == HttpStatusCode.OK && (await client.GetAsync("/Order/History")).StatusCode == HttpStatusCode.Redirect, "Wrong password rejected");
    login = await Post(client, "/Account/Login?returnUrl=https://evil.example", "/Account/Login", new() { ["Email"] = email, ["Password"] = "Synthetic!2026" });
    Assert(login.Headers.Location?.OriginalString is "/" or "/Home/Index", "External return URL rejected");
    Assert((await client.GetAsync("/Order/History")).StatusCode == HttpStatusCode.OK, "Password signs in immediately");
    // Normal customer cannot enter or mutate inventory.
    clock.Advance(TimeSpan.FromSeconds(61));
    await Post(client, "/Account/Login", "/Account/Login", new() { ["Email"] = email, ["Password"] = "Synthetic!2026" });
    Assert((await client.GetAsync("/Inventory")).Headers.Location?.OriginalString?.Contains("AccessDenied") == true, "Inventory is restricted to admin role");
    var orderProduct = store.All<Product>("products").First(p => p.Id != first.Id && p.StockOf(p.AvailableSizes[0]) > 1);
    var orderSize = orderProduct.AvailableSizes[0];
    var quantityBefore = orderProduct.StockOf(orderSize);
    await Post(client, "/Cart/Add", "/Product/Details/" + orderProduct.Id, new() { ["productId"] = orderProduct.Id.ToString(), ["size"] = orderSize, ["quantity"] = "1" });
    await Post(client, "/Order/Checkout", "/Order/Checkout", new() { ["FullName"] = "Synthetic", ["Phone"] = "0900000000", ["Address"] = "Test only", ["PaymentMethod"] = "COD" });
    var order = store.Read<Order>("orders.json").Single();
    Assert(order.StockDeducted && store.All<Product>("products").First(p => p.Id == orderProduct.Id).StockOf(orderSize) == quantityBefore - 1, "Real checkout deducts selected size and records order flag");
    // Only the isolated synthetic account is promoted; real user files are never used by this host.
    store.Modify<User>("users.json", users => users.Single().Role = "Admin");
    await Post(client, "/Account/Logout", "/", new());
    clock.Advance(TimeSpan.FromSeconds(61));
    await Post(client, "/Account/Login", "/Account/Login", new() { ["Email"] = email, ["Password"] = "Synthetic!2026" });
    var inventoryPage = await client.GetStringAsync("/Inventory");
    Assert(inventoryPage.Contains("stock-form") && inventoryPage.Contains("/Inventory/Export") && inventoryPage.Contains("typography.css"), "Admin inventory page renders forms, Excel link and shared font");
    var unitsBeforeBad = store.All<Product>("products").Sum(p => p.Stock);
    await Post(client, "/Inventory/Adjust", "/Inventory", new() { ["id"] = orderProduct.Id.ToString(), ["size"] = orderSize, ["amount"] = "not-a-number" });
    Assert(store.All<Product>("products").Sum(p => p.Stock) == unitsBeforeBad, "Invalid numeric form cannot reset stock to zero");
    await Post(client, "/Admin/UpdateOrderStatus", "/Admin/OrderDetails/" + order.Id, new() { ["id"] = order.Id.ToString(), ["status"] = OrderStatus.Cancelled });
    await Post(client, "/Admin/UpdateOrderStatus", "/Admin/OrderDetails/" + order.Id, new() { ["id"] = order.Id.ToString(), ["status"] = OrderStatus.Cancelled });
    Assert(store.All<Product>("products").First(p => p.Id == orderProduct.Id).StockOf(orderSize) == quantityBefore, "Cancellation restores stock exactly once");
    store.Modify<Order>("orders.json", orders => orders.Add(new Order { Id = 900, Items = order.Items, Status = OrderStatus.Pending, StockDeducted = false }));
    await Post(client, "/Admin/UpdateOrderStatus", "/Admin/OrderDetails/900", new() { ["id"] = "900", ["status"] = OrderStatus.Cancelled });
    Assert(store.All<Product>("products").First(p => p.Id == orderProduct.Id).StockOf(orderSize) == quantityBefore, "Legacy orders do not inflate stock on cancellation");
    var export = await client.GetByteArrayAsync("/Inventory/Export");
    using (var zip = new ZipArchive(new MemoryStream(export)))
    {
        var sheet = zip.GetEntry("xl/worksheets/sheet1.xml");
        Assert(sheet is not null && XDocument.Load(sheet.Open()).Root is not null, "Inventory Excel export is valid OOXML");
    }
    Assert((await client.GetAsync("/Inventory/History")).StatusCode == HttpStatusCode.OK && (await client.GetAsync("/Inventory/ExportHistory")).StatusCode == HttpStatusCode.OK, "Inventory history and history export respond successfully");
    await PaymentChecks.RunAsync(store, app.Services.GetRequiredService<QrPaymentService>(), client, stranger, Post, Assert, orderProduct.Id, orderSize);
    await Post(client, "/Account/Logout", "/", new());
    clock.Advance(TimeSpan.FromHours(2));
    await Post(client, "/Account/Login", "/Account/Login", new() { ["Email"] = email, ["Password"] = "Synthetic!2026" });
    store.Modify<User>("users.json", users => users.Single().IsLocked = true);
    Assert((await client.GetAsync("/Order/History")).StatusCode == HttpStatusCode.Redirect, "Account locked after password check loses access");
    foreach (var (file, hash) in hashes) Assert(SHA256.HashData(File.ReadAllBytes(file)).SequenceEqual(hash), "Real data unchanged: " + file);
    Console.WriteLine($"Completed {count} inventory, authentication and QR payment checks ({(sqlMode ? "SQL Server" : "JSON regression")}). Email transport simulated; no real email sent.");
}
finally
{
    await app.StopAsync();
    await app.DisposeAsync();
    if (sqlMode)
    {
        if (!Regex.IsMatch(testDatabase, @"^FashionStore_Test_[0-9a-f]{32}$")) throw new InvalidOperationException("Unsafe test database name.");
        var master = new SqlConnectionStringBuilder(testConnection) { InitialCatalog = "master" };
        await using var cleanup = new SqlConnection(master.ConnectionString);
        await cleanup.OpenAsync();
        await using var command = new SqlCommand($"IF DB_ID(N'{testDatabase}') IS NOT NULL BEGIN ALTER DATABASE [{testDatabase}] SET SINGLE_USER WITH ROLLBACK IMMEDIATE; DROP DATABASE [{testDatabase}]; END", cleanup);
        await command.ExecuteNonQueryAsync();
    }
    if (Path.GetFullPath(root).StartsWith(Path.Combine(source, ".test-output") + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase)) Directory.Delete(root, true);
}

sealed class TestClock : TimeProvider
{
    private DateTimeOffset current = DateTimeOffset.UtcNow;
    public override DateTimeOffset GetUtcNow() => current;
    public void Advance(TimeSpan span) => current += span;
}







