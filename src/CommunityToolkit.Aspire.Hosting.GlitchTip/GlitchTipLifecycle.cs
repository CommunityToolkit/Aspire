// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Aspire.Hosting;
using Aspire.Hosting.ApplicationModel;
using CommunityToolkit.Aspire.Hosting.GlitchTip.Deployment;
using CommunityToolkit.Aspire.Hosting.GlitchTip.Management;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace CommunityToolkit.Aspire.Hosting.GlitchTip;

internal static class GlitchTipLifecycle
{
    internal static void ConfigureDeployment(IResourceBuilder<GlitchTipResource> builder)
    {
        GlitchTipDeploymentPipeline.Configure(builder, async context =>
        {
            var resource = builder.Resource;
            try
            {
                var instance = new Uri(await RequiredAsync(resource.InstanceUrl, "instance URL", context.CancellationToken).ConfigureAwait(false));
                var organization = await RequiredAsync(resource.Organization, "organization", context.CancellationToken).ConfigureAwait(false);
                var token = await RequiredAsync(resource.ApiToken, "management token", context.CancellationToken).ConfigureAwait(false);
                await ProvisionAsync(resource, instance, organization, token, context.CancellationToken).ConfigureAwait(false);
                if (context.Logger.IsEnabled(LogLevel.Information))
                {
                    context.Logger.LogInformation("GlitchTip project resolved for {Resource}.", resource.Name);
                }
            }
            catch (Exception ex)
            {
                resource.Provisioned.TrySetException(ex);
                throw;
            }
        }, async context =>
        {
            await GlitchTipMonitoring.SynchronizeAsync(builder, context.Model.Resources, context.CancellationToken, context).ConfigureAwait(false);
            await GlitchTipArtifacts.UploadAsync(builder, context.Model.Resources, context.CancellationToken).ConfigureAwait(false);
        });
    }

    internal static async Task ProvisionLocalAsync(IResourceBuilder<GlitchTipResource> builder, IServiceProvider services, CancellationToken cancellationToken)
    {
        var resource = builder.Resource;
        var notifications = services.GetRequiredService<ResourceNotificationService>();
        var logger = services.GetRequiredService<ResourceLoggerService>().GetLogger(resource);
        try
        {
            using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            timeout.CancelAfter(TimeSpan.FromMinutes(3));
            var ct = timeout.Token;
            var instance = new Uri(new EndpointReference(resource.LocalServer!, "http").Url.TrimEnd('/') + "/");
            var organization = await RequiredAsync(resource.Organization, "local organization", ct).ConfigureAwait(false);
            var team = await RequiredAsync(resource.InitialTeam, "local initial team", ct).ConfigureAwait(false);
            var email = await RequiredAsync(resource.AdminEmail, "local admin email", ct).ConfigureAwait(false);
            var password = await RequiredAsync(resource.AdminPassword, "local admin password", ct).ConfigureAwait(false);
            var token = await GlitchTipLocalBootstrap.EnsureAsync(instance, email, password, organization, team, ct).ConfigureAwait(false);
            await ProvisionAsync(resource, instance, organization, token, ct).ConfigureAwait(false);
            // Endpoint allocation precedes readiness. Monitor creation never waits for consumer health.
            await GlitchTipMonitoring.SynchronizeAsync(builder, builder.ApplicationBuilder.Resources, ct).ConfigureAwait(false);
            await GlitchTipArtifacts.UploadLocalAsync(builder, builder.ApplicationBuilder.Resources, ct).ConfigureAwait(false);
            await notifications.PublishUpdateAsync(resource, snapshot => snapshot with
            {
                State = new(KnownResourceStates.Finished, KnownResourceStateStyles.Success),
                ExitCode = 0
            }).ConfigureAwait(false);
            if (logger.IsEnabled(LogLevel.Information))
            {
                logger.LogInformation("Project ready. Local login uses the {EmailParameter} and {PasswordParameter} Aspire parameters. MCP is available from the server resource.", resource.AdminEmail!.Name, resource.AdminPassword!.Name);
            }
        }
        catch (Exception ex)
        {
            resource.Provisioned.TrySetException(ex);
            await notifications.PublishUpdateAsync(resource, snapshot => snapshot with
            {
                State = new(KnownResourceStates.FailedToStart, KnownResourceStateStyles.Error),
                ExitCode = 1
            }).ConfigureAwait(false);
            if (ex is GlitchTipManagementException managementError)
            {
                logger.LogError("GlitchTip project setup failed: {Failure}", managementError.Message);
            }
            else if (logger.IsEnabled(LogLevel.Error))
            {
                logger.LogError("GlitchTip project setup failed ({FailureType}). Check the server and provisioning configuration.", ex.GetType().Name);
            }
            throw;
        }
    }

    private static async Task ProvisionAsync(GlitchTipResource resource, Uri instance, string organization, string token, CancellationToken ct)
    {
        var slug = await RequiredAsync(resource.ProjectSlug, "project slug", ct).ConfigureAwait(false);
        _ = await RequiredAsync(resource.Release, "release", ct).ConfigureAwait(false);
        var team = await RequiredAsync(resource.InitialTeam, "initial team", ct).ConfigureAwait(false);
        var displayName = resource.DisplayName is null ? null : await RequiredAsync(resource.DisplayName, "display name", ct).ConfigureAwait(false);
        using var client = new GlitchTipManagementClient(instance, token);
        var project = await client.EnsureProjectAsync(organization, team, slug, displayName, ct).ConfigureAwait(false);
        resource.Management = (instance, token, organization);
        resource.Provisioned.TrySetResult(project);
    }

    internal static async Task<string> RequiredAsync(ParameterResource? parameter, string description, CancellationToken cancellationToken)
    {
        if (parameter is null) throw new DistributedApplicationException($"GlitchTip requires a {description} parameter. Supply its deployment configuration or bind it with WithDeploymentParameters.");
        var value = await parameter.GetValueAsync(cancellationToken).ConfigureAwait(false);
        return !string.IsNullOrWhiteSpace(value) ? value : throw new DistributedApplicationException($"GlitchTip {description} parameter '{parameter.Name}' is empty.");
    }
}