using Aspire.Hosting.ApplicationModel;
using Aspire.Hosting.Eventing;
using CommunityToolkit.Aspire.Hosting.SqlDatabaseProjects;
using Microsoft.Build.Locator;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.SqlServer.Dac;

namespace Aspire.Hosting;

/// <summary>
/// Provides extension methods for adding SQL Server Database Projects to the application.
/// </summary>
public static class SqlProjectBuilderExtensions
{
    /// <summary>
    /// Static constructor to ensure that MSBuild assemblies are properly loaded.
    /// </summary>
    static SqlProjectBuilderExtensions()
    {
        if (!MSBuildLocator.IsRegistered)
        {
            MSBuildLocator.AllowQueryAllDotnetLocations = true;
            MSBuildLocator.RegisterDefaults();
        }
    }

    /// <summary>
    /// Adds a SQL Server Database Project resource to the application based on a referenced MSBuild.Sdk.SqlProj project.
    /// </summary>
    /// <typeparam name="TProject">Type that represents the project that produces the .dacpac file.</typeparam>
    /// <param name="builder">An <see cref="IDistributedApplicationBuilder"/> instance to add the SQL Server Database project to.</param>
    /// <param name="name">Name of the resource.</param>
    /// <returns>An <see cref="IResourceBuilder{T}"/> that can be used to further customize the resource.</returns>
    public static IResourceBuilder<SqlProjectResource> AddSqlProject<TProject>(this IDistributedApplicationBuilder builder, [ResourceName] string name)
        where TProject : IProjectMetadata, new()
    {
        ArgumentNullException.ThrowIfNull(builder, nameof(builder));
        ArgumentNullException.ThrowIfNull(name, nameof(name));

        return builder.AddSqlProject(name)
                      .WithAnnotation(new TProject());
    }

    /// <summary>
    /// Adds a SQL Server Database Project resource to the application.
    /// </summary>
    /// <param name="builder">An <see cref="IDistributedApplicationBuilder"/> instance to add the SQL Server Database project to.</param>
    /// <param name="name">Name of the resource.</param>
    /// <returns>An <see cref="IResourceBuilder{T}"/> that can be used to further customize the resource.</returns>
    public static IResourceBuilder<SqlProjectResource> AddSqlProject(this IDistributedApplicationBuilder builder, [ResourceName] string name)
    {
        ArgumentNullException.ThrowIfNull(builder, nameof(builder));
        ArgumentNullException.ThrowIfNull(name, nameof(name));

        var resource = new SqlProjectResource(name);

        return builder.AddResource(resource)
                      .WithIconName("DatabaseArrowUp")
                      .WithInitialState(new CustomResourceSnapshot
                      {
                          Properties = [],
                          ResourceType = "SqlProject",
                          State = KnownResourceStates.Waiting
                      })
                      .ExcludeFromManifest();
    }

    /// <summary>
    /// Adds a SQL Server Database Project resource to the application based on a referenced NuGet package.
    /// </summary>
    /// <typeparam name="TPackage">Type that represents the NuGet package that contains the .dacpac file.</typeparam>
    /// <param name="builder">An <see cref="IDistributedApplicationBuilder"/> instance to add the SQL Server Database project to.</param>
    /// <param name="name">Name of the resource.</param>
    /// <returns>Am <see cref="IResourceBuilder{T}"/> that can be used to further customize the resource.</returns>
    public static IResourceBuilder<SqlPackageResource<TPackage>> AddSqlPackage<TPackage>(this IDistributedApplicationBuilder builder, [ResourceName] string name)
        where TPackage : IPackageMetadata, new()
    {
        ArgumentNullException.ThrowIfNull(builder, nameof(builder));
        ArgumentNullException.ThrowIfNull(name, nameof(name));

        var resource = new SqlPackageResource<TPackage>(name);

        var packageAnnotation = new TPackage();

        return builder.AddResource(resource)
                      .WithAnnotation(packageAnnotation)
                      .WithIconName("DatabaseArrowUp")
                      .WithInitialState(new CustomResourceSnapshot
                      {
                          Properties = [
                              new("resource.source", $"{packageAnnotation.PackageId}@{packageAnnotation.PackageVersion}"),
                              new("package.id", packageAnnotation.PackageId),
                              new("package.version", packageAnnotation.PackageVersion),
                              new("package.path", packageAnnotation.PackageId)
                              ],
                          ResourceType = "SqlPackage",
                          State = KnownResourceStates.Waiting
                      })
                      .ExcludeFromManifest();
    }

