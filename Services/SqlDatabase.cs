using System.Data;
using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.Data.SqlClient;
using OutfitStudio.Models;

namespace OutfitStudio.Services;

public static class SqlDatabase
{
    public static string ConnectionString(IConfiguration config) => config.GetConnectionString("FashionStore")
        ?? throw new InvalidOperationException("Chưa cấu hình ConnectionStrings:FashionStore cho SQL Server.");

    public static void Initialize(IConfiguration config, string contentRoot)
    {
        var target = new SqlConnectionStringBuilder(ConnectionString(config));
        if (string.IsNullOrWhiteSpace(target.InitialCatalog) || new[] { "master", "model", "msdb", "tempdb" }.Contains(target.InitialCatalog, StringComparer.OrdinalIgnoreCase))
            throw new InvalidOperationException("Chọn database riêng cho FashionStore.");
        var database = target.InitialCatalog;
        var master = new SqlConnectionStringBuilder(target.ConnectionString) { InitialCatalog = "master" };
        using (var connection = new SqlConnection(master.ConnectionString))
        {
            connection.Open();
            using var create = new SqlCommand("IF DB_ID(@database) IS NULL BEGIN DECLARE @sql nvarchar(max)=N'CREATE DATABASE '+QUOTENAME(@database); EXEC sys.sp_executesql @sql; END", connection);
            create.Parameters.Add("@database", SqlDbType.NVarChar, 128).Value = database;
            create.ExecuteNonQuery();
        }
        using var sql = new SqlConnection(target.ConnectionString);
        sql.Open();
        using (var guard = new SqlCommand("IF EXISTS(SELECT 1 FROM sys.tables WHERE is_ms_shipped=0) AND OBJECT_ID(N'dbo.SchemaVersions',N'U') IS NULL THROW 51003,N'Database đã có bảng của ứng dụng khác. Hãy chọn database trống.',1;", sql))
            guard.ExecuteNonQuery();
        var schema = File.ReadAllText(Path.Combine(contentRoot, "database", "Schema.sql"));
        foreach (var batch in Regex.Split(schema, @"^\s*GO\s*\r?$", RegexOptions.Multiline | RegexOptions.IgnoreCase).Where(b => !string.IsNullOrWhiteSpace(b)))
        {
            using var command = new SqlCommand(batch, sql) { CommandTimeout = 60 };
            command.ExecuteNonQuery();
        }
    }

    public static void Validate(IConfiguration config)
    {
        using var connection = new SqlConnection(ConnectionString(config));
        connection.Open();
        using var command = new SqlCommand("SELECT COUNT(*) FROM dbo.SchemaVersions WHERE Version=1", connection);
        if (Convert.ToInt32(command.ExecuteScalar()) != 1)
            throw new InvalidOperationException("Cần chạy --init-sql trước khi mở website.");
    }

    // Import only into an empty database, under the same SQL application lock used by requests.
    // Password hashes, IDs, timestamps and order/payment snapshots are preserved; source files are never modified.
    public static void ImportJson(AppStore store, IConfiguration config, string contentRoot, bool includeRuntime = true)
    {
        if (!store.IsSqlServer)
            throw new InvalidOperationException("Import yêu cầu SQL Server.");
        var catalogDir = config["Storage:CatalogDirectory"] ?? Path.Combine(contentRoot, "Data");
        var runtimeDir = config["Storage:RuntimeDirectory"] ?? Path.Combine(contentRoot, "App_Data");
        if (File.Exists(Path.Combine(runtimeDir, ".store-transaction.json")))
            throw new InvalidOperationException("Nguồn JSON còn giao dịch chờ khôi phục. Hoàn tất giao dịch JSON trước khi nhập SQL.");
        var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
        List<T> Read<T>(string folder, string file) => File.Exists(Path.Combine(folder, file))
            ? JsonSerializer.Deserialize<List<T>>(File.ReadAllText(Path.Combine(folder, file)), options) ?? [] : [];
        store.Atomic(() =>
        {
            if (store.All<Product>("products").Count > 0 || store.Read<User>("users.json").Count > 0 || store.Read<Order>("orders.json").Count > 0 || store.All<Category>("categories").Count > 0 || store.All<OutfitProduct>("outfit-products").Count > 0 || store.All<Review>("reviews").Count > 0 || store.Read<StockMovement>("stock-movements.json").Count > 0 || store.Read<QrPaymentSettings>("qr-payment-settings.json").Count > 0)
                throw new InvalidOperationException("Database đã có dữ liệu. Không nhập đè; hãy dùng database mới nếu cần nhập lại.");
            var categories = Read<Category>(catalogDir, "categories.json");
            var products = Read<Product>(catalogDir, "products.json");
            foreach (var product in products)
                InventoryService.SyncSizes(product);
            store.Update<Category, bool>("categories", rows => { rows.AddRange(categories); return true; });
            store.Update<Product, bool>("products", rows => { rows.AddRange(products); return true; });
            var outfitPath = Path.Combine(catalogDir, "outfits.json");
            var outfits = File.Exists(outfitPath) ? JsonSerializer.Deserialize<OutfitCatalog>(File.ReadAllText(outfitPath), options)?.Products ?? [] : [];
            store.Update<OutfitProduct, bool>("outfit-products", rows => { rows.AddRange(outfits); return true; });
            store.Update<Review, bool>("reviews", rows => { rows.AddRange(includeRuntime ? Read<Review>(catalogDir, "reviews.json") : []); return true; });
            if (includeRuntime)
            {
                store.Modify<User>("users.json", rows => rows.AddRange(Read<User>(runtimeDir, "users.json")));
                store.Modify<Order>("orders.json", rows => rows.AddRange(Read<Order>(runtimeDir, "orders.json")));
                store.Modify<StockMovement>("stock-movements.json", rows => rows.AddRange(Read<StockMovement>(runtimeDir, "stock-movements.json")));
                store.Modify<QrPaymentSettings>("qr-payment-settings.json", rows => rows.AddRange(Read<QrPaymentSettings>(runtimeDir, "qr-payment-settings.json")));
                store.Modify<Product>("inventory-before-sizes.json", rows => rows.AddRange(Read<Product>(runtimeDir, "inventory-before-sizes.json")));
            }
            if (includeRuntime) SqlImportVerification.Verify(store, config, contentRoot);
            store.RecordImport(includeRuntime ? "Imported JSON catalog and runtime data" : "Imported isolated catalog fixture");
            return true;
        });
    }
}

