using CommunityToolkit.Aspire.Hosting.Compose.Parsing.Contracts;
using YamlDotNet.Core;
using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;

namespace CommunityToolkit.Aspire.Hosting.Compose.Parsing;

/// <summary>
/// Parses Docker Compose YAML files into <see cref="Compose"/> models.
/// Supports all compose format versions: v1 (legacy), v2.x, v3.x, and modern Compose Spec.
/// </summary>
internal static class ComposeParser
{
    private static readonly HashSet<string> KnownTopLevelKeys = new(StringComparer.OrdinalIgnoreCase)
    {
        ComposeConstants.TopLevel.Version, ComposeConstants.TopLevel.Services, ComposeConstants.TopLevel.Volumes, ComposeConstants.TopLevel.Networks,
        ComposeConstants.TopLevel.Configs, ComposeConstants.TopLevel.Secrets, ComposeConstants.TopLevel.Extensions, ComposeConstants.TopLevel.Name
    };

    private static readonly IDeserializer Deserializer = new DeserializerBuilder()
        .WithNamingConvention(UnderscoredNamingConvention.Instance)
        .IgnoreUnmatchedProperties()
        .Build();

    /// <summary>
    /// Parses a compose file from the specified path.
    /// </summary>
    /// <param name="filePath">The absolute or relative path to the compose YAML file.</param>
    /// <returns>The parsed <see cref="Compose"/>.</returns>
    /// <exception cref="FileNotFoundException">Thrown when the compose file does not exist.</exception>
    /// <exception cref="ComposeParseException">Thrown when the YAML content is invalid.</exception>
    public static ComposeFile Parse(string filePath)
    {
        ArgumentException.ThrowIfNullOrEmpty(filePath);
        string fullPath = Path.GetFullPath(filePath);

        if (!File.Exists(fullPath))
            throw new FileNotFoundException($"Compose file not found: '{fullPath}'. Ensure the file exists and the path is correct.", fullPath);

        string yaml = File.ReadAllText(fullPath);
        
        return ParseYaml(yaml, fullPath);
    }

    /// <summary>
    /// Parses compose YAML content from a string.
    /// </summary>
    /// <param name="yamlContent">The YAML content to parse.</param>
    /// <param name="sourcePath">Optional source path for error messages.</param>
    /// <returns>The parsed <see cref="Compose"/>.</returns>
    /// <exception cref="ComposeParseException">Thrown when the YAML content is invalid.</exception>
    internal static ComposeFile ParseYaml(string yamlContent, string? sourcePath = null)
    {
        ArgumentException.ThrowIfNullOrEmpty(yamlContent);

        try
        {
            Dictionary<string, object?>? rawDict = Deserializer.Deserialize<Dictionary<string, object?>>(yamlContent);

            if (rawDict is null || rawDict.Count == 0)
                throw new ComposeParseException("Compose file is empty or contains no valid YAML content.", sourcePath);

            if (IsV1Format(rawDict))
                return ParseV1Format(rawDict, sourcePath);

            ComposeFile model = Deserializer.Deserialize<ComposeFile>(yamlContent);

            if (model.Services is null || model.Services.Count == 0)
                throw new ComposeParseException("Compose file does not contain any services.", sourcePath);

            return model;
        }
        catch (YamlException ex)
        {
            throw new ComposeParseException($"Invalid YAML in compose file{(sourcePath is not null ? $" '{sourcePath}'" : string.Empty)}: {ex.Message}", sourcePath, ex);
        }
    }

    private static bool IsV1Format(Dictionary<string, object?> rawDict)
    {
        if (rawDict.ContainsKey(ComposeConstants.TopLevel.Services)) return false;

        foreach ((string key, object? value) in rawDict)
        {
            if (KnownTopLevelKeys.Contains(key))
                continue;

            if (value is Dictionary<object, object>)
                return true;
        }

        return false;
    }

    private static ComposeFile ParseV1Format(Dictionary<string, object?> rawDict, string? sourcePath)
    {
        Dictionary<string, object?> servicesYaml = new();
        string? version = null;
        Dictionary<string, object?>? volumes = null;
        Dictionary<string, object?>? networks = null;

        foreach ((string key, object? value) in rawDict)
        {
            switch (key.ToLowerInvariant())
            {
                case ComposeConstants.TopLevel.Version:
                    version = value?.ToString();
                    break;
                case ComposeConstants.TopLevel.Volumes:
                    volumes = value as Dictionary<string, object?>;
                    break;
                case ComposeConstants.TopLevel.Networks:
                    networks = value as Dictionary<string, object?>;
                    break;
                default:
                    if (!KnownTopLevelKeys.Contains(key))
                        servicesYaml[key] = value;
                    break;
            }
        }

        if (servicesYaml.Count == 0)
            throw new ComposeParseException("Compose file (v1 format) does not contain any services.", sourcePath);

        Dictionary<string, object?> wrappedDict = new() { [ComposeConstants.TopLevel.Services] = servicesYaml };

        if (version is not null)
            wrappedDict[ComposeConstants.TopLevel.Version] = version;

        if (volumes is not null)
            wrappedDict[ComposeConstants.TopLevel.Volumes] = volumes;

        if (networks is not null)
            wrappedDict[ComposeConstants.TopLevel.Networks] = networks;

        ISerializer serializer = new SerializerBuilder()
            .WithNamingConvention(UnderscoredNamingConvention.Instance)
            .Build();
        string wrappedYaml = serializer.Serialize(wrappedDict);
        ComposeFile model = Deserializer.Deserialize<ComposeFile>(wrappedYaml);

        if (model.Services is null || model.Services.Count == 0)
            throw new ComposeParseException("Compose file (v1 format) does not contain any valid services.", sourcePath);

        return model;
    }
}
