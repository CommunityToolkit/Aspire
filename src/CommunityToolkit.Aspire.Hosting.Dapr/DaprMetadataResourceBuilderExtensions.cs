using Aspire.Hosting.ApplicationModel;
using CommunityToolkit.Aspire.Hosting.Dapr;

namespace Aspire.Hosting;

/// <summary>
/// Provides extension methods for configuring dapr components with metadata
/// </summary>
public static class DaprMetadataResourceBuilderExtensions
{
    /// <summary>
    /// Adds static value metadata to the Dapr component
    /// </summary>
    /// <param name="builder"></param>
    /// <param name="name"></param>
    /// <param name="value"></param>
    /// <returns></returns>
    public static IResourceBuilder<IDaprComponentResource> WithMetadata(this IResourceBuilder<IDaprComponentResource> builder, string name, string value) =>
        builder.WithAnnotation(new DaprComponentConfigurationAnnotation(schema => schema.Spec.Metadata.Add(new DaprComponentSpecMetadataValue { Name = name, Value = value })));


    /// <summary>
    /// Adds a parameter resource as metadata to the Dapr component
    /// </summary>
    /// <param name="builder"></param>
    /// <param name="name"></param>
    /// <param name="parameterResource"></param>
    /// <returns></returns>
    public static IResourceBuilder<IDaprComponentResource> WithMetadata(this IResourceBuilder<IDaprComponentResource> builder, string name, ParameterResource parameterResource)
    {
        return parameterResource.Secret ? builder
            .WithAnnotation(new DaprComponentSecretAnnotation(parameterResource.Name, parameterResource.Value))
            .WithAnnotation(new DaprComponentConfigurationAnnotation(schema =>
                schema.Spec.Metadata.Add(new DaprComponentSpecMetadataSecret
                {
                    Name = name,
                    SecretKeyRef = new DaprSecretKeyRef
                    {
                        Name = parameterResource.Name,
                        Key = parameterResource.Value
                    }
                }))) : builder.WithAnnotation(new DaprComponentConfigurationAnnotation(schema =>
                schema.Spec.Metadata.Add(new DaprComponentSpecMetadataValue
                {
                    Name = name,
                    Value = parameterResource.Value
                })));
    }
}
