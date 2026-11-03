using OutfitStudio.Services;

var app = StoreApplication.Build(args);

if (args.Contains("--init-sql"))
{
    SqlDatabase.Initialize(app.Configuration, app.Environment.ContentRootPath);
    SqlDatabase.ImportJson(app.Services.GetRequiredService<AppStore>(), app.Configuration, app.Environment.ContentRootPath);
    Console.WriteLine("Đã khởi tạo SQL Server và nhập dữ liệu JSON. File gốc được giữ nguyên.");
    return;
}

if (args.Contains("--init-supabase"))
{
    SupabaseDatabase.Initialize(app.Configuration);
    var result = SupabaseDatabase.MigrateFromSqlServer(app.Configuration);
    Console.WriteLine($"Đã nhập SQL Server sang Supabase: {result}");
    return;
}

var store = app.Services.GetRequiredService<AppStore>();
if (store.IsSqlServer) SqlDatabase.Validate(app.Configuration);
if (store.IsSupabase)
{
    // Make a fresh Supabase project ready on first launch. Initialize is idempotent
    // and creates only the private app schema/tables; it does not import or erase data.
    SupabaseDatabase.Initialize(app.Configuration);
    SupabaseDatabase.Validate(app.Configuration);
}

if (args.Contains("--verify-supabase-import"))
{
    Console.WriteLine($"Đã xác minh số bản ghi SQL Server/Supabase: {SupabaseDatabase.VerifySqlServerImport(app.Configuration)}");
    return;
}
if (args.Contains("--verify-sql-import"))
{
    foreach (var item in SqlImportVerification.Verify(store, app.Configuration, app.Environment.ContentRootPath))
        Console.WriteLine($"Verified {item.Key}: {item.Value} rows");
    return;
}
if (args.Contains("--seed-admin-demo"))
{
    AdminSeed.Run(store, app.Configuration["AdminSeed:Email"]);
    return;
}

app.Services.GetRequiredService<InventoryService>().Migrate();
app.Run();
