using Aspire.Hosting.ApplicationModel;
using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;

namespace CommunityToolkit.Aspire.Hosting.Dapr;

internal interface IDaprComponentSpecMetadata : IList<DaprComponentSpecMetadata>;

internal class DaprComponentSchema
{
    private static readonly ISerializer serializer = BuildSerializer();
    private static readonly IDeserializer deserializer = BuildDeSerializer();

    public string ApiVersion { get; init; } = "dapr.io/v1alpha1";
    public string Kind { get; init; } = "Component";
    public DaprComponentAuth? Auth { get; set; }
    public DaprComponentMetadata Metadata { get; init; } = default!;
    public DaprComponentSpec Spec { get; init; } = default!;

    // Required for deserialization
    public DaprComponentSchema() { }

    public DaprComponentSchema(string name, string type)
    {
        Metadata = new DaprComponentMetadata { Name = name };
        Spec = new DaprComponentSpec
        {
            Type = type,
            Metadata = []
        };
    }
    public override string ToString() => serializer.Serialize(this);
    
    /// <summary>
    /// Resolves all async values in the component schema
    /// </summary>
    public async Task ResolveAllValuesAsync(CancellationToken cancellationToken = default)
    {
        foreach (var metadata in Spec.Metadata)
        {
            if (metadata is DaprComponentSpecMetadataValueProvider valueProvider)
            {
                await valueProvider.ResolveValueAsync(cancellationToken);
            }
        }
    }

    public static DaprComponentSchema FromYaml(string yamlContent) =>
        deserializer.Deserialize<DaprComponentSchema>(yamlContent);
    private static IDeserializer BuildDeSerializer()
    {
        DeserializerBuilder builder = new();
        builder.WithNamingConvention(CamelCaseNamingConvention.Instance)
            .WithTypeDiscriminatingNodeDeserializer(static o =>
            {
                Dictionary<string, Type> keyMappings = new()
                {
                    ["value"] = typeof(DaprComponentSpecMetadataValue),
                    ["secretKeyRef"] = typeof(DaprComponentSpecMetadataSecret)
                };
                o.AddUniqueKeyTypeDiscriminator<DaprComponentSpecMetadata>(keyMappings);
            });
        return builder.Build();
    }

    private static ISerializer BuildSerializer()
    {
        SerializerBuilder builder = new();
        builder.WithNamingConvention(CamelCaseNamingConvention.Instance)
               .ConfigureDefaultValuesHandling(DefaultValuesHandling.OmitDefaults);
        return builder.Build();
    }

}
internal class DaprComponentMetadata
{
    public required string Name { get; init; }
    public string? Namespace { get; init; }

}

internal class DaprComponentAuth
{
    public required string SecretStore { get; init; }
}

internal class GenericDaprComponentSpecMetadata : List<DaprComponentSpecMetadata>, IDaprComponentSpecMetadata;

internal class DaprComponentSpec : DaprComponentSpec<GenericDaprComponentSpecMetadata> { }

internal class DaprComponentSpec<TSpecMetadata> where TSpecMetadata : IDaprComponentSpecMetadata
{
    public required string Type { get; init; }
    public string Version { get; init; } = "v1";
    public required TSpecMetadata Metadata { get; init; }
}

/// <summary>
/// Represents a Dapr component spec metadata item
/// </summary>
public abstract class DaprComponentSpecMetadata
{
    /// <summary>
    /// The name of the metadata item
    /// </summary>
    [YamlMember(Order = 1)]
    public required string Name { get; init; }
}

/// <summary>
/// Represents a Dapr component spec metadata item with a value
/// </summary>
public sealed class DaprComponentSpecMetadataValue : DaprComponentSpecMetadata
{
    /// <summary>
    /// The value of the metadata item
    /// </summary>
    [YamlMember(Order = 2)]
    public required string Value { get; set; }
}

internal sealed class DaprComponentSpecMetadataValueProvider : DaprComponentSpecMetadata
{
    /// <summary>
    /// The value provider for deferred evaluation
    /// </summary>
    [YamlIgnore]
    public required IValueProvider ValueProvider { get; init; }
    
    /// <summary>
    /// The resolved value (populated after resolution)
    /// </summary>
    [YamlMember(Order = 2)]
    public string? Value { get; set; }
    
    /// <summary>
    /// Resolves the value from the provider
    /// </summary>
    public async Task ResolveValueAsync(CancellationToken cancellationToken = default)
    {
        Value = await ValueProvider.GetValueAsync(cancellationToken) ?? string.Empty;
    }
}

/// <summary>
/// Represents a Dapr component spec metadata item with a secret key reference
/// </summary>
public sealed class DaprComponentSpecMetadataSecret : DaprComponentSpecMetadata
{
    /// <summary>
    /// The secret key reference of the metadata item
    /// </summary>
    [YamlMember(Order = 2)]
    public required DaprSecretKeyRef SecretKeyRef { get; set; }

}

/// <summary>
/// Represents a Dapr secret key reference
/// </summary>
public sealed class DaprSecretKeyRef
{
    /// <summary>
    /// The name of the secret
    /// </summary>
    public required string Name { get; init; }
    /// <summary>
    /// The key of the secret
    /// </summary>
    public required string Key { get; init; }
};

