using Aspire.Hosting.ApplicationModel;
using Aspire.Hosting.Utils;
using CommunityToolkit.Aspire.Hosting.Supabase;

namespace Aspire.Hosting;

/// <summary>
/// Provides extension methods for adding Supabase resources to the application model.
/// </summary>
public static class SupabaseBuilderExtensions
{
    private const int SupabaseApiPort = 8000;
    private const int SupabaseDatabasePort = 5432;

    /// <summary>
    /// Adds a Supabase container resource to the application model.
    /// </summary>
    public static IResourceBuilder<SupabaseResource> AddSupabase(
        this IDistributedApplicationBuilder builder,
        string name,
        IResourceBuilder<ParameterResource>? password = null,
        int? apiPort = null,
        int? dbPort = null,
        SupabaseModuleOptions? modules = null)
    {
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentNullException.ThrowIfNull(name);

        modules ??= new SupabaseModuleOptions();

        var passwordParam = password?.Resource ?? ParameterResourceBuilderExtensions.CreateDefaultPasswordParameter(builder, $"{name}-password");
        var resource = new SupabaseResource(name, passwordParam);

        var builderResult = builder.AddResource(resource)
            .WithImage(SupabaseContainerImageTags.PostgresImage, SupabaseContainerImageTags.PostgresTag)
            .WithImageRegistry(SupabaseContainerImageTags.Registry)
            .WithHttpEndpoint(targetPort: SupabaseApiPort, port: apiPort, name: SupabaseResource.PrimaryEndpointName)
            .WithEndpoint(targetPort: SupabaseDatabasePort, port: dbPort, name: SupabaseResource.DatabaseEndpointName)
            .WithEnvironment(context =>
            {
                context.EnvironmentVariables["POSTGRES_PASSWORD"] = resource.PasswordParameter;
                // Add more Supabase env vars as needed
            });

        // Add/skip modules based on options
        if (modules.EnableAuth)
        {
            // Add Auth container/resource setup here
        }
        if (modules.EnableStorage)
        {
            // Add Storage container/resource setup here
        }
        if (modules.EnableMinio)
        {
            // Add Minio container/resource setup here
        }
        if (modules.EnableEdgeFunctions)
        {
            // Add Edge Functions container/resource setup here
        }
        if (modules.EnableVector)
        {
            // Add Vector container/resource setup here
        }

        return builderResult;
    }

    /// <summary>
    /// Adds a named volume for the data folder to a Supabase container resource.
    /// </summary>
    public static IResourceBuilder<SupabaseResource> WithDataVolume(
        this IResourceBuilder<SupabaseResource> builder,
        string? name = null)
    {
        ArgumentNullException.ThrowIfNull(builder);
        return builder.WithVolume(name ?? VolumeNameGenerator.Generate(builder, "data"), "/var/lib/postgresql/data");
    }

    /// <summary>
    /// Adds a bind mount for the data folder to a Supabase container resource.
    /// </summary>
    public static IResourceBuilder<SupabaseResource> WithDataBindMount(
        this IResourceBuilder<SupabaseResource> builder,
        string source)
    {
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentNullException.ThrowIfNull(source);
        return builder.WithBindMount(source, "/var/lib/postgresql/data");
    }
}