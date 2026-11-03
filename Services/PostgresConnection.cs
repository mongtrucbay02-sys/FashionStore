using Npgsql;

namespace OutfitStudio.Services;

internal static class PostgresConnection
{
    public static string Normalize(string value)
    {
        if (!Uri.TryCreate(value, UriKind.Absolute, out var uri) ||
            (uri.Scheme != "postgres" && uri.Scheme != "postgresql"))
            return new NpgsqlConnectionStringBuilder(value)
            {
                SslMode = SslMode.Require,
                GssEncryptionMode = GssEncryptionMode.Disable
            }.ConnectionString;

        var credentials = uri.UserInfo.Split(':', 2);
        var builder = new NpgsqlConnectionStringBuilder
        {
            Host = uri.Host,
            Port = uri.IsDefaultPort ? 5432 : uri.Port,
            Database = Uri.UnescapeDataString(uri.AbsolutePath.TrimStart('/')),
            Username = Uri.UnescapeDataString(credentials[0]),
            // Supabase requires TLS. Require encrypts the connection while allowing
            // the platform's certificate chain until a project CA certificate is configured.
            SslMode = SslMode.Require,
            GssEncryptionMode = GssEncryptionMode.Disable
        };
        if (credentials.Length > 1) builder.Password = Uri.UnescapeDataString(credentials[1]);
        return builder.ConnectionString;
    }
}
