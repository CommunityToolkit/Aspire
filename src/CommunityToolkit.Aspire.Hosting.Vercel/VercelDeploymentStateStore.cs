#pragma warning disable ASPIREPIPELINES001
#pragma warning disable ASPIREPIPELINES002
#pragma warning disable CTASPIREVERCEL001

using Aspire.Hosting.ApplicationModel;
using Aspire.Hosting.Pipelines;
using Microsoft.Extensions.DependencyInjection;
using Aspire.Hosting;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace CommunityToolkit.Aspire.Hosting.Vercel;

internal static class VercelDeploymentStateStore
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = true
    };

    public static async Task ValidateExistingAsync(
        PipelineStepContext context,
        VercelEnvironmentResource environment,
        VercelEnvironmentOptionsAnnotation options)
    {
        var stateManager = context.Services.GetRequiredService<IDeploymentStateManager>();
        var stateSection = await stateManager.AcquireSectionAsync(GetSectionName(environment), context.CancellationToken).ConfigureAwait(false);
        var existingState = Read(stateSection);
        if (existingState is not null)
        {
            Validate(environment, options, existingState);
        }
    }

    public static async Task<PreviousVercelDeployment?> GetPreviousAsync(
        PipelineStepContext context,
        VercelEnvironmentResource environment,
        VercelEnvironmentOptionsAnnotation options,
        string resourceName,
        string projectName)
    {
        var stateManager = context.Services.GetRequiredService<IDeploymentStateManager>();
        var stateSection = await stateManager.AcquireSectionAsync(GetSectionName(environment), context.CancellationToken).ConfigureAwait(false);
        var existingState = Read(stateSection);
        if (existingState is null)
        {
            return null;
        }

        Validate(environment, options, existingState);
        var entry = existingState.Deployments.FirstOrDefault(deployment =>
            string.Equals(deployment.ResourceName, resourceName, StringComparison.Ordinal)
            && string.Equals(deployment.ProjectName, projectName, StringComparison.Ordinal));

        return entry is null
            ? null
            : new(entry, VercelProjectEnvironment.GetName(existingState));
    }

    public static async Task SaveEntryAsync(
        PipelineStepContext context,
        VercelEnvironmentResource environment,
        VercelEnvironmentOptionsAnnotation options,
        VercelDeploymentStateEntry deployment)
    {
        var stateManager = context.Services.GetRequiredService<IDeploymentStateManager>();
        var stateSection = await stateManager.AcquireSectionAsync(GetSectionName(environment), context.CancellationToken).ConfigureAwait(false);
        var existingState = Read(stateSection);
        var state = existingState is null
            ? Create(environment, options, [deployment])
            : Merge(environment, options, existingState, deployment);

        stateSection.SetValue(Serialize(state));

        await stateManager.SaveSectionAsync(stateSection, context.CancellationToken).ConfigureAwait(false);
    }

    public static VercelDeploymentState? Read(DeploymentStateSection stateSection)
    {
        // DeploymentStateSection storage shape has changed across Aspire builds. Accept the
        // known wrappers so destroy can still clean up projects created by an older CLI.
        if (stateSection.Data.TryGetPropertyValue("value", out JsonNode? value)
            && value is not null)
        {
            return Deserialize(value);
        }

        value = stateSection.Data.FirstOrDefault().Value;
        if (value is not null)
        {
            return Deserialize(value);
        }

        if (stateSection.Data.ContainsKey("schemaVersion"))
        {
            return stateSection.Data.Deserialize<VercelDeploymentState>(JsonOptions);
        }

        return null;
    }

    public static void Validate(
        VercelEnvironmentResource environment,
        VercelEnvironmentOptionsAnnotation options,
        VercelDeploymentState state)
    {
        if (state.SchemaVersion != VercelConstants.DeploymentStateSchemaVersion)
        {
            throw new DistributedApplicationException($"Vercel deployment state for environment '{environment.Name}' uses unsupported schema version '{state.SchemaVersion}'. Redeploy the environment before destroying it.");
        }

        if (!string.Equals(state.Environment, environment.Name, StringComparison.Ordinal))
        {
            throw new DistributedApplicationException($"Vercel deployment state for environment '{state.Environment}' cannot be used to destroy environment '{environment.Name}'.");
        }

        string? configuredScope = NormalizeScope(options.Scope);
        if (!string.Equals(state.Scope, configuredScope, StringComparison.Ordinal))
        {
            string stateScope = string.IsNullOrWhiteSpace(state.Scope) ? "<default>" : state.Scope;
            string requestedScope = string.IsNullOrWhiteSpace(configuredScope) ? "<default>" : configuredScope;
            throw new DistributedApplicationException($"Vercel deployment state for environment '{environment.Name}' was created for scope '{stateScope}', but destroy is configured for scope '{requestedScope}'. Use the same Vercel scope that created the deployment state.");
        }
    }

    public static VercelDeploymentState RemoveManagedProject(VercelDeploymentState state, string projectName)
        => state with
        {
            Deployments = state.Deployments
                .Where(deployment => !deployment.ManagedByAspire || !string.Equals(deployment.ProjectName, projectName, StringComparison.Ordinal))
                .ToArray()
        };

    public static string GetSectionName(VercelEnvironmentResource environment) => $"{VercelConstants.StateSectionNamePrefix}{environment.Name}";

    public static string? GetProductionUrl(VercelEnvironmentOptionsAnnotation options, string projectName)
        => options.Production ? $"https://{projectName}.vercel.app" : null;

    private static VercelDeploymentState Create(
        VercelEnvironmentResource environment,
        VercelEnvironmentOptionsAnnotation options,
        VercelDeploymentStateEntry[] deployments)
        => new(
            VercelConstants.DeploymentStateSchemaVersion,
            environment.Name,
            NormalizeScope(options.Scope),
            NormalizeTarget(options.Target),
            options.Production,
            deployments);

    private static VercelDeploymentState Merge(
        VercelEnvironmentResource environment,
        VercelEnvironmentOptionsAnnotation options,
        VercelDeploymentState existingState,
        VercelDeploymentStateEntry deployment)
    {
        Validate(environment, options, existingState);

        return Create(
            environment,
            options,
            [
                .. existingState.Deployments.Where(existing =>
                    !string.Equals(existing.ResourceName, deployment.ResourceName, StringComparison.Ordinal)
                    || !string.Equals(existing.ProjectName, deployment.ProjectName, StringComparison.Ordinal)),
                deployment
            ]);
    }

    private static VercelDeploymentState? Deserialize(JsonNode value)
    {
        return value.GetValueKind() == JsonValueKind.String
            ? JsonSerializer.Deserialize<VercelDeploymentState>(value.GetValue<string>(), JsonOptions)
            : value.Deserialize<VercelDeploymentState>(JsonOptions);
    }

    public static string Serialize(VercelDeploymentState state)
        => JsonSerializer.Serialize(state, JsonOptions);

    private static string? NormalizeScope(string? scope)
        => string.IsNullOrWhiteSpace(scope) ? null : scope;

    private static string? NormalizeTarget(string? target)
        => string.IsNullOrWhiteSpace(target) ? null : target;
}
