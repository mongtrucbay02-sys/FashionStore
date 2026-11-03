using System.Data;
using System.Text.Json;
using Microsoft.Data.SqlClient;

namespace OutfitStudio.Services;

internal sealed class SqlServerStore(string connectionString) : IStoreBackend
{
    private readonly object gate = new();
    private SqlConnection? connection;
    private SqlTransaction? transaction;
    private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNameCaseInsensitive = true };

    // The transaction-scoped SQL lock also serializes multiple website processes, not just local threads.
    public TResult Atomic<TResult>(Func<TResult> action)
    {
        lock (gate)
        {
            if (connection is not null)
                return action();
            using var current = new SqlConnection(connectionString);
            current.Open();
            using var tx = current.BeginTransaction(IsolationLevel.Serializable);
            connection = current;
            transaction = tx;
            try
            {
                using var locked = new SqlCommand("DECLARE @result int; EXEC @result=sys.sp_getapplock @Resource=N'FashionStore.Store.V1',@LockMode='Exclusive',@LockOwner='Transaction',@LockTimeout=15000; IF @result<0 THROW 51002,N'Không lấy được khóa dữ liệu. Vui lòng thử lại.',1;", current, tx);
                locked.ExecuteNonQuery();
                var result = action();
                tx.Commit();
                return result;
            }
            catch { try { tx.Rollback(); } catch (Exception error) when (error is InvalidOperationException or SqlException) { } throw; }
            finally { connection = null; transaction = null; }
        }
    }

    private static string Collection(string name)
    {
        if (name.EndsWith(".json", StringComparison.OrdinalIgnoreCase))
            name = name[..^5];
        if (name is not ("categories" or "products" or "users" or "orders" or "reviews" or "stock-movements" or "outfit-products" or "qr-payment-settings" or "inventory-before-sizes"))
            throw new InvalidOperationException("Unknown SQL collection.");
        return name;
    }
    internal void RecordImport(string source)
    {
        using var command = new SqlCommand("INSERT dbo.ImportHistory(Id,ImportedAt,SourceDescription) VALUES(1,SYSDATETIMEOFFSET(),@source)", connection, transaction);
        command.Parameters.Add("@source", SqlDbType.NVarChar, 200).Value = source;
        command.ExecuteNonQuery();
    }
    private List<T> ReadRows<T>(string name)
    {
        using var command = new SqlCommand("dbo.StoreRead", connection, transaction) { CommandType = CommandType.StoredProcedure };
        command.Parameters.Add("@Collection", SqlDbType.NVarChar, 100).Value = Collection(name);
        var json = (string?)command.ExecuteScalar() ?? "[]";
        return JsonSerializer.Deserialize<List<T>>(json, JsonOptions) ?? [];
    }
    private void WriteRows<T>(string name, List<T> rows)
    {
        using var command = new SqlCommand("dbo.StoreWrite", connection, transaction) { CommandType = CommandType.StoredProcedure };
        command.Parameters.Add("@Collection", SqlDbType.NVarChar, 100).Value = Collection(name);
        command.Parameters.Add("@Data", SqlDbType.NVarChar, -1).Value = JsonSerializer.Serialize(rows, JsonOptions);
        command.ExecuteNonQuery();
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
}