    /// <summary>
    /// Specifies the path to the .dacpac file.
    /// </summary>
    /// <param name="builder">An <see cref="IResourceBuilder{T}"/> representing the SQL Server Database project.</param>
    /// <param name="dacpacPath">Path to the .dacpac file.</param>
    /// <returns>An <see cref="IResourceBuilder{T}"/> that can be used to further customize the resource.</returns>
    public static IResourceBuilder<SqlProjectResource> WithDacpac(this IResourceBuilder<SqlProjectResource> builder, string dacpacPath)
        => InternalWithDacpac(builder, dacpacPath);

    /// <summary>
    /// Specifies the path to the .dacpac file.
    /// </summary>
    /// <param name="builder">An <see cref="IResourceBuilder{T}"/> representing the SQL Server Database project.</param>
    /// <param name="dacpacPath">Path to the .dacpac file.</param>
    /// <returns>An <see cref="IResourceBuilder{T}"/> that can be used to further customize the resource.</returns>
    public static IResourceBuilder<SqlPackageResource<TPackage>> WithDacpac<TPackage>(this IResourceBuilder<SqlPackageResource<TPackage>> builder, string dacpacPath)
        where TPackage : IPackageMetadata => InternalWithDacpac(builder, dacpacPath);


    internal static IResourceBuilder<TResource> InternalWithDacpac<TResource>(this IResourceBuilder<TResource> builder, string dacpacPath)
        where TResource : IResourceWithDacpac
    {
        return builder.WithAnnotation(new DacpacMetadataAnnotation(dacpacPath));
    }

    /// <summary>
    /// Specifies that .dacpac deployment should be skipped if metadata in the target database indicates that the .dacpac has already been deployed in its current state.
    /// </summary>
    /// <param name="builder">An <see cref="IResourceBuilder{T}"/> representing the SQL Server Database project.</param>
    /// <returns>An <see cref="IResourceBuilder{T}"/> that can be used to further customize the resource.</returns>
    public static IResourceBuilder<SqlProjectResource> WithSkipWhenDeployed(this IResourceBuilder<SqlProjectResource> builder)
        => InternalWithSkipWhenDeployed(builder);

    /// <summary>
    /// Specifies that .dacpac deployment should be skipped if metadata in the target database indicates that the .dacpac has already been deployed in its current state.
    /// </summary>
    /// <param name="builder">An <see cref="IResourceBuilder{T}"/> representing the SQL Server Database project.</param>
    /// <returns>An <see cref="IResourceBuilder{T}"/> that can be used to further customize the resource.</returns>
    public static IResourceBuilder<SqlPackageResource<TPackage>> WithSkipWhenDeployed<TPackage>(this IResourceBuilder<SqlPackageResource<TPackage>> builder)
        where TPackage : IPackageMetadata => InternalWithSkipWhenDeployed(builder);


    internal static IResourceBuilder<TResource> InternalWithSkipWhenDeployed<TResource>(this IResourceBuilder<TResource> builder)
        where TResource : IResourceWithDacpac
    {
        return builder.WithAnnotation(new DacpacSkipWhenDeployedAnnotation());
    }

    /// <summary>
    /// Adds a delegate annotation for configuring dacpac deployment options to the <see cref="SqlProjectResource"/>.
    /// </summary>
    /// <param name="builder">An <see cref="IResourceBuilder{T}"/> representing the SQL Server Database project.</param>
    /// <param name="configureDeploymentOptions">The delegate for configuring dacpac deployment options</param>
    /// <returns>An <see cref="IResourceBuilder{T}"/> that can be used to further customize the resource.</returns>
    public static IResourceBuilder<SqlProjectResource> WithConfigureDacDeployOptions(this IResourceBuilder<SqlProjectResource> builder, Action<DacDeployOptions> configureDeploymentOptions)
        => InternalWithConfigureDacDeployOptions(builder, configureDeploymentOptions);

