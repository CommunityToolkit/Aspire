#pragma warning disable ASPIREPIPELINES001

using Aspire.Hosting.ApplicationModel;
using Aspire.Hosting.Pipelines;
using CommunityToolkit.Aspire.Hosting.Bitwarden.SecretManager;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Aspire.Hosting;

/// <summary>
/// Extension methods for adding Bitwarden Secrets Manager resources.
/// </summary>
public static class BitwardenSecretManagerExtensions
{
    /// <summary>
    /// Adds a Bitwarden Secrets Manager resource with a fixed project name and fixed organization identifier.
    /// </summary>
    /// <param name="builder">The distributed application builder.</param>
    /// <param name="name">The resource name.</param>
    /// <param name="projectName">The required remote Bitwarden project name.</param>
    /// <param name="organizationId">The Bitwarden organization identifier.</param>
    /// <param name="accessToken">The access token parameter used to manage the Bitwarden project and managed secrets.</param>
    /// <returns>The resource builder.</returns>
    public static IResourceBuilder<BitwardenSecretManagerResource> AddBitwardenSecretManager(
        this IDistributedApplicationBuilder builder,
        [ResourceName] string name,
        string projectName,
        Guid organizationId,
        IResourceBuilder<ParameterResource> accessToken)
    {
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        ArgumentException.ThrowIfNullOrWhiteSpace(projectName);
        ArgumentNullException.ThrowIfNull(accessToken);

        return AddBitwardenSecretManagerCore(
            builder,
            name,
            ConfiguredStringValue.FromLiteral(projectName),
            ConfiguredGuidValue.FromLiteral(organizationId),
            accessToken);
    }

    /// <summary>
    /// Adds a Bitwarden Secrets Manager resource with a parameter-backed project name and fixed organization identifier.
    /// </summary>
    /// <param name="builder">The distributed application builder.</param>
    /// <param name="name">The resource name.</param>
    /// <param name="projectName">The parameter that resolves to the required remote Bitwarden project name.</param>
    /// <param name="organizationId">The Bitwarden organization identifier.</param>
    /// <param name="accessToken">The access token parameter used to manage the Bitwarden project and managed secrets.</param>
    /// <returns>The resource builder.</returns>
    public static IResourceBuilder<BitwardenSecretManagerResource> AddBitwardenSecretManager(
        this IDistributedApplicationBuilder builder,
        [ResourceName] string name,
        IResourceBuilder<ParameterResource> projectName,
        Guid organizationId,
        IResourceBuilder<ParameterResource> accessToken)
    {
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        ArgumentNullException.ThrowIfNull(projectName);
        ArgumentNullException.ThrowIfNull(accessToken);

        return AddBitwardenSecretManagerCore(
            builder,
            name,
            ConfiguredStringValue.FromParameter(projectName.Resource),
            ConfiguredGuidValue.FromLiteral(organizationId),
            accessToken);
    }

    /// <summary>
    /// Adds a Bitwarden Secrets Manager resource with a fixed project name and parameter-backed organization identifier.
    /// </summary>
    /// <param name="builder">The distributed application builder.</param>
    /// <param name="name">The resource name.</param>
    /// <param name="projectName">The required remote Bitwarden project name.</param>
    /// <param name="organizationId">The parameter that resolves to the Bitwarden organization identifier.</param>
    /// <param name="accessToken">The access token parameter used to manage the Bitwarden project and managed secrets.</param>
    /// <returns>The resource builder.</returns>
    public static IResourceBuilder<BitwardenSecretManagerResource> AddBitwardenSecretManager(
        this IDistributedApplicationBuilder builder,
        [ResourceName] string name,
        string projectName,
        IResourceBuilder<ParameterResource> organizationId,
        IResourceBuilder<ParameterResource> accessToken)
    {
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        ArgumentException.ThrowIfNullOrWhiteSpace(projectName);
        ArgumentNullException.ThrowIfNull(organizationId);
        ArgumentNullException.ThrowIfNull(accessToken);

        return AddBitwardenSecretManagerCore(
            builder,
            name,
            ConfiguredStringValue.FromLiteral(projectName),
            ConfiguredGuidValue.FromParameter(organizationId.Resource),
            accessToken);
    }

