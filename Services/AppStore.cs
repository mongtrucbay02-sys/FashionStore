namespace OutfitStudio.Services;

internal interface IStoreBackend
{
    TResult Atomic<TResult>(Func<TResult> action);
    List<T> All<T>(string name);
    List<T> Read<T>(string filename);
    void Modify<T>(string filename, Action<List<T>> change);
    TResult Update<T, TResult>(string name, Func<List<T>, TResult> change);
}

public sealed class AppStore
{
    private readonly IStoreBackend backend;
    public bool IsSqlServer { get; }
    public bool IsSupabase { get; }

    public AppStore(IWebHostEnvironment environment, IConfiguration configuration)
    {
        var provider = configuration["Storage:Provider"] ?? "SqlServer";
        if (!string.IsNullOrWhiteSpace(configuration["DATABASE_URL"])) provider = "Supabase";
        IsSqlServer = provider.Equals("SqlServer", StringComparison.OrdinalIgnoreCase);
        IsSupabase = provider.Equals("Supabase", StringComparison.OrdinalIgnoreCase);
        backend = IsSupabase
            ? new SupabaseStore(PostgresConnection.Normalize(configuration["DATABASE_URL"]
                ?? throw new InvalidOperationException("Thiếu DATABASE_URL cho Supabase.")))
            : IsSqlServer ? new SqlServerStore(SqlDatabase.ConnectionString(configuration))
            : provider.Equals("Json", StringComparison.OrdinalIgnoreCase) ? new JsonFileStore(environment, configuration)
            : throw new InvalidOperationException("Storage:Provider phải là SqlServer, Supabase hoặc Json.");
    }

    public TResult Atomic<TResult>(Func<TResult> action) => backend.Atomic(action);
    public List<T> All<T>(string name) => backend.All<T>(name);
    public List<T> Read<T>(string filename) => backend.Read<T>(filename);
    public void Modify<T>(string filename, Action<List<T>> change) => backend.Modify(filename, change);
    public TResult Update<T, TResult>(string name, Func<List<T>, TResult> change) => backend.Update(name, change);

    internal void RecordImport(string source)
    {
        if (backend is SqlServerStore sql) sql.RecordImport(source);
        else if (backend is SupabaseStore supabase) supabase.RecordImport(source);
        else throw new InvalidOperationException("Database import required.");
    }
}
