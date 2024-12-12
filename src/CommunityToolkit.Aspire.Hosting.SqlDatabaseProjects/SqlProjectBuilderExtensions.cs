using Aspire.Hosting.ApplicationModel;
using Microsoft.Build.Locator;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using CommunityToolkit.Aspire.Hosting.SqlDatabaseProjects;
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
    public static IResourceBuilder<SqlProjectResource> AddSqlProject<TProject>(this IDistributedApplicationBuilder builder, [ResourceName]string name)
        where TProject : IProjectMetadata, new()
    {
        ArgumentNullException.ThrowIfNull(builder, nameof(builder));
        ArgumentNullException.ThrowIfNull(name, nameof(name));

        return builder.AddSqlProject(name)
                      .WithAnnotation(new TProject());
    }

    /// <summary>
    /// 
    /// </summary>
    /// <typeparam name="TPackage"></typeparam>
    /// <param name="builder"></param>
    /// <param name="name"></param>
    /// <param name="relativePathToDacpac"></param>
    /// <returns></returns>
    public static IResourceBuilder<SqlProjectResource> AddSqlPackage<TPackage>(this IDistributedApplicationBuilder builder, [ResourceName]string name, string relativePathToDacpac)
        where TPackage : IPackageMetadata, new()
    {
        ArgumentNullException.ThrowIfNull(builder, nameof(builder));
        ArgumentNullException.ThrowIfNull(name, nameof(name));

        return builder.AddSqlProject(name)
                      .WithAnnotation(new TPackage())
                      .WithAnnotation(new DacpacMetadataAnnotation(relativePathToDacpac));
    }

    /// <summary>
    /// Adds a SQL Server Database Project resource to the application.
    /// </summary>
    /// <param name="builder">An <see cref="IDistributedApplicationBuilder"/> instance to add the SQL Server Database project to.</param>
    /// <param name="name">Name of the resource.</param>
    /// <returns>An <see cref="IResourceBuilder{T}"/> that can be used to further customize the resource.</returns>
    public static IResourceBuilder<SqlProjectResource> AddSqlProject(this IDistributedApplicationBuilder builder, [ResourceName]string name)
    {
        ArgumentNullException.ThrowIfNull(builder, nameof(builder));
        ArgumentNullException.ThrowIfNull(name, nameof(name));

        var resource = new SqlProjectResource(name);
        
        return builder.AddResource(resource)
                      .ExcludeFromManifest();
    }

    /// <summary>
    /// Specifies the path to the .dacpac file.
    /// </summary>
    /// <param name="builder">An <see cref="IResourceBuilder{T}"/> representing the SQL Server Database project.</param>
    /// <param name="dacpacPath">Path to the .dacpac file.</param>
    /// <returns>An <see cref="IResourceBuilder{T}"/> that can be used to further customize the resource.</returns>
    public static IResourceBuilder<SqlProjectResource> WithDacpac(this IResourceBuilder<SqlProjectResource> builder, string dacpacPath)
    {
        if (!Path.IsPathRooted(dacpacPath))
        {
            dacpacPath = Path.Combine(builder.ApplicationBuilder.AppHostDirectory, dacpacPath);
        }

        return builder.WithAnnotation(new DacpacMetadataAnnotation(dacpacPath));
    }

    /// <summary>
    /// Adds a delegate annotation for configuring dacpac deployment options to the <see cref="SqlProjectResource"/>.
    /// </summary>
    /// <param name="builder">An <see cref="IResourceBuilder{T}"/> representing the SQL Server Database project.</param>
    /// <param name="configureDeploymentOptions">The delegate for configuring dacpac deployment options</param>
    /// <returns>An <see cref="IResourceBuilder{T}"/> that can be used to further customize the resource.</returns>
    public static IResourceBuilder<SqlProjectResource> WithConfigureDacDeployOptions(this IResourceBuilder<SqlProjectResource> builder, Action<DacDeployOptions> configureDeploymentOptions)
    {
        ArgumentNullException.ThrowIfNull(builder, nameof(builder));
        ArgumentNullException.ThrowIfNull(configureDeploymentOptions);

        return builder
            .WithAnnotation(new ConfigureDacDeployOptionsAnnotation(configureDeploymentOptions));
    }

    /// <summary>
    /// Publishes the SQL Server Database project to the target <see cref="SqlServerDatabaseResource"/>.
    /// </summary>
    /// <param name="builder">An <see cref="IResourceBuilder{T}"/> representing the SQL Server Database project to publish.</param>
    /// <param name="target">An <see cref="IResourceBuilder{T}"/> representing the target <see cref="SqlServerDatabaseResource"/> to publish the SQL Server Database project to.</param>
    /// <returns>An <see cref="IResourceBuilder{T}"/> that can be used to further customize the resource.</returns>
    public static IResourceBuilder<SqlProjectResource> WithReference(
        this IResourceBuilder<SqlProjectResource> builder, IResourceBuilder<SqlServerDatabaseResource> target)
    {
        builder.ApplicationBuilder.Services.TryAddSingleton<IDacpacDeployer, DacpacDeployer>();
        builder.ApplicationBuilder.Services.TryAddSingleton<SqlProjectPublishService>();

        builder.ApplicationBuilder.Eventing.Subscribe<ResourceReadyEvent>(target.Resource, (resourceReady, ct) =>
        {
            var service = resourceReady.Services.GetRequiredService<SqlProjectPublishService>();
            return service.PublishSqlProject(builder.Resource, target.Resource, ct);
        });

        builder.WaitFor(target);

        builder.WithInitialState(new CustomResourceSnapshot
        {
            Properties = [],
            ResourceType = "SqlProject",
            State = new ResourceStateSnapshot("Pending", KnownResourceStateStyles.Info)
        });

        builder.WithCommand("redeploy", "Redeploy", async (context) =>
        {
            var service = context.ServiceProvider.GetRequiredService<SqlProjectPublishService>();
            await service.PublishSqlProject(builder.Resource, target.Resource, context.CancellationToken);
            return new ExecuteCommandResult { Success = true };
        }, updateState: (context) => context.ResourceSnapshot?.State?.Text == KnownResourceStates.Finished ? ResourceCommandState.Enabled : ResourceCommandState.Disabled,
           displayDescription: "Redeploys the SQL Server Database project to the target database.",
           iconName: "ArrowReset",
           iconVariant: IconVariant.Filled,
           isHighlighted: true);

        return builder;
    }
}