    /// <summary>
    /// Adds a Bitwarden Secrets Manager resource with parameter-backed project and organization identifiers.
    /// </summary>
    /// <param name="builder">The distributed application builder.</param>
    /// <param name="name">The resource name.</param>
    /// <param name="projectName">The parameter that resolves to the required remote Bitwarden project name.</param>
    /// <param name="organizationId">The parameter that resolves to the Bitwarden organization identifier.</param>
    /// <param name="accessToken">The access token parameter used to manage the Bitwarden project and managed secrets.</param>
    /// <returns>The resource builder.</returns>
    public static IResourceBuilder<BitwardenSecretManagerResource> AddBitwardenSecretManager(
        this IDistributedApplicationBuilder builder,
        [ResourceName] string name,
        IResourceBuilder<ParameterResource> projectName,
        IResourceBuilder<ParameterResource> organizationId,
        IResourceBuilder<ParameterResource> accessToken)
    {
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        ArgumentNullException.ThrowIfNull(projectName);
        ArgumentNullException.ThrowIfNull(organizationId);
        ArgumentNullException.ThrowIfNull(accessToken);

        return AddBitwardenSecretManagerCore(
            builder,
            name,
            ConfiguredStringValue.FromParameter(projectName.Resource),
            ConfiguredGuidValue.FromParameter(organizationId.Resource),
            accessToken);
    }

