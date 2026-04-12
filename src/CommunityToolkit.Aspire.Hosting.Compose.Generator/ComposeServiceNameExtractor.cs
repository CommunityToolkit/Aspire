using System;
using System.Collections.Generic;
using System.Linq;

namespace CommunityToolkit.Aspire.Hosting.Compose.Generator;

/// <summary>
/// Minimal YAML parser that extracts service names from Docker Compose files.
/// Avoids YamlDotNet dependency for source generator compatibility.
/// </summary>
internal static class ComposeServiceNameExtractor
{
    private static readonly HashSet<string> KnownTopLevelKeys = new(StringComparer.OrdinalIgnoreCase)
    {
        "version", "services", "volumes", "networks", "configs", "secrets", "extensions", "name"
    };

    /// <summary>
    /// Extracts service names from compose YAML content.
    /// </summary>
    public static List<string> Extract(string yamlContent)
    {
        if (string.IsNullOrWhiteSpace(yamlContent))
            return [];

        string[] lines = yamlContent.Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries);
        int servicesIndex = FindTopLevelKey(lines, "services");
        return servicesIndex >= 0
            ? ExtractChildKeys(lines, servicesIndex)
            : ExtractV1Services(lines);
    }

    private static int FindTopLevelKey(string[] lines, string key)
    {
        for (int i = 0; i < lines.Length; i++)
        {
            string line = lines[i];

            if (IsSkippableLine(line))
                continue;

            string trimmed = line.TrimEnd();
            
            if (trimmed.Equals(key + ":", StringComparison.OrdinalIgnoreCase) || trimmed.StartsWith(key + ":", StringComparison.OrdinalIgnoreCase))
                return i;
        }

        return -1;
    }

    private static List<string> ExtractChildKeys(string[] lines, int parentIndex)
    {
        List<string> services = [];
        int childIndent = -1;

        for (int i = parentIndex + 1; i < lines.Length; i++)
        {
            if (IsEmptyOrComment(lines[i]))
                continue;

            int indent = GetIndentation(lines[i]);

            if (indent == 0)
                break;

            if (childIndent < 0)
                childIndent = indent;

            if (indent != childIndent)
                continue;

            string? name = ExtractKeyName(lines[i]);

            if (name is not null)
                services.Add(name);
        }

        return services;
    }

    private static List<string> ExtractV1Services(string[] lines)
    {
        List<string> services = [];
        services.AddRange((from t in lines where !IsEmptyOrComment(t) where !IsIndented(t) select ExtractKeyName(t)).Where(name => !KnownTopLevelKeys.Contains(name)));
        return services;
    }

    private static bool IsSkippableLine(string line) =>
        line.Length == 0 || line[0] is '#' or ' ' or '\t';

    private static bool IsEmptyOrComment(string line) =>
        string.IsNullOrWhiteSpace(line) || line.TrimStart().StartsWith("#");

    private static bool IsIndented(string line) =>
        line[0] is ' ' or '\t';

    private static int GetIndentation(string line)
    {
        int count = 0;

        foreach (char ch in line)
        {
            switch (ch)
            {
                case ' ': count++; break;
                case '\t': count += 2; break;
                default: return count;
            }
        }

        return count;
    }

    private static string? ExtractKeyName(string line)
    {
        string trimmed = line.TrimStart();
        int colonIndex = trimmed.IndexOf(':');

        if (colonIndex <= 0)
            return null;

        string key = trimmed[..colonIndex].Trim();

        if (key.StartsWith("-") || key.StartsWith("{") || key.StartsWith("["))
            return null;

        return StripQuotes(key);
    }

    private static string StripQuotes(string value) =>
        value switch
        {
            ['"', _, ..] when value[^1] == '"' => value.Substring(1, value.Length - 2),
            ['\'', _, ..] when value[^1] == '\'' => value.Substring(1, value.Length - 2),
            _ => value
        };
}
