namespace CommunityToolkit.Aspire.Hosting.Dapr;
internal class DaprComponentSchema
{
    public string ApiVersion { get; set; } = "dapr.io/v1alpha1";
    public string Kind { get; set; } = "Component";
    public DaprComponentMetadata Metadata { get; init; }
    public DaprComponentSpec Spec { get; init; }

    internal DaprComponentSchema(string name, string type)
    {
        Metadata = new DaprComponentMetadata { Name = name };
        Spec = new DaprComponentSpec { Type = type };
    }

    override public string ToString()
    {
        return
            $"""
            apiVersion: {ApiVersion}
            kind: {Kind}
            metadata:
              name: {Metadata.Name}
            spec:
              {Spec.Metadata}
            """;
    }
}
internal class DaprComponentMetadata
{
    public required string Name { get; set; }
}

internal class DaprComponentSpec<TSpecMetadata> where TSpecMetadata : IDaprComponentSpecMetadata
{
    public required string Type { get; set; }
    public string Version { get; set; } = "v1";
    public TSpecMetadata? Metadata { get; set; }

    override public string ToString()
    {
        return
            $"""
            type: {Type}
            version: {Version}
            metadata: 
              {Metadata}
            """;
    }
}
public abstract class DaprComponentSpecMetadata
{
    public required string Name { get; set; }

}
public class DaprComponentSpecMetadataValue : DaprComponentSpecMetadata
{
    public required string Value { get; set; }
    override public string ToString()
    {
        return
            $"""
            - name: {Name}
              value: {Value}
            """;
    }
}
public class DaprComponentSpecMetadataSecret : DaprComponentSpecMetadata
{
    public required DaprSecretKeyRef SecretKeyRef { get; set; }

    override public string ToString()
    {
        return
            $"""
            - name: {Name}
              secretKeyRef:
                name: {SecretKeyRef.Name}
                key: {SecretKeyRef.Key}
            """;
    }
}
public class DaprSecretKeyRef
{
    public required string Name { get; set; }
    public required string Key { get; set; }
}
public interface IDaprComponentSpecMetadata;
public class RedisStateStoreMetadata :IDaprComponentSpecMetadata
{
    public required DaprComponentSpecMetadataValue RedisHost { get; set; }
    public required DaprComponentSpecMetadataSecret RedisPassword { get; set; }

}