    /// <summary>
    /// Configures the resource to adopt an existing Bitwarden project.
    /// </summary>
    /// <param name="builder">The resource builder.</param>
    /// <param name="projectId">The Bitwarden project identifier.</param>
    /// <returns>The resource builder.</returns>
    public static IResourceBuilder<BitwardenSecretManagerResource> WithExistingProject(
        this IResourceBuilder<BitwardenSecretManagerResource> builder,
        Guid projectId)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.Resource.ExistingProjectId = projectId;
        return builder;
    }

    /// <summary>
    /// Overrides the Bitwarden API URL.
    /// </summary>
    /// <param name="builder">The resource builder.</param>
    /// <param name="apiUrl">The absolute Bitwarden API URL.</param>
    /// <returns>The resource builder.</returns>
    public static IResourceBuilder<BitwardenSecretManagerResource> WithApiUrl(
        this IResourceBuilder<BitwardenSecretManagerResource> builder,
        string apiUrl)
    {
        ArgumentNullException.ThrowIfNull(builder);
        ValidateAbsoluteUri(apiUrl, nameof(apiUrl));

        builder.Resource.ApiUrl = apiUrl;
        return builder;
    }

    /// <summary>
    /// Overrides the Bitwarden identity URL.
    /// </summary>
    /// <param name="builder">The resource builder.</param>
    /// <param name="identityUrl">The absolute Bitwarden identity URL.</param>
    /// <returns>The resource builder.</returns>
    public static IResourceBuilder<BitwardenSecretManagerResource> WithIdentityUrl(
        this IResourceBuilder<BitwardenSecretManagerResource> builder,
        string identityUrl)
    {
        ArgumentNullException.ThrowIfNull(builder);
        ValidateAbsoluteUri(identityUrl, nameof(identityUrl));

        builder.Resource.IdentityUrl = identityUrl;
        return builder;
    }

    /// <summary>
    /// Overrides the reconciliation state file path.
    /// </summary>
    /// <param name="builder">The resource builder.</param>
    /// <param name="stateFile">The state file path, relative to the Aspire store directory when not rooted.</param>
    /// <returns>The resource builder.</returns>
    public static IResourceBuilder<BitwardenSecretManagerResource> WithStateFile(
        this IResourceBuilder<BitwardenSecretManagerResource> builder,
        string stateFile)
    {
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentException.ThrowIfNullOrWhiteSpace(stateFile);

        builder.Resource.StateFile = stateFile;

        return builder;
    }

    /// <summary>
    /// Overrides the Bitwarden SDK auth state file path.
    /// </summary>
    /// <param name="builder">The resource builder.</param>
    /// <param name="authStateFile">The auth state file path, relative to the Aspire store directory when not rooted.</param>
    /// <returns>The resource builder.</returns>
    public static IResourceBuilder<BitwardenSecretManagerResource> WithAuthStateFile(
        this IResourceBuilder<BitwardenSecretManagerResource> builder,
        string authStateFile)
    {
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentException.ThrowIfNullOrWhiteSpace(authStateFile);

        builder.Resource.AuthStateFile = authStateFile;

        return builder;
    }

    /// <summary>
    /// Overrides the Bitwarden SDK auth state file path using a parameter.
    /// When the parameter has no configured value the path falls back to the Aspire store default.
    /// </summary>
    /// <param name="builder">The resource builder.</param>
    /// <param name="authStateFile">
    /// A parameter whose value is the auth state file path.
    /// Relative paths are resolved against the Aspire store directory.
    /// </param>
    /// <returns>The resource builder.</returns>
    public static IResourceBuilder<BitwardenSecretManagerResource> WithAuthStateFile(
        this IResourceBuilder<BitwardenSecretManagerResource> builder,
        IResourceBuilder<ParameterResource> authStateFile)
    {
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentNullException.ThrowIfNull(authStateFile);

        builder.Resource.AuthStateFileParameter = authStateFile.Resource;

        return builder;
    }

    /// <summary>
    /// Overrides the runtime access token injected into dependents by <see cref="WithReference{TDestination}(IResourceBuilder{TDestination}, IResourceBuilder{BitwardenSecretManagerResource}, string?)"/>.
    /// </summary>
    /// <param name="builder">The resource builder.</param>
    /// <param name="accessToken">The runtime access token parameter.</param>
    /// <returns>The resource builder.</returns>
    public static IResourceBuilder<BitwardenSecretManagerResource> WithRuntimeAccessToken(
        this IResourceBuilder<BitwardenSecretManagerResource> builder,
        IResourceBuilder<ParameterResource> accessToken)
    {
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentNullException.ThrowIfNull(accessToken);

        builder.Resource.RuntimeAccessToken = accessToken.Resource;
        return builder;
    }

    /// <summary>
    /// Gets a Bitwarden secret reference by remote name.
    /// </summary>
    /// <param name="builder">The resource builder.</param>
    /// <param name="remoteName">The Bitwarden secret name.</param>
    /// <returns>A Bitwarden secret reference.</returns>
    public static IBitwardenSecretReference GetSecret(
        this IResourceBuilder<BitwardenSecretManagerResource> builder,
        string remoteName)
    {
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentException.ThrowIfNullOrWhiteSpace(remoteName);
        return builder.Resource.GetSecret(remoteName);
    }

    /// <summary>
    /// Gets a Bitwarden secret reference by secret identifier.
    /// </summary>
    /// <param name="builder">The resource builder.</param>
    /// <param name="secretId">The Bitwarden secret identifier.</param>
    /// <returns>A Bitwarden secret reference.</returns>
    public static IBitwardenSecretReference GetSecret(
        this IResourceBuilder<BitwardenSecretManagerResource> builder,
        Guid secretId)
    {
        ArgumentNullException.ThrowIfNull(builder);
        return builder.Resource.GetSecret(secretId);
    }

    /// <summary>
    /// Adds a managed Bitwarden secret whose local and remote names are the same.
    /// </summary>
    /// <param name="builder">The parent Bitwarden resource builder.</param>
    /// <param name="name">The Aspire resource name and Bitwarden secret name.</param>
    /// <param name="value">The secret value parameter.</param>
    /// <returns>The managed secret resource builder.</returns>
    public static IResourceBuilder<BitwardenSecretResource> AddSecret(
        this IResourceBuilder<BitwardenSecretManagerResource> builder,
        [ResourceName] string name,
        IResourceBuilder<ParameterResource> value)
    {
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        ArgumentNullException.ThrowIfNull(value);
        return builder.AddSecret(name, name, value);
    }

    /// <summary>
    /// Adds a managed Bitwarden secret whose local and remote names are the same.
    /// </summary>
    /// <param name="builder">The parent Bitwarden resource builder.</param>
    /// <param name="name">The Aspire resource name and Bitwarden secret name.</param>
    /// <param name="value">The secret value expression.</param>
    /// <returns>The managed secret resource builder.</returns>
    public static IResourceBuilder<BitwardenSecretResource> AddSecret(
        this IResourceBuilder<BitwardenSecretManagerResource> builder,
        [ResourceName] string name,
        ReferenceExpression value)
    {
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        ArgumentNullException.ThrowIfNull(value);
        return builder.AddSecret(name, name, value);
    }

    /// <summary>
    /// Adds a managed Bitwarden secret with distinct Aspire and remote names.
    /// </summary>
    /// <param name="builder">The parent Bitwarden resource builder.</param>
    /// <param name="name">The Aspire resource name.</param>
    /// <param name="remoteName">The Bitwarden secret name.</param>
    /// <param name="value">The secret value parameter.</param>
    /// <returns>The managed secret resource builder.</returns>
    public static IResourceBuilder<BitwardenSecretResource> AddSecret(
        this IResourceBuilder<BitwardenSecretManagerResource> builder,
        [ResourceName] string name,
        string remoteName,
        IResourceBuilder<ParameterResource> value)
    {
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        ArgumentException.ThrowIfNullOrWhiteSpace(remoteName);
        ArgumentNullException.ThrowIfNull(value);
        return AddSecretCore(builder, name, remoteName, value.Resource);
    }

    /// <summary>
    /// Adds a managed Bitwarden secret with distinct Aspire and remote names.
    /// </summary>
    /// <param name="builder">The parent Bitwarden resource builder.</param>
    /// <param name="name">The Aspire resource name.</param>
    /// <param name="remoteName">The Bitwarden secret name.</param>
    /// <param name="value">The secret value expression.</param>
    /// <returns>The managed secret resource builder.</returns>
    public static IResourceBuilder<BitwardenSecretResource> AddSecret(
        this IResourceBuilder<BitwardenSecretManagerResource> builder,
        [ResourceName] string name,
        string remoteName,
        ReferenceExpression value)
    {
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        ArgumentException.ThrowIfNullOrWhiteSpace(remoteName);
        ArgumentNullException.ThrowIfNull(value);
        return AddSecretCore(builder, name, remoteName, value);
    }

    /// <summary>
    /// Configures a managed Bitwarden secret to adopt an existing remote secret.
    /// </summary>
    /// <param name="builder">The managed secret resource builder.</param>
    /// <param name="secretId">The Bitwarden secret identifier.</param>
    /// <returns>The managed secret resource builder.</returns>
    public static IResourceBuilder<BitwardenSecretResource> WithExistingSecret(
        this IResourceBuilder<BitwardenSecretResource> builder,
        Guid secretId)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.Resource.ExistingSecretId = secretId;
        return builder;
    }

    /// <summary>
    /// Injects structured Bitwarden client configuration into the destination resource.
    /// </summary>
    /// <typeparam name="TDestination">The destination resource type.</typeparam>
    /// <param name="builder">The destination resource builder.</param>
    /// <param name="source">The Bitwarden resource builder.</param>
    /// <param name="connectionName">The logical connection name. Defaults to the Bitwarden resource name.</param>
    /// <returns>The destination resource builder.</returns>
    public static IResourceBuilder<TDestination> WithReference<TDestination>(
        this IResourceBuilder<TDestination> builder,
        IResourceBuilder<BitwardenSecretManagerResource> source,
        string? connectionName = null)
        where TDestination : IResourceWithEnvironment
    {
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentNullException.ThrowIfNull(source);

        if (connectionName is not null)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(connectionName);
        }

        connectionName ??= source.Resource.Name;

        builder.WithReferenceRelationship(source);

        if (builder.Resource is IResourceWithWaitSupport waitResource)
        {
            builder.ApplicationBuilder.CreateResourceBuilder(waitResource).WaitFor(source);
        }

        return builder.WithEnvironment(context => source.Resource.ApplyReferenceConfiguration(context.EnvironmentVariables, connectionName));
    }

    /// <summary>
    /// Injects a Bitwarden secret value into a destination environment variable.
    /// </summary>
    /// <typeparam name="TDestination">The destination resource type.</typeparam>
    /// <param name="builder">The destination resource builder.</param>
    /// <param name="environmentVariableName">The destination environment variable name.</param>
    /// <param name="secretReference">The Bitwarden secret reference.</param>
    /// <returns>The destination resource builder.</returns>
    public static IResourceBuilder<TDestination> WithBitwardenSecretValue<TDestination>(
        this IResourceBuilder<TDestination> builder,
        string environmentVariableName,
        IBitwardenSecretReference secretReference)
        where TDestination : IResourceWithEnvironment
    {
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentException.ThrowIfNullOrWhiteSpace(environmentVariableName);
        ArgumentNullException.ThrowIfNull(secretReference);

        AttachSecretDependencies(builder, secretReference);

        return builder.WithEnvironment(environmentVariableName, secretReference);
    }

    /// <summary>
    /// Injects a Bitwarden secret identifier into a destination environment variable.
    /// </summary>
    /// <typeparam name="TDestination">The destination resource type.</typeparam>
    /// <param name="builder">The destination resource builder.</param>
    /// <param name="environmentVariableName">The destination environment variable name.</param>
    /// <param name="secretReference">The Bitwarden secret reference.</param>
    /// <returns>The destination resource builder.</returns>
    public static IResourceBuilder<TDestination> WithBitwardenSecretId<TDestination>(
        this IResourceBuilder<TDestination> builder,
        string environmentVariableName,
        IBitwardenSecretReference secretReference)
        where TDestination : IResourceWithEnvironment
    {
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentException.ThrowIfNullOrWhiteSpace(environmentVariableName);
        ArgumentNullException.ThrowIfNull(secretReference);

        AttachSecretDependencies(builder, secretReference);

        return builder.WithEnvironment(environmentVariableName, new BitwardenSecretIdExpression(secretReference));
    }

    private static IResourceBuilder<BitwardenSecretManagerResource> AddBitwardenSecretManagerCore(
        IDistributedApplicationBuilder builder,
        string name,
        ConfiguredStringValue projectName,
        ConfiguredGuidValue organizationId,
        IResourceBuilder<ParameterResource> accessToken)
    {
        // Keep the public overloads explicit, but normalize their implementation here.
        BitwardenSecretManagerResource resource = new(
            name,
            projectName,
            organizationId,
            accessToken.Resource,
            builder.AppHostDirectory);
        return ConfigureBitwardenSecretManager(builder.AddResource(resource));
    }

    private static IResourceBuilder<BitwardenSecretManagerResource> ConfigureBitwardenSecretManager(
        IResourceBuilder<BitwardenSecretManagerResource> builder)
    {
        bool isPublishMode = builder.ApplicationBuilder.ExecutionContext.IsPublishMode;

        builder.ApplicationBuilder.Services.TryAddSingleton<IBitwardenSecretManagerProviderFactory, BitwardenSecretManagerProviderFactory>();
        builder.ApplicationBuilder.Services.TryAddSingleton<BitwardenStateStore>();
        builder.ApplicationBuilder.Services.TryAddSingleton<BitwardenSecretManagerReconciler>();

        builder.WithPipelineStepFactory(
            $"bitwarden-{builder.Resource.Name}-reconcile",
            async context => await BitwardenSecretManagerDeploymentStep.ExecuteAsync(context, builder.Resource.Name).ConfigureAwait(false),
            dependsOn: [WellKnownPipelineSteps.DeployPrereq],
            requiredBy: [WellKnownPipelineSteps.Deploy],
            tags: [WellKnownPipelineTags.ProvisionInfrastructure]
        );

        var resourceBuilder = builder.WithInitialState(new CustomResourceSnapshot
        {
            ResourceType = "BitwardenSecretManager",
            State = KnownResourceStates.NotStarted,
            Properties = [new("RemoteProjectName", builder.Resource.GetProjectNameDisplayValue())]
        });

        // Only register startup reconciliation in non-publish mode;
        // in publish mode, the publishing step handles reconciliation
        if (!isPublishMode)
        {
            resourceBuilder.OnInitializeResource(async (resource, eventContext, cancellationToken) =>
            {
                await eventContext.Notifications.PublishUpdateAsync(resource, state => state with
                {
                    State = KnownResourceStates.Starting,
                    Properties = [new("RemoteProjectName", resource.GetProjectNameDisplayValue())]
                }).ConfigureAwait(false);

                await eventContext.Eventing.PublishAsync(new BeforeResourceStartedEvent(resource, eventContext.Services), cancellationToken).ConfigureAwait(false);

                try
                {
                    BitwardenSecretManagerReconciler reconciler = eventContext.Services.GetRequiredService<BitwardenSecretManagerReconciler>();
                    BitwardenReconciliationResult result = await reconciler.InitializeAsync(resource, eventContext.Services, eventContext.Logger, cancellationToken).ConfigureAwait(false);

                    await eventContext.Notifications.PublishUpdateAsync(resource, state => state with
                    {
                        State = new ResourceStateSnapshot(KnownResourceStates.Running, KnownResourceStateStyles.Success),
                        StartTimeStamp = DateTime.UtcNow,
                        Properties =
                        [
                            new("RemoteProjectName", resource.GetProjectNameDisplayValue()),
                            new("ProjectId", result.ProjectId.ToString("D")),
                            new("StateFile", result.StateFile)
                        ]
                    }).ConfigureAwait(false);
                }
                catch (Exception ex)
                {
                    await eventContext.Notifications.PublishUpdateAsync(resource, state => state with
                    {
                        State = new ResourceStateSnapshot(KnownResourceStates.FailedToStart, KnownResourceStateStyles.Error),
                        Properties =
                        [
                            new("RemoteProjectName", resource.GetProjectNameDisplayValue()),
                            new("Error", ex.Message)
                        ]
                    }).ConfigureAwait(false);

                    throw;
                }
            });
        }

        return resourceBuilder;
    }

    private static IResourceBuilder<BitwardenSecretResource> AddSecretCore(
        IResourceBuilder<BitwardenSecretManagerResource> builder,
        string name,
        string remoteName,
        object value)
    {
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        ArgumentException.ThrowIfNullOrWhiteSpace(remoteName);
        ArgumentNullException.ThrowIfNull(value);

        if (builder.Resource.ManagedSecrets.Any(secret => string.Equals(secret.LocalName, name, StringComparison.OrdinalIgnoreCase)))
        {
            throw new DistributedApplicationException($"Bitwarden resource '{builder.Resource.Name}' already declares a managed secret with local name '{name}'. Managed local names must be unique per Bitwarden resource.");
        }

        if (builder.Resource.ManagedSecrets.Any(secret => string.Equals(secret.RemoteName, remoteName, StringComparison.OrdinalIgnoreCase)))
        {
            throw new DistributedApplicationException($"Bitwarden resource '{builder.Resource.Name}' already declares a managed secret with remote name '{remoteName}'. Managed remote names must be unique per Bitwarden resource.");
        }

        string secretResourceName = $"{builder.Resource.Name}-{name}";
        BitwardenSecretResource secret = new(secretResourceName, name, remoteName, builder.Resource, value);
        builder.Resource.RegisterManagedSecret(secret);

        builder.WithReferenceRelationship(secret);
        if (value is IResource valueResource)
        {
            builder.WithReferenceRelationship(valueResource);
        }
        else if (value is ReferenceExpression referenceExpression)
        {
            builder.WithReferenceRelationship(referenceExpression);
            WaitForReferencedResources(builder, referenceExpression);
        }

        return builder.ApplicationBuilder.AddResource(secret)
            .WithParentRelationship(builder)
            .WithInitialState(new CustomResourceSnapshot
            {
                ResourceType = "BitwardenSecret",
                IsHidden = true,
                Properties = []
            })
            // Managed secret children are implementation details of the declared graph.
            .ExcludeFromManifest();
    }

    private static void WaitForReferencedResources(
        IResourceBuilder<BitwardenSecretManagerResource> builder,
        ReferenceExpression referenceExpression)
    {
        HashSet<IResource> dependencies = [];

        foreach (object reference in ((IValueWithReferences)referenceExpression).References)
        {
            if (reference is not IResource dependency || !dependencies.Add(dependency))
            {
                continue;
            }

            if (ReferenceEquals(dependency, builder.Resource))
            {
                continue;
            }

            if (dependency is IResourceWithParent dependencyWithParent && ReferenceEquals(dependencyWithParent.Parent, builder.Resource))
            {
                continue;
            }

            builder.WaitFor(builder.ApplicationBuilder.CreateResourceBuilder(dependency));
        }
    }

    private static void ValidateAbsoluteUri(string value, string paramName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(value);

        if (!Uri.TryCreate(value, UriKind.Absolute, out _))
        {
            throw new ArgumentException("The value must be an absolute URI.", paramName);
        }
    }

    private static void AttachSecretDependencies<TDestination>(
        IResourceBuilder<TDestination> builder,
        IBitwardenSecretReference secretReference)
        where TDestination : IResourceWithEnvironment
    {
        builder.WithReferenceRelationship(secretReference.Resource);

        if (secretReference.SecretOwner is IResource secretOwner)
        {
            builder.WithReferenceRelationship(secretOwner);
        }

        if (builder.Resource is IResourceWithWaitSupport waitResource)
        {
            builder.ApplicationBuilder.CreateResourceBuilder(waitResource)
                .WaitFor(builder.ApplicationBuilder.CreateResourceBuilder(secretReference.Resource));
        }
    }
}