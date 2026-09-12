// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Aspire.Hosting.ApplicationModel;
using CommunityToolkit.Aspire.Hosting.GlitchTip;
using Microsoft.Extensions.Hosting;
using System.Globalization;

#pragma warning disable IDE0130 // Hosting extensions use the Aspire public API namespace.
namespace Aspire.Hosting;
#pragma warning restore IDE0130

/// <summary>Models a stack's GlitchTip project and connects its consumers.</summary>
public static class GlitchTipBuilderExtensions
{
    /// <summary>Adds the stack's single project, with a worktree-local server and required shared-instance deployment parameters.</summary>
    /// <param name="builder">The application builder.</param>
    /// <param name="name">The Aspire resource name.</param>
    /// <param name="projectSlug">The required stable GlitchTip project slug.</param>
    /// <param name="release">The reporting release shared by consumers unless overridden.</param>
    /// <returns>The project resource builder.</returns>
    /// <remarks>Publish mode adds required parameters named {name}-url, {name}-organization, {name}-initial-team, and {name}-api-token (secret). Use WithDeploymentParameters to bind custom parameters.</remarks>
    [AspireExport]
    public static IResourceBuilder<GlitchTipResource> AddGlitchTip(this IDistributedApplicationBuilder builder,
        [ResourceName] string name, IResourceBuilder<ParameterResource> projectSlug, IResourceBuilder<ParameterResource> release)
    {
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        ArgumentNullException.ThrowIfNull(projectSlug);
        ArgumentNullException.ThrowIfNull(release);
        if (!builder.Resources.Contains(projectSlug.Resource) || !builder.Resources.Contains(release.Resource))
        {
            throw new ArgumentException("GlitchTip parameters must belong to the same AppHost.");
        }
        if (builder.Resources.OfType<GlitchTipResource>().Any())
        {
            throw new InvalidOperationException("An Aspire stack has exactly one GlitchTip project. Reference the existing resource from each consumer.");
        }

        if (builder.ExecutionContext.IsPublishMode)
        {
            foreach (var suffix in new[] { "url", "organization", "initial-team", "api-token" })
            {
                var parameterName = $"{name}-{suffix}";
                if (builder.Resources.Any(resource => StringComparer.OrdinalIgnoreCase.Equals(resource.Name, parameterName)))
                {
                    throw new ArgumentException($"The GlitchTip deployment parameter name '{parameterName}' is already used. Custom deployment parameters must use distinct names.", nameof(name));
                }
            }
        }

        var resource = new GlitchTipResource(name, projectSlug.Resource, release.Resource, builder.Environment.EnvironmentName);
        var result = builder.AddResource(resource).WithInitialState(new CustomResourceSnapshot
        {
            ResourceType = "GlitchTip project",
            Properties = [],
            State = new(KnownResourceStates.Starting, null)
        });
        // The project is external state, never deployable server infrastructure.
        result.ExcludeFromManifest();
        if (builder.ExecutionContext.IsRunMode)
        {
            GlitchTipLocalHosting.Configure(result);
            GlitchTipMonitoring.ConfigureLocalReferences(result);
        }
        else
        {
            resource.InstanceUrl = builder.AddParameter($"{name}-url").Resource;
            resource.Organization = builder.AddParameter($"{name}-organization").Resource;
            resource.InitialTeam = builder.AddParameter($"{name}-initial-team").Resource;
            resource.ApiToken = builder.AddParameter($"{name}-api-token", secret: true).Resource;
            resource.OwnedDeploymentParameters.AddRange([resource.InstanceUrl, resource.Organization, resource.InitialTeam, resource.ApiToken]);
            GlitchTipLifecycle.ConfigureDeployment(result);
        }
        return result;
    }

