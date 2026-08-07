namespace CommunityToolkit.Aspire.Hosting.Vercel;

/// <summary>
/// Parses the limited dotenv syntax emitted by <c>vercel pull</c> so deploy can read the
/// short-lived OIDC token without taking a dependency on user-authored dotenv semantics.
/// </summary>
internal static class VercelDotEnvParser
{
    public static Dictionary<string, string> Parse(IEnumerable<string> lines)
    {
        // Vercel writes dotenv files such as `.vercel/.env.production.local` during pull.
        // We only need the VERCEL_OIDC_TOKEN line. This intentionally supports the subset the
        // CLI emits: comments/blank lines, KEY=value, single/double quoted values, and common
        // backslash escapes. It is not a general dotenv evaluator with interpolation.
        // See https://vercel.com/docs/cli/pull.
        Dictionary<string, string> values = new(StringComparer.Ordinal);

        foreach (string rawLine in lines)
        {
            string line = rawLine.Trim();
            if (line.Length == 0 || line[0] == '#')
            {
                continue;
            }

            int separator = line.IndexOf('=');
            if (separator <= 0)
            {
                continue;
            }

            string key = line[..separator].Trim();
            string value = line[(separator + 1)..].Trim();
            values[key] = UnquoteValue(value);
        }

        return values;
    }

    private static string UnquoteValue(string value)
    {
        if (value.Length >= 2
            && ((value[0] == '"' && value[^1] == '"')
                || (value[0] == '\'' && value[^1] == '\'')))
        {
            value = value[1..^1];
        }

        return value.Replace("\\n", "\n", StringComparison.Ordinal)
            .Replace("\\r", "\r", StringComparison.Ordinal)
            .Replace("\\\"", "\"", StringComparison.Ordinal)
            .Replace("\\\\", "\\", StringComparison.Ordinal);
    }
}
