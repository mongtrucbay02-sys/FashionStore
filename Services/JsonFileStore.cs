using System.Text.Json;

namespace OutfitStudio.Services;

// One process owns these JSON files. A redo journal commits catalog, orders and stock history together.
internal sealed class JsonFileStore(IWebHostEnvironment environment, IConfiguration configuration) : IStoreBackend
{
    private readonly object gate = new();
    private readonly string dataDir = Path.GetFullPath(configuration["Storage:CatalogDirectory"] ?? Path.Combine(environment.ContentRootPath, "Data"));
    private readonly string runtimeDir = Path.GetFullPath(configuration["Storage:RuntimeDirectory"] ?? Path.Combine(environment.ContentRootPath, "App_Data"));
    private static readonly JsonSerializerOptions Options = new() { WriteIndented = true, PropertyNameCaseInsensitive = true };
    private Dictionary<string, string>? pending;
    private string Journal => Path.Combine(runtimeDir, ".store-transaction.json");

    private string PathFor(string directory, string name)
    {
        if (Path.GetFileName(name) != name || string.IsNullOrWhiteSpace(name))
            throw new ArgumentException("Invalid storage name.");
        return Path.Combine(directory, name);
    }

    private static void WriteText(string path, string content)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        var temporary = path + ".tmp";
        File.WriteAllText(temporary, content);
        File.Move(temporary, path, true);
    }

    private void Recover()
    {
        if (!File.Exists(Journal))
            return;
        var changes = JsonSerializer.Deserialize<Dictionary<string, string>>(File.ReadAllText(Journal), Options)!;
        foreach (var path in changes.Keys)
            if (Path.GetDirectoryName(path) != dataDir && Path.GetDirectoryName(path) != runtimeDir)
                throw new InvalidDataException("Invalid transaction path.");
        foreach (var (path, content) in changes)
            WriteText(path, content);
        File.Delete(Journal);
    }

    public TResult Atomic<TResult>(Func<TResult> action)
    {
        lock (gate)
        {
            if (pending is not null)
                return action();
            Recover();
            pending = new(StringComparer.OrdinalIgnoreCase);
            try
            {
                var result = action();
                if (pending.Count > 0)
                {
                    WriteText(Journal, JsonSerializer.Serialize(pending, Options));
                    foreach (var (path, content) in pending)
                        WriteText(path, content);
                    File.Delete(Journal);
                }
                return result;
            }
            finally { pending = null; }
        }
    }

    private List<T> ReadFile<T>(string path)
    {
        var content = pending?.GetValueOrDefault(path) ?? (File.Exists(path) ? File.ReadAllText(path) : "[]");
        return JsonSerializer.Deserialize<List<T>>(content, Options) ?? [];
    }

    public List<T> All<T>(string name) => Atomic(() => ReadFile<T>(PathFor(dataDir, name + ".json")));
    public List<T> Read<T>(string filename) => Atomic(() => ReadFile<T>(PathFor(runtimeDir, filename)));

    public void Modify<T>(string filename, Action<List<T>> change) => Atomic(() =>
    {
        var path = PathFor(runtimeDir, filename);
        var data = ReadFile<T>(path);
        change(data);
        pending![path] = JsonSerializer.Serialize(data, Options);
        return true;
    });

    public TResult Update<T, TResult>(string name, Func<List<T>, TResult> change) => Atomic(() =>
    {
        var path = PathFor(dataDir, name + ".json");
        var data = ReadFile<T>(path);
        var result = change(data);
        pending![path] = JsonSerializer.Serialize(data, Options);
        return result;
    });
}

