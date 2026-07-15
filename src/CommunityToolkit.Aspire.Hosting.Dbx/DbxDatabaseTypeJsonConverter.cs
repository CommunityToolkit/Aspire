using Aspire.Hosting.ApplicationModel;
using System.Reflection;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace CommunityToolkit.Aspire.Hosting.Dbx;

/// <summary>
/// JSON converter for <see cref="DbxDatabaseType"/> that respects <see cref="JsonPropertyNameAttribute"/> on each enum member.
/// </summary>
internal sealed class DbxDatabaseTypeJsonConverter : JsonConverter<DbxDatabaseType>
{
    private static readonly Dictionary<string, DbxDatabaseType> s_readMap;
    private static readonly Dictionary<DbxDatabaseType, string> s_writeMap;

    static DbxDatabaseTypeJsonConverter()
    {
        var fields = typeof(DbxDatabaseType).GetFields(BindingFlags.Public | BindingFlags.Static);

        s_readMap  = new Dictionary<string, DbxDatabaseType>(fields.Length, StringComparer.OrdinalIgnoreCase);
        s_writeMap = new Dictionary<DbxDatabaseType, string>(fields.Length);

        foreach (var field in fields)
        {
            var value = (DbxDatabaseType)field.GetValue(null)!;
            var jsonName = field.GetCustomAttribute<JsonPropertyNameAttribute>()?.Name ?? field.Name;

            s_readMap[jsonName]  = value;
            s_writeMap[value]    = jsonName;
        }
    }

    /// <inheritdoc/>
    public override DbxDatabaseType Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        var raw = reader.GetString() ?? throw new JsonException("Expected a string value for DbxDatabaseType.");

        if (s_readMap.TryGetValue(raw, out var result))
        {
            return result;
        }

        throw new JsonException($"Unknown DbxDatabaseType value: '{raw}'.");
    }

    /// <inheritdoc/>
    public override void Write(Utf8JsonWriter writer, DbxDatabaseType value, JsonSerializerOptions options)
    {
        writer.WriteStringValue(s_writeMap[value]);
    }
}
