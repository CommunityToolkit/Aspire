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
        builder.WithAnnotation(new DaprComponentConfigurationAnnotation((schema, ct) =>
        {
            var existing = schema.Spec.Metadata.Find(m => m.Name == name);
            if (existing is not null)
            {
                schema.Spec.Metadata.Remove(existing);
            }
            schema.Spec.Metadata.Add(new DaprComponentSpecMetadataValue
            {
                Name = name,
                Value = value
            });
            return Task.CompletedTask;
        }));

    /// <summary>
    /// Adds a value provider as metadata to the Dapr component that will be resolved at runtime.
    /// </summary>
    /// <param name="builder">The resource builder for the Dapr component being configured.</param>
    /// <param name="name">The name of the metadata property to add to the Dapr component configuration.</param>
    /// <param name="valueProvider">The value provider (e.g., EndpointReference, ConnectionStringReference) whose value will be resolved at runtime.
    /// The provider's value is stored as an environment variable and referenced through Dapr's secret key reference mechanism.</param>
    /// <returns>The resource builder instance.</returns>
    /// <remarks>
    /// This method enables dynamic configuration of Dapr components by:
    /// <list type="bullet">
    /// <item>Creating a unique environment variable name based on the component and metadata names</item>
    /// <item>Storing the value provider reference for runtime resolution</item>
    /// <item>Configuring the metadata to use a secretKeyRef pointing to the environment variable</item>
    /// </list>
    /// The environment variable name format is: {COMPONENT_NAME}_{METADATA_NAME} (uppercased, hyphens replaced with underscores).
    /// This approach allows for secure injection of dynamic values like endpoint URLs or connection strings that are only known at runtime.
    /// </remarks>
    /// <example>
    /// <code>
    /// var cache = builder.AddDaprComponent("cache", "state.redis")
    ///     .WithMetadata("redisHost", redis.GetEndpoint("tcp"));
    /// </code>
    /// </example>
    public static IResourceBuilder<IDaprComponentResource> WithMetadata(this IResourceBuilder<IDaprComponentResource> builder, string name, IValueProvider valueProvider)
    {
        // Create a unique environment variable name for this value provider
        // Note: We avoid using DAPR_ prefix as it's restricted by Dapr's local.env secret store
        var envVarName = $"{builder.Resource.Name}_{name}".ToUpperInvariant().Replace("-", "_");

        // Add an annotation to track this value provider reference
        builder.WithAnnotation(new DaprComponentValueProviderAnnotation(name, envVarName, valueProvider));

        return builder.WithAnnotation(new DaprComponentConfigurationAnnotation((schema, cancellationToken) =>
        {
            var existing = schema.Spec.Metadata.Find(m => m.Name == name);
            if (existing is not null)
            {
                schema.Spec.Metadata.Remove(existing);
            }

            // Use a secretKeyRef to reference the environment variable
            // This will be resolved from the environment at runtime
            schema.Spec.Metadata.Add(new DaprComponentSpecMetadataSecret
            {
                Name = name,
                SecretKeyRef = new DaprSecretKeyRef
                {
                    Name = envVarName,
                    Key = envVarName
                }
            });
            return Task.CompletedTask;
        }));
    }


    /// <summary>
    /// Adds a parameter resource as metadata to the Dapr component
    /// </summary>
    /// <param name="builder"></param>
    /// <param name="name"></param>
    /// <param name="parameterResource"></param>
    /// <returns></returns>
    public static IResourceBuilder<IDaprComponentResource> WithMetadata(this IResourceBuilder<IDaprComponentResource> builder, string name, ParameterResource parameterResource)
    {
        if (parameterResource.Secret)
        {
            return builder.WithAnnotation(new DaprComponentSecretAnnotation(parameterResource.Name, parameterResource))
                          .WithAnnotation(new DaprComponentConfigurationAnnotation((schema, ct) =>
                          {
                              var existing = schema.Spec.Metadata.Find(m => m.Name == name);
                              if (existing is not null)
                              {
                                  schema.Spec.Metadata.Remove(existing);
                              }
                              schema.Spec.Metadata.Add(new DaprComponentSpecMetadataSecret
                              {
                                  Name = name,
                                  SecretKeyRef = new DaprSecretKeyRef
                                  {
                                      Name = parameterResource.Name,
                                      Key = parameterResource.Name
                                  }
                              });
                              return Task.CompletedTask;
                          }));
        }

        return builder.WithAnnotation(new DaprComponentConfigurationAnnotation(async (schema, ct) =>
        {
            var existing = schema.Spec.Metadata.Find(m => m.Name == name);
            if (existing is not null)
            {
                schema.Spec.Metadata.Remove(existing);
            }
            schema.Spec.Metadata.Add(new DaprComponentSpecMetadataValue
            {
                Name = name,
                Value = (await ((IValueProvider)parameterResource).GetValueAsync(ct))!
            });
        }));
    }
}