    /// <summary>Replaces the default deployment parameters with custom instance, organization, initial team, and secret token bindings.</summary>
    /// <param name="builder">The GlitchTip project.</param>
    /// <param name="instanceUrl">The shared server URL parameter.</param>
    /// <param name="organization">The existing organization slug.</param>
    /// <param name="initialTeam">The existing team used only when creating a project.</param>
    /// <param name="apiToken">A secret management token parameter.</param>
    /// <returns>The original builder.</returns>
    /// <remarks>Only publish mode changes. Unused parameters created by AddGlitchTip are removed; caller-created parameters are retained. Repeated calls replace the bindings.</remarks>
    [AspireExport]
    public static IResourceBuilder<GlitchTipResource> WithDeploymentParameters(this IResourceBuilder<GlitchTipResource> builder,
        IResourceBuilder<ParameterResource> instanceUrl, IResourceBuilder<ParameterResource> organization,
        IResourceBuilder<ParameterResource> initialTeam, IResourceBuilder<ParameterResource> apiToken)
    {
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentNullException.ThrowIfNull(instanceUrl);
        ArgumentNullException.ThrowIfNull(organization);
        ArgumentNullException.ThrowIfNull(initialTeam);
        ArgumentNullException.ThrowIfNull(apiToken);
        if (!builder.ApplicationBuilder.Resources.Contains(builder.Resource) ||
            new[] { instanceUrl, organization, initialTeam, apiToken }.Any(parameter => !builder.ApplicationBuilder.Resources.Contains(parameter.Resource)))
        {
            throw new ArgumentException("GlitchTip parameters must belong to the same AppHost.");
        }
        if (!apiToken.Resource.Secret) throw new ArgumentException("The GlitchTip management token must be a secret parameter.", nameof(apiToken));
        if (builder.ApplicationBuilder.ExecutionContext.IsPublishMode)
        {
            var replacements = new[] { instanceUrl.Resource, organization.Resource, initialTeam.Resource, apiToken.Resource };
            foreach (var owned in builder.Resource.OwnedDeploymentParameters.Where(parameter => !replacements.Contains(parameter)).ToArray())
            {
                builder.ApplicationBuilder.Resources.Remove(owned);
                builder.Resource.OwnedDeploymentParameters.Remove(owned);
            }
            builder.Resource.InstanceUrl = instanceUrl.Resource;
            builder.Resource.Organization = organization.Resource;
            builder.Resource.InitialTeam = initialTeam.Resource;
            builder.Resource.ApiToken = apiToken.Resource;
        }
        return builder;
    }

    /// <summary>Sets the optional display name, reconciled without changing project identity.</summary>
    /// <param name="builder">The project.</param>
    /// <param name="displayName">The display-name parameter.</param>
    /// <returns>The original builder.</returns>
    [AspireExport]
    public static IResourceBuilder<GlitchTipResource> WithProjectDisplayName(this IResourceBuilder<GlitchTipResource> builder, IResourceBuilder<ParameterResource> displayName)
    {
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentNullException.ThrowIfNull(displayName);
        if (!builder.ApplicationBuilder.Resources.Contains(displayName.Resource)) throw new ArgumentException("GlitchTip parameters must belong to the same AppHost.", nameof(displayName));
        builder.Resource.DisplayName = displayName.Resource;
        return builder;
    }

