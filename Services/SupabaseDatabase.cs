using Npgsql;
using OutfitStudio.Models;

namespace OutfitStudio.Services;

internal static class SupabaseDatabase
{
    public static void Initialize(IConfiguration configuration)
    {
        using var connection = Open(configuration);
        using var command = new NpgsqlCommand("""
            CREATE SCHEMA IF NOT EXISTS app_private;
            CREATE TABLE IF NOT EXISTS app_private.store_collections
            (
                collection_name text PRIMARY KEY,
                data jsonb NOT NULL CHECK (jsonb_typeof(data) = 'array'),
                updated_at timestamptz NOT NULL DEFAULT now()
            );
            CREATE TABLE IF NOT EXISTS app_private.import_history
            (
                id bigint GENERATED ALWAYS AS IDENTITY PRIMARY KEY,
                imported_at timestamptz NOT NULL DEFAULT now(),
                source_description text NOT NULL
            );
            """, connection);
        command.ExecuteNonQuery();
    }

    public static void Validate(IConfiguration configuration)
    {
        using var connection = Open(configuration);
        using var command = new NpgsqlCommand("SELECT to_regclass('app_private.store_collections') IS NOT NULL", connection);
        if (!Convert.ToBoolean(command.ExecuteScalar()))
            throw new InvalidOperationException("Chưa khởi tạo Supabase. Chạy `dotnet run -- --init-supabase` để nhập dữ liệu hiện tại.");
    }

    public static string MigrateFromSqlServer(IConfiguration configuration)
    {
        SqlDatabase.Validate(configuration);
        var source = new SqlServerStore(SqlDatabase.ConnectionString(configuration));
        var target = new SupabaseStore(PostgresConnection.Normalize(configuration["DATABASE_URL"]
            ?? throw new InvalidOperationException("Thiếu DATABASE_URL.")));

        var snapshot = source.Atomic(() => (
            Categories: source.All<Category>("categories"),
            Products: source.All<Product>("products"),
            Users: source.Read<User>("users.json"),
            Orders: source.Read<Order>("orders.json"),
            Reviews: source.All<Review>("reviews"),
            Movements: source.Read<StockMovement>("stock-movements.json"),
            Outfits: source.All<OutfitProduct>("outfit-products"),
            QrSettings: source.Read<QrPaymentSettings>("qr-payment-settings.json"),
            InventorySnapshot: source.Read<Product>("inventory-before-sizes.json")));

        target.Atomic(() =>
        {
            if (target.All<Category>("categories").Count > 0 || target.All<Product>("products").Count > 0 ||
                target.Read<User>("users.json").Count > 0 || target.Read<Order>("orders.json").Count > 0 ||
                target.All<Review>("reviews").Count > 0 || target.Read<StockMovement>("stock-movements.json").Count > 0 ||
                target.All<OutfitProduct>("outfit-products").Count > 0 || target.Read<QrPaymentSettings>("qr-payment-settings.json").Count > 0 ||
                target.Read<Product>("inventory-before-sizes.json").Count > 0)
                throw new InvalidOperationException("Supabase đã có dữ liệu. Không nhập đè để tránh mất dữ liệu.");
            target.Update<Category, bool>("categories", rows => { rows.AddRange(snapshot.Categories); return true; });
            target.Update<Product, bool>("products", rows => { rows.AddRange(snapshot.Products); return true; });
            target.Modify<User>("users.json", rows => rows.AddRange(snapshot.Users));
            target.Modify<Order>("orders.json", rows => rows.AddRange(snapshot.Orders));
            target.Update<Review, bool>("reviews", rows => { rows.AddRange(snapshot.Reviews); return true; });
            target.Modify<StockMovement>("stock-movements.json", rows => rows.AddRange(snapshot.Movements));
            target.Update<OutfitProduct, bool>("outfit-products", rows => { rows.AddRange(snapshot.Outfits); return true; });
            target.Modify<QrPaymentSettings>("qr-payment-settings.json", rows => rows.AddRange(snapshot.QrSettings));
            target.Modify<Product>("inventory-before-sizes.json", rows => rows.AddRange(snapshot.InventorySnapshot));
            target.RecordImport("Imported all collections from the configured SQL Server database");
            return true;
        });

        return $"Imported categories={snapshot.Categories.Count}, products={snapshot.Products.Count}, users={snapshot.Users.Count}, orders={snapshot.Orders.Count}, reviews={snapshot.Reviews.Count}, stock-movements={snapshot.Movements.Count}, outfit-products={snapshot.Outfits.Count}, qr-payment-settings={snapshot.QrSettings.Count}, inventory-snapshot={snapshot.InventorySnapshot.Count}";
    }

    public static string VerifySqlServerImport(IConfiguration configuration)
    {
        var source = new SqlServerStore(SqlDatabase.ConnectionString(configuration));
        var target = new SupabaseStore(PostgresConnection.Normalize(configuration["DATABASE_URL"]
            ?? throw new InvalidOperationException("Thiếu DATABASE_URL.")));
        var counts = new Dictionary<string, (int Source, int Target)>
        {
            ["categories"] = (source.All<Category>("categories").Count, target.All<Category>("categories").Count),
            ["products"] = (source.All<Product>("products").Count, target.All<Product>("products").Count),
            ["users"] = (source.Read<User>("users.json").Count, target.Read<User>("users.json").Count),
            ["orders"] = (source.Read<Order>("orders.json").Count, target.Read<Order>("orders.json").Count),
            ["reviews"] = (source.All<Review>("reviews").Count, target.All<Review>("reviews").Count),
            ["stock-movements"] = (source.Read<StockMovement>("stock-movements.json").Count, target.Read<StockMovement>("stock-movements.json").Count),
            ["outfit-products"] = (source.All<OutfitProduct>("outfit-products").Count, target.All<OutfitProduct>("outfit-products").Count),
            ["qr-payment-settings"] = (source.Read<QrPaymentSettings>("qr-payment-settings.json").Count, target.Read<QrPaymentSettings>("qr-payment-settings.json").Count),
            ["inventory-before-sizes"] = (source.Read<Product>("inventory-before-sizes.json").Count, target.Read<Product>("inventory-before-sizes.json").Count)
        };
        var mismatches = counts.Where(pair => pair.Value.Source != pair.Value.Target).ToList();
        if (mismatches.Count > 0)
            throw new InvalidOperationException("Số bản ghi chưa khớp: " + string.Join(", ", mismatches.Select(pair => $"{pair.Key} SQL={pair.Value.Source}, Supabase={pair.Value.Target}")));
        return string.Join(", ", counts.Select(pair => $"{pair.Key}={pair.Value.Target}"));
    }

    private static NpgsqlConnection Open(IConfiguration configuration)
    {
        var connectionString = PostgresConnection.Normalize(configuration["DATABASE_URL"]
            ?? throw new InvalidOperationException("Thiếu DATABASE_URL cho Supabase."));
        var connection = new NpgsqlConnection(connectionString);
        connection.Open();
        return connection;
    }
}
