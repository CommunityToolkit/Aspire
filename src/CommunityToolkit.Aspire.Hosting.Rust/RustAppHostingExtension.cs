using Aspire.Hosting.ApplicationModel;
using CommunityToolkit.Aspire.Utils;

namespace Aspire.Hosting;

/// <summary>
/// Provides extension methods for adding Rust applications to an <see cref="IDistributedApplicationBuilder"/>.
/// </summary>
public static class RustAppHostingExtension
{
    /// <summary>
    /// Adds a Rust application to the application model. Executes the Rust app using <c>cargo run</c>.
    /// </summary>
    /// <param name="builder">The <see cref="IDistributedApplicationBuilder"/> to add the resource to.</param>
    /// <param name="name">The name of the resource.</param>
    /// <param name="workingDirectory">The working directory to use for the command.</param>
    /// <param name="args">The optional arguments to be passed to the executable when it is started.</param>
    /// <returns>A reference to the <see cref="IResourceBuilder{T}"/>.</returns>
    public static IResourceBuilder<RustAppExecutableResource> AddRustApp(this IDistributedApplicationBuilder builder, [ResourceName] string name, string workingDirectory, string[]? args = null)
    {
        ArgumentNullException.ThrowIfNull(builder, nameof(builder));
        ArgumentException.ThrowIfNullOrWhiteSpace(name, nameof(name));
        ArgumentException.ThrowIfNullOrWhiteSpace(workingDirectory, nameof(workingDirectory));

        string[] allArgs = args is { Length: > 0 }
            ? ["run", .. args]
            : ["run"];

        workingDirectory = Path.Combine(builder.AppHostDirectory, workingDirectory).NormalizePathForCurrentPlatform();
        var resource = new RustAppExecutableResource(name, workingDirectory);

        return builder.AddResource(resource)
                      .WithRustDefaults()
                      .WithArgs(allArgs)
                      .PublishAsDockerFile();
    }

    /// <summary>
    /// Replaces the default <c>cargo run</c> command with an alternative Rust build tool command.
    /// Use this to integrate tools like <c>trunk</c>, <c>cargo-leptos</c>, or other Rust build utilities.
    /// </summary>
    /// <param name="builder">The resource builder for the Rust app.</param>
    /// <param name="command">The command to execute (e.g., "trunk", "cargo-leptos").</param>
    /// <param name="args">Arguments to pass to the command, replacing any previously configured arguments.</param>
    /// <returns>A reference to the <see cref="IResourceBuilder{T}"/>.</returns>
    public static IResourceBuilder<RustAppExecutableResource> WithCargoCommand(
        this IResourceBuilder<RustAppExecutableResource> builder,
        string command,
        params string[] args)
    {
        ArgumentNullException.ThrowIfNull(builder, nameof(builder));
        ArgumentException.ThrowIfNullOrWhiteSpace(command, nameof(command));

        builder.WithCommand(command);

        builder.WithArgs(context =>
        {
            context.Args.Clear();
            foreach (var arg in args)
            {
                context.Args.Add(arg);
            }
        });

        return builder;
    }

    /// <summary>
    /// Installs a Rust tool using <c>cargo install</c> or <c>cargo binstall</c> before the application starts.
    /// Creates a <see cref="RustToolInstallerResource"/> that runs before the Rust application.
    /// </summary>
    /// <param name="builder">The resource builder for the Rust app.</param>
    /// <param name="packageName">The name of the cargo package to install (e.g., "trunk", "cargo-leptos").</param>
    /// <param name="version">The version of the package to install. If <see langword="null"/>, the latest version is used.</param>
    /// <param name="binstall">When true, uses <c>cargo binstall</c> instead of <c>cargo install</c>.</param>
    /// <param name="locked">Whether to pass the <c>--locked</c> flag to the install command.</param>
    /// <param name="features">Optional features to enable when installing the package.</param>
    /// <returns>A reference to the <see cref="IResourceBuilder{T}"/>.</returns>
    public static IResourceBuilder<RustAppExecutableResource> WithCargoInstall(
        this IResourceBuilder<RustAppExecutableResource> builder,
        string packageName,
        string? version = null,
        bool binstall = false,
        bool locked = false,
        string[]? features = null)
    {
        ArgumentNullException.ThrowIfNull(builder, nameof(builder));
        ArgumentException.ThrowIfNullOrWhiteSpace(packageName, nameof(packageName));

        if (!builder.ApplicationBuilder.ExecutionContext.IsPublishMode)
        {
            var installerName = $"{builder.Resource.Name}-cargo-install-{packageName}";
            var installer = new RustToolInstallerResource(installerName, builder.Resource.WorkingDirectory);

            List<string> installArgs = binstall ? ["binstall", "-y"] : ["install"];

            if (version is not null)
            {
                installArgs.Add("--version");
                installArgs.Add(version);
            }

            if (locked)
            {
                installArgs.Add("--locked");
            }

            if (features is { Length: > 0 })
            {
                installArgs.Add("--features");
                installArgs.Add(string.Join(",", features));
            }

            installArgs.Add(packageName);

            var installerBuilder = builder.ApplicationBuilder.AddResource(installer)
                .WithArgs([.. installArgs])
                .WithParentRelationship(builder.Resource)
                .ExcludeFromManifest();

            builder.WaitForCompletion(installerBuilder);
        }

        return builder;
    }

    private static IResourceBuilder<RustAppExecutableResource> WithRustDefaults(
        this IResourceBuilder<RustAppExecutableResource> builder) =>
        builder.WithOtlpExporter();
}
