using System.Globalization;
using System.Text.Json;

namespace OutfitStudio.Helpers;

public static class Extensions
{
    private static readonly CultureInfo Vi = new("vi-VN");

    public static string VND(this decimal v) => v.ToString("N0", Vi) + "đ";

    public static T GetObject<T>(this ISession s, string key) where T : new()
    {
        var json = s.GetString(key);
        return json == null ? new T() : JsonSerializer.Deserialize<T>(json) ?? new T();
    }

    public static void SetObject<T>(this ISession s, string key, T value)
        => s.SetString(key, JsonSerializer.Serialize(value));
}

