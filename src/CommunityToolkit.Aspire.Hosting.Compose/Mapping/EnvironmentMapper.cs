using Aspire.Hosting;
using Aspire.Hosting.ApplicationModel;
using CommunityToolkit.Aspire.Hosting.Compose.Parsing.Contracts;

namespace CommunityToolkit.Aspire.Hosting.Compose.Mapping;

/// <summary>
/// Maps compose environment variables to Aspire environment.
/// </summary>
internal static class EnvironmentMapper
{
    public static void Map(IResourceBuilder<ContainerResource> resourceBuilder, ComposeService service)
    {
        if (service.Environment is not { } env)
            return;

        foreach ((string key, string value) in Parse(env))
            resourceBuilder.WithEnvironment(key, value);
    }

    internal static Dictionary<string, string> Parse(object environment)
    {
        Dictionary<string, string> result = new(StringComparer.OrdinalIgnoreCase);

        switch (environment)
        {
            case Dictionary<object, object> dict:
                foreach (KeyValuePair<object, object> kvp in dict)
                    result[kvp.Key.ToString()!] = kvp.Value?.ToString() ?? string.Empty;
                break;

            case List<object> list:
                foreach (object item in list)
                {
                    string str = item.ToString()!;
                    int eqIndex = str.IndexOf('=');
                    result[eqIndex > 0 ? str[..eqIndex] : str] = eqIndex > 0 ? str[(eqIndex + 1)..] : string.Empty;
                }
                break;
        }

        return result;
    }
}