    /// <summary>References the project, forwards reporting metadata, and waits for local provisioning.</summary>
    /// <typeparam name="T">A project or container with environment configuration.</typeparam>
    /// <param name="builder">The consumer.</param>
    /// <param name="glitchtip">The stack's GlitchTip project.</param>
    /// <returns>The original consumer.</returns>
    [AspireExport("withGlitchTipReference")]
    public static IResourceBuilder<T> WithReference<T>(this IResourceBuilder<T> builder, IResourceBuilder<GlitchTipResource> glitchtip)
        where T : IResourceWithEnvironment, IResourceWithWaitSupport
    {
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentNullException.ThrowIfNull(glitchtip);
        if (!builder.ApplicationBuilder.Resources.Contains(glitchtip.Resource))
        {
            throw new ArgumentException("A GlitchTip reference must belong to the same Aspire stack.", nameof(glitchtip));
        }
        if (builder.Resource.Annotations.OfType<GlitchTipConsumerAnnotation>().Any()) return builder;
        builder.WithAnnotation(new GlitchTipConsumerAnnotation(glitchtip.Resource));
        builder.WithEnvironment($"ConnectionStrings__{glitchtip.Resource.Name}", glitchtip.Resource.ConnectionStringExpression);
        builder.WithEnvironment("SENTRY_DSN", glitchtip.Resource.ConnectionStringExpression);
        builder.WithEnvironment("SENTRY_ENVIRONMENT", glitchtip.Resource.EnvironmentName);
        builder.WithEnvironment("Aspire__GlitchTip__Environment", glitchtip.Resource.EnvironmentName);
        builder.WithEnvironment("Aspire__GlitchTip__ServiceName", builder.Resource.Name);
        builder.WithEnvironment(ctx =>
        {
            var release = builder.Resource.Annotations.OfType<GlitchTipReleaseAnnotation>().LastOrDefault()?.Release ?? glitchtip.Resource.Release;
            var settings = builder.Resource.Annotations.OfType<GlitchTipTelemetryAnnotation>().LastOrDefault()?.Options ?? new();
            ctx.EnvironmentVariables["SENTRY_RELEASE"] = release;
            ctx.EnvironmentVariables["Aspire__GlitchTip__Release"] = release;
            ctx.EnvironmentVariables["Aspire__GlitchTip__EnableErrors"] = settings.EnableErrors.ToString(CultureInfo.InvariantCulture);
            ctx.EnvironmentVariables["Aspire__GlitchTip__EnableLogs"] = settings.EnableLogs.ToString(CultureInfo.InvariantCulture);
            ctx.EnvironmentVariables["Aspire__GlitchTip__EnableTracing"] = settings.EnableTracing.ToString(CultureInfo.InvariantCulture);
            ctx.EnvironmentVariables["Aspire__GlitchTip__TracesSampleRate"] = settings.TracesSampleRate.ToString(CultureInfo.InvariantCulture);
        });
        if (builder.ApplicationBuilder.ExecutionContext.IsRunMode) builder.WaitForCompletion(glitchtip);
        return builder;
    }

    /// <summary>Overrides a service's release for both its telemetry and registered artifacts.</summary>
    /// <typeparam name="T">The consumer resource type.</typeparam>
    /// <param name="builder">The consumer.</param>
    /// <param name="release">The release parameter.</param>
    /// <returns>The original builder.</returns>
    [AspireExport("withParameterGlitchTipRelease")]
    public static IResourceBuilder<T> WithGlitchTipRelease<T>(this IResourceBuilder<T> builder, IResourceBuilder<ParameterResource> release) where T : IResourceWithEnvironment
    {
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentNullException.ThrowIfNull(release);
        if (!builder.ApplicationBuilder.Resources.Contains(release.Resource)) throw new ArgumentException("GlitchTip parameters must belong to the same AppHost.", nameof(release));
        return builder.WithAnnotation(new GlitchTipReleaseAnnotation(release.Resource), ResourceAnnotationMutationBehavior.Replace);
    }

    /// <summary>Configures independently enabled signals without adding application instrumentation.</summary>
    /// <typeparam name="T">The consumer resource type.</typeparam>
    /// <param name="builder">The consumer.</param>
    /// <param name="options">The reporting settings.</param>
    /// <returns>The original builder.</returns>
    [AspireExport]
    public static IResourceBuilder<T> WithGlitchTipTelemetry<T>(this IResourceBuilder<T> builder, GlitchTipTelemetryOptions options) where T : IResourceWithEnvironment
    {
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentNullException.ThrowIfNull(options);
        if (double.IsNaN(options.TracesSampleRate) || options.TracesSampleRate < 0 || options.TracesSampleRate > 1) throw new ArgumentOutOfRangeException(nameof(options));
        return builder.WithAnnotation(new GlitchTipTelemetryAnnotation(options), ResourceAnnotationMutationBehavior.Replace);
    }
}