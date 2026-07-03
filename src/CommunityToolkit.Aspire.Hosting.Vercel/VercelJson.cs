using System.Diagnostics.CodeAnalysis;
using System.Text.Json;

namespace CommunityToolkit.Aspire.Hosting.Vercel;

internal static class VercelJson
{
    public static string? GetStringProperty(JsonElement element, string propertyName)
        => element.TryGetProperty(propertyName, out var property)
            && property.ValueKind == JsonValueKind.String
            && !string.IsNullOrWhiteSpace(property.GetString())
                ? property.GetString()
                : null;

    public static bool TryGetString(JsonElement element, string propertyName, [NotNullWhen(true)] out string? value)
    {
        value = GetStringProperty(element, propertyName);
        return value is not null;
    }

    public static JsonElement.ArrayEnumerator EnumerateArrayOrNamedArray(JsonElement root, string propertyName)
    {
        if (root.ValueKind == JsonValueKind.Array)
        {
            return root.EnumerateArray();
        }

        if (root.ValueKind == JsonValueKind.Object
            && root.TryGetProperty(propertyName, out var array)
            && array.ValueKind == JsonValueKind.Array)
        {
            return array.EnumerateArray();
        }

        throw new JsonException($"Expected JSON array or object property '{propertyName}'.");
    }
}