    /// <summary>
    /// Adds a delegate annotation for configuring dacpac deployment options to the <see cref="SqlProjectResource"/>.
    /// </summary>
    /// <param name="builder">An <see cref="IResourceBuilder{T}"/> representing the SQL Server Database project.</param>
    /// <param name="configureDeploymentOptions">The delegate for configuring dacpac deployment options</param>
    /// <returns>An <see cref="IResourceBuilder{T}"/> that can be used to further customize the resource.</returns>
    public static IResourceBuilder<SqlPackageResource<TPackage>> WithConfigureDacDeployOptions<TPackage>(this IResourceBuilder<SqlPackageResource<TPackage>> builder, Action<DacDeployOptions> configureDeploymentOptions)
        where TPackage : IPackageMetadata => InternalWithConfigureDacDeployOptions(builder, configureDeploymentOptions);

    internal static IResourceBuilder<TResource> InternalWithConfigureDacDeployOptions<TResource>(this IResourceBuilder<TResource> builder, Action<DacDeployOptions> configureDeploymentOptions)
        where TResource : IResourceWithDacpac
    {
        ArgumentNullException.ThrowIfNull(builder, nameof(builder));
        ArgumentNullException.ThrowIfNull(configureDeploymentOptions);

        return builder
            .WithAnnotation(new ConfigureDacDeployOptionsAnnotation(configureDeploymentOptions));
    }

    /// <summary>
    /// Adds a path to a publish profile for configuring dacpac deployment options to the <see cref="SqlProjectResource"/>.
    /// </summary>
    /// <param name="builder">An <see cref="IResourceBuilder{T}"/> representing the SQL Server Database project.</param>
    /// <param name="optionsPath">Path to the publish profile xml file</param>
    /// <returns>An <see cref="IResourceBuilder{T}"/> that can be used to further customize the resource.</returns>
    public static IResourceBuilder<SqlProjectResource> WithDacDeployOptions(this IResourceBuilder<SqlProjectResource> builder, string optionsPath)
        => InternalWithDacDeployOptions(builder, optionsPath);

    /// <summary>
    /// Adds a path to a publish profile for configuring dacpac deployment options to the <see cref="SqlProjectResource"/>.
    /// </summary>
    /// <param name="builder">An <see cref="IResourceBuilder{T}"/> representing the SQL Server Database project.</param>
    /// <param name="optionsPath">Path to the publish profile xml file</param>
    /// <returns>An <see cref="IResourceBuilder{T}"/> that can be used to further customize the resource.</returns>
    public static IResourceBuilder<SqlPackageResource<TPackage>> WithDacDeployOptions<TPackage>(this IResourceBuilder<SqlPackageResource<TPackage>> builder, string optionsPath)
        where TPackage : IPackageMetadata => InternalWithDacDeployOptions(builder, optionsPath);

    internal static IResourceBuilder<TResource> InternalWithDacDeployOptions<TResource>(this IResourceBuilder<TResource> builder, string optionsPath)
        where TResource : IResourceWithDacpac
    {
        ArgumentNullException.ThrowIfNull(builder, nameof(builder));
        ArgumentNullException.ThrowIfNull(optionsPath);

        return builder
            .WithAnnotation(new DacDeployOptionsAnnotation(optionsPath));
    }

    /// <summary>
    /// Publishes the SQL Server Database project to the target <see cref="SqlServerDatabaseResource"/>.
    /// </summary>
    /// <param name="builder">An <see cref="IResourceBuilder{T}"/> representing the SQL Server Database project to publish.</param>
    /// <param name="target">An <see cref="IResourceBuilder{T}"/> representing the target <see cref="SqlServerDatabaseResource"/> to publish the SQL Server Database project to.</param>
    /// <returns>An <see cref="IResourceBuilder{T}"/> that can be used to further customize the resource.</returns>
    public static IResourceBuilder<SqlProjectResource> WithReference(
        this IResourceBuilder<SqlProjectResource> builder, IResourceBuilder<SqlServerDatabaseResource> target) => InternalWithReference(builder, target, target.Resource.DatabaseName);

    /// <summary>
    /// Publishes the SQL Server Database project to the target <see cref="IResourceWithConnectionString"/>.
    /// </summary>
    /// <param name="builder">An <see cref="IResourceBuilder{T}"/> representing the SQL Server Database project to publish.</param>
    /// <param name="target">An <see cref="IResourceBuilder{T}"/> representing the target <see cref="IResourceWithConnectionString"/> to publish the SQL Server Database project to.</param>
    /// <returns>An <see cref="IResourceBuilder{T}"/> that can be used to further customize the resource.</returns>
    public static IResourceBuilder<SqlProjectResource> WithReference(
        this IResourceBuilder<SqlProjectResource> builder, IResourceBuilder<IResourceWithConnectionString> target) => InternalWithReference(builder, target);

