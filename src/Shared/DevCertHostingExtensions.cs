using Aspire.Hosting.ApplicationModel;

namespace Aspire.Hosting;

/// <summary>
/// Extensions for adding Dev Certs to aspire resources.
/// </summary>
public static class DevCertHostingExtensions
{
    /// <summary>
    /// The destination directory for the certificate files in a container.
    /// </summary>
    public const string DEV_CERT_BIND_MOUNT_DEST_DIR = "/dev-certs";

    /// <summary>
    /// The file name of the certificate file.
    /// </summary>
    public const string CERT_FILE_NAME = "dev-cert.pem";

    /// <summary>
    /// The file name of the certificate key file.
    /// </summary>
    public const string CERT_KEY_FILE_NAME = "dev-cert.key";

    /// <summary>
    /// Injects the ASP.NET Core HTTPS developer certificate into the resource via the specified environment variables when
    /// <paramref name="builder"/>.<see cref="IResourceBuilder{T}.ApplicationBuilder">ApplicationBuilder</see>.<see cref="IDistributedApplicationBuilder.ExecutionContext">ExecutionContext</see>.<see cref="DistributedApplicationExecutionContext.IsRunMode">IsRunMode</see><c> == true</c>.<br/>
    /// If the resource is a <see cref="ContainerResource"/>, the certificate files will be provided via WithContainerFiles.
    /// </summary>
    /// <remarks>
    /// This method <strong>does not</strong> configure an HTTPS endpoint on the resource.
    /// Use <see cref="ResourceBuilderExtensions.WithHttpsEndpoint{TResource}"/> to configure an HTTPS endpoint.
    /// </remarks>
    public static IResourceBuilder<TResource> RunWithHttpsDevCertificate<TResource>(
        this IResourceBuilder<TResource> builder, string certFileEnv = "", string certKeyFileEnv = "")
        where TResource : IResourceWithEnvironment, IResourceWithWaitSupport
    {
        if (!builder.ApplicationBuilder.ExecutionContext.IsRunMode)
        {
            return builder;
        }

        if (builder.Resource is not ContainerResource &&
            (!string.IsNullOrEmpty(certFileEnv) || !string.IsNullOrEmpty(certKeyFileEnv)))
        {
            throw new InvalidOperationException("RunWithHttpsDevCertificate needs environment variables only for Resources that aren't Containers.");
        }

        // Create temp directory for certificate export
        var tempDir = Directory.CreateTempSubdirectory("aspire-dev-certs");
        var certExportPath = Path.Combine(tempDir.FullName, "dev-cert.pem");
        var certKeyExportPath = Path.Combine(tempDir.FullName, "dev-cert.key");

        // Create a unique resource name for the certificate export
        var exportResourceName = $"dev-cert-export";

        // Check if we already have a certificate export resource
        var existingResource = builder.ApplicationBuilder.Resources.FirstOrDefault(r => r.Name == exportResourceName);
        IResourceBuilder<ExecutableResource> exportExecutable;

        if (existingResource is null)
        {
            // Create the executable resource to export the certificate
            exportExecutable = builder.ApplicationBuilder
                .AddExecutable(exportResourceName, "dotnet", tempDir.FullName)
                .WithEnvironment("DOTNET_CLI_UI_LANGUAGE", "en") // Ensure consistent output language
                .WithArgs(context =>
                {
                    context.Args.Add("dev-certs");
                    context.Args.Add("https");
                    context.Args.Add("--export-path");
                    context.Args.Add(certExportPath);
                    context.Args.Add("--format");
                    context.Args.Add("Pem");
                    context.Args.Add("--no-password");
                });
        }
        else
        {
            exportExecutable = builder.ApplicationBuilder.CreateResourceBuilder((ExecutableResource)existingResource);
        }

        builder.WaitForCompletion(exportExecutable);

        // Configure the current resource with the certificate paths
        if (builder.Resource is ContainerResource containerResource)
        {
            var certFileDest = $"{DEV_CERT_BIND_MOUNT_DEST_DIR}/{CERT_FILE_NAME}";
            var certKeyFileDest = $"{DEV_CERT_BIND_MOUNT_DEST_DIR}/{CERT_KEY_FILE_NAME}";

            if (!containerResource.TryGetContainerMounts(out var mounts) &&
                mounts is not null &&
                mounts.Any(cm => cm.Target == DEV_CERT_BIND_MOUNT_DEST_DIR))
            {
                return builder;
            }

            // Use WithContainerFiles to provide the certificate files to the container
            builder.ApplicationBuilder.CreateResourceBuilder(containerResource)
                .WithContainerFiles(DEV_CERT_BIND_MOUNT_DEST_DIR, (context, cancellationToken) =>
                {
                    var files = new List<ContainerFile>();

                    // Check if certificate files exist before adding them
                    if (File.Exists(certExportPath))
                    {
                        files.Add(new ContainerFile
                        {
                            Name = CERT_FILE_NAME,
                            SourcePath = certExportPath
                        });
                    }

                    if (File.Exists(certKeyExportPath))
                    {
                        files.Add(new ContainerFile
                        {
                            Name = CERT_KEY_FILE_NAME,
                            SourcePath = certKeyExportPath
                        });
                    }

                    return Task.FromResult(files.AsEnumerable<ContainerFileSystemItem>());
                });

            if (!string.IsNullOrEmpty(certFileEnv))
            {
                builder.WithEnvironment(certFileEnv, certFileDest);
            }
            if (!string.IsNullOrEmpty(certKeyFileEnv))
            {
                builder.WithEnvironment(certKeyFileEnv, certKeyFileDest);
            }
        }
        else
        {
            // For non-container resources, set the file paths directly
            if (!string.IsNullOrEmpty(certFileEnv))
            {
                builder.WithEnvironment(certFileEnv, certExportPath);
            }

            if (!string.IsNullOrEmpty(certKeyFileEnv))
            {
                builder.WithEnvironment(certKeyFileEnv, certKeyExportPath);
            }
        }

        return builder;
    }
}