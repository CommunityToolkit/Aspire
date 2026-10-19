using Aspire.Hosting.ApplicationModel;
using Aspire.Hosting.JavaScript;
using Microsoft.Extensions.Hosting;

namespace Aspire.Hosting;

/// <summary>
/// Provides extension methods for adding JavaScript applications to the distributed application builder.
/// </summary>
public static class JavaScriptHostingExtensions
{
    /// <summary>
    /// Maps the endpoint port for the <see cref="JavaScriptAppResource"/> to the appropriate command line argument.
    /// </summary>
    /// <ats-summary>Maps the endpoint port for the JavaScript app resource to the appropriate command line argument</ats-summary>
    /// <param name="builder">The Node.js app resource.</param>
    /// <param name="endpointName">The name of the endpoint to map. If not specified, it will use the first HTTP or HTTPS endpoint found.</param>
    /// <returns>A reference to the <see cref="IResourceBuilder{T}"/>.</returns>
    [AspireExport]
    public static IResourceBuilder<TResource> WithMappedEndpointPort<TResource>(this IResourceBuilder<TResource> builder, string? endpointName = null) where TResource : JavaScriptAppResource
    {
        ArgumentNullException.ThrowIfNull(builder);

        return builder.WithArgs(ctx =>
        {
            var resource = builder.Resource;

            // monorepo tools and npm (from Aspire.Hosting.JavaScript) need `--`, but yarn and pnpm don't
            if (!resource.TryGetLastAnnotation<JavaScriptPackageManagerAnnotation>(out var executionAnnotation) || executionAnnotation.ExecutableName == "npm")
            {
                ctx.Args.Add("--");
            }

            // Find the target endpoint by name, or default to http/https if no name specified
            var targetEndpoint = endpointName is not null
                ? resource.GetEndpoint(endpointName)
                : resource.GetEndpoints().FirstOrDefault(e => e.EndpointName == "https") ?? resource.GetEndpoint("http");

            ctx.Args.Add("--port");
            ctx.Args.Add(targetEndpoint.Property(EndpointProperty.TargetPort));
        });
    }

    internal static void AddMonorepoInstaller<TResource>(
        this IResourceBuilder<TResource> builder,
        string packageManager,
        bool install,
        Action<IResourceBuilder<JavaScriptInstallerResource>>? configureInstaller)
        where TResource : Resource
    {
        // Always add the configured annotation to track which package manager was set up
        builder.WithAnnotation(new JavaScriptPackageManagerConfiguredAnnotation(packageManager));

        // Only install packages if not in publish mode and install is true
        if (!builder.ApplicationBuilder.ExecutionContext.IsPublishMode && install)
        {
            var installerName = $"{builder.Resource.Name}-installer";
            var workingDirectory = builder.Resource switch
            {
                NxResource nx => nx.WorkingDirectory,
                TurborepoResource turbo => turbo.WorkingDirectory,
                _ => throw new InvalidOperationException($"Unsupported resource type: {builder.Resource.GetType().Name}")
            };

            // Check if installer already exists
            if (builder.ApplicationBuilder.TryCreateResourceBuilder<JavaScriptInstallerResource>(installerName, out var existingResource))
            {
                // Installer already exists, don't create a new one
                return;
            }

            var installer = new JavaScriptInstallerResource(installerName, workingDirectory);

            var installerBuilder = builder.ApplicationBuilder
                .AddResource(installer)
                .WithCommand(packageManager)
                .WithParentRelationship(builder.Resource)
                .WithIconName("CodeJsRectangle")
                .ExcludeFromManifest();

            // Set up the installer command based on package manager
            installerBuilder.WithArgs("install");

            configureInstaller?.Invoke(installerBuilder);

            // Add annotation to track the installer
            builder.WithAnnotation(new JavaScriptPackageInstallerAnnotation(installer));
        }
    }

    internal static IResourceBuilder<TResource> WithNodeDefaults<TResource>(this IResourceBuilder<TResource> builder) where TResource : JavaScriptAppResource =>
        builder.WithOtlpExporter()
            .WithEnvironment("NODE_ENV", builder.ApplicationBuilder.Environment.IsDevelopment() ? "development" : "production")
            .WithCertificateTrustConfiguration((ctx) =>
            {
                if (ctx.Scope == CertificateTrustScope.Append)
                {
                    ctx.EnvironmentVariables["NODE_EXTRA_CA_CERTS"] = ctx.CertificateBundlePath;
                }
                else
                {
                    ctx.Arguments.Add("--use-openssl-ca");
                }

                return Task.CompletedTask;
            });
}