    /// <summary>
    /// Publishes the SQL Server Database project to the target <see cref="SqlServerDatabaseResource"/>.
    /// </summary>
    /// <param name="builder">An <see cref="IResourceBuilder{T}"/> representing the SQL Server Database project to publish.</param>
    /// <param name="target">An <see cref="IResourceBuilder{T}"/> representing the target <see cref="SqlServerDatabaseResource"/> to publish the SQL Server Database project to.</param>
    /// <returns>An <see cref="IResourceBuilder{T}"/> that can be used to further customize the resource.</returns>
    public static IResourceBuilder<SqlPackageResource<TPackage>> WithReference<TPackage>(
        this IResourceBuilder<SqlPackageResource<TPackage>> builder, IResourceBuilder<SqlServerDatabaseResource> target)
        where TPackage : IPackageMetadata
    {
        return InternalWithReference(builder, target, target.Resource.DatabaseName);
    }

    /// <summary>
    /// Publishes the SQL Server Database project to the target <see cref="IResourceWithConnectionString"/>.
    /// </summary>
    /// <param name="builder">An <see cref="IResourceBuilder{T}"/> representing the SQL Server Database project to publish.</param>
    /// <param name="target">An <see cref="IResourceBuilder{T}"/> representing the target <see cref="IResourceWithConnectionString"/> to publish the SQL Server Database project to.</param>
    /// <returns>An <see cref="IResourceBuilder{T}"/> that can be used to further customize the resource.</returns>
    public static IResourceBuilder<SqlPackageResource<TPackage>> WithReference<TPackage>(
        this IResourceBuilder<SqlPackageResource<TPackage>> builder, IResourceBuilder<IResourceWithConnectionString> target)
        where TPackage : IPackageMetadata
    {
        return InternalWithReference(builder, target);
    }

    internal static IResourceBuilder<TResource> InternalWithReference<TResource>(this IResourceBuilder<TResource> builder, IResourceBuilder<IResourceWithConnectionString> target, string? targetDatabaseName = null)
        where TResource : IResourceWithDacpac
    {
        builder.ApplicationBuilder.Services.TryAddSingleton<IDacpacDeployer, DacpacDeployer>();
        builder.ApplicationBuilder.Services.TryAddSingleton<IDacpacChecksumService, DacpacChecksumService>();
        builder.ApplicationBuilder.Services.TryAddSingleton<SqlProjectPublishService>();

        builder.WithParentRelationship(target.Resource);

        target.OnResourceReady(async (targetResource, evt, ct) =>
        {
            if (builder.Resource.TryGetAnnotationsOfType<ExplicitStartupAnnotation>(out _))
            {
                return;
            }

            await ExecuteResource(builder.Resource, target.Resource, targetDatabaseName, evt.Services, ct);
        });

        var commandOptions = new CommandOptions
        {
            IconName = "ArrowReset",
            IconVariant = IconVariant.Filled,
            IsHighlighted = true,
            Description = "Deploy the SQL Server Database Project to the target database.",
            UpdateState = (context) =>
            {
                if (context.ResourceSnapshot?.State?.Text is string stateText && (KnownResourceStates.TerminalStates.Contains(stateText) || stateText == KnownResourceStates.NotStarted))
                {
                    return ResourceCommandState.Enabled;
                }
                else
                {
                    return ResourceCommandState.Disabled;
                }
            },
        };

        builder.WithCommand("deploy", "Deploy", async (context) =>
        {
            await ExecuteResource(builder.Resource, target.Resource, targetDatabaseName, context.ServiceProvider, context.CancellationToken);
            return new ExecuteCommandResult { Success = true };
        }, commandOptions);

        return builder;
    }


    private static async Task ExecuteResource<TResource>(TResource resource, IResourceWithConnectionString target, string? targetDatabaseName, IServiceProvider serviceProvider, CancellationToken ct)
        where TResource : IResourceWithDacpac
    {
        var eventing = serviceProvider.GetRequiredService<IDistributedApplicationEventing>();
        await eventing.PublishAsync(new BeforeResourceStartedEvent(resource, serviceProvider), ct);

        var service = serviceProvider.GetRequiredService<SqlProjectPublishService>();
        await service.PublishSqlProject(resource, target, targetDatabaseName, ct);
    }
}
