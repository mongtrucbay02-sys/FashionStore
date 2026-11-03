using System.Data;
using System.Text.Json;
using Npgsql;
using NpgsqlTypes;

namespace OutfitStudio.Services;

// Stores each existing FashionStore collection as one JSONB array in the private Supabase schema.
internal sealed class SupabaseStore(string connectionString) : IStoreBackend
{
    private readonly object gate = new();
    private NpgsqlConnection? connection;
    private NpgsqlTransaction? transaction;
    private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNameCaseInsensitive = true };

    public TResult Atomic<TResult>(Func<TResult> action)
    {
        lock (gate)
        {
            if (connection is not null) return action();
            using var current = new NpgsqlConnection(connectionString);
            current.Open();
            using var tx = current.BeginTransaction(IsolationLevel.Serializable);
            connection = current;
            transaction = tx;
            try
            {
                using var locked = new NpgsqlCommand("SELECT pg_advisory_xact_lock(hashtext('FashionStore.Store.V1'))", current, tx);
                locked.ExecuteNonQuery();
                var result = action();
                tx.Commit();
                return result;
            }
            catch
            {
                try { tx.Rollback(); } catch (InvalidOperationException) { }
                throw;
            }
            finally { connection = null; transaction = null; }
        }
    }

    private static string Collection(string name)
    {
        if (name.EndsWith(".json", StringComparison.OrdinalIgnoreCase)) name = name[..^5];
        if (name is not ("categories" or "products" or "users" or "orders" or "reviews" or "stock-movements" or "outfit-products" or "qr-payment-settings" or "inventory-before-sizes"))
            throw new InvalidOperationException("Unknown store collection.");
        return name;
    }

    public List<T> All<T>(string name) => Atomic(() => ReadRows<T>(name));
    public List<T> Read<T>(string filename) => All<T>(filename);

    public void Modify<T>(string filename, Action<List<T>> change) => Atomic(() =>
    {
        var rows = ReadRows<T>(filename);
        change(rows);
        WriteRows(filename, rows);
        return true;
    });

    public TResult Update<T, TResult>(string name, Func<List<T>, TResult> change) => Atomic(() =>
    {
        var rows = ReadRows<T>(name);
        var result = change(rows);
        WriteRows(name, rows);
        return result;
    });

    private List<T> ReadRows<T>(string name)
    {
        using var command = new NpgsqlCommand(
            "SELECT data::text FROM app_private.store_collections WHERE collection_name = @collection", connection, transaction);
        command.Parameters.AddWithValue("collection", Collection(name));
        var json = (string?)command.ExecuteScalar() ?? "[]";
        return JsonSerializer.Deserialize<List<T>>(json, JsonOptions) ?? [];
    }

    private void WriteRows<T>(string name, List<T> rows)
    {
        using var command = new NpgsqlCommand("""
            INSERT INTO app_private.store_collections(collection_name, data)
            VALUES (@collection, @data)
            ON CONFLICT (collection_name) DO UPDATE SET data = EXCLUDED.data, updated_at = now()
            """, connection, transaction);
        command.Parameters.AddWithValue("collection", Collection(name));
        command.Parameters.Add(new NpgsqlParameter("data", NpgsqlDbType.Jsonb) { Value = JsonSerializer.Serialize(rows) });
        command.ExecuteNonQuery();
    }

    public void RecordImport(string source) => Atomic(() =>
    {
        using var command = new NpgsqlCommand(
            "INSERT INTO app_private.import_history(source_description) VALUES (@source)", connection, transaction);
        command.Parameters.AddWithValue("source", source);
        command.ExecuteNonQuery();
        return true;
    });
}
