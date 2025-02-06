using Aspire.Hosting.ApplicationModel;

namespace CommunityToolkit.Aspire.Hosting.Dapr;
/// <summary>
/// Provides extension methods for configuring dapr components with metadata
/// </summary>
public static class DaprMetadataResourceBuilderExtensions
{
    public static IResourceBuilder<T> WithMetadata<T>(this IResourceBuilder<T> builder, string name, string value) where T : IDaprComponentResource
    {
        return builder.WithAnnotation(new DaprComponentMetadataAnnotation(name, value));
    }

    public static IResourceBuilder<T> WithMetadata<T>(this IResourceBuilder<T> builder, string name, EndpointReference endpointReference) where T : IDaprComponentResource
    {
        return builder.WithAnnotation(new DaprComponentMetadataAnnotation(name, endpointReference));
    }
    public static IResourceBuilder<T> WithMetadata<T>(this IResourceBuilder<T> builder, string name, ParameterResource parameterResource) where T : IDaprComponentResource
    {
        return builder.WithAnnotation(new DaprComponentMetadataAnnotation(name, parameterResource));
    }
   
}
