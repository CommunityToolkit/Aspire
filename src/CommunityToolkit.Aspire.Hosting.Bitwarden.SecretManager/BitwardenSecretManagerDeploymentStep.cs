#pragma warning disable ASPIREPIPELINES001

using Aspire.Hosting.ApplicationModel;
using Aspire.Hosting.Pipelines;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace CommunityToolkit.Aspire.Hosting.Bitwarden.SecretManager;

/// <summary>
/// Runs the Bitwarden reconciliation during the AppHost deployment pipeline.
/// </summary>
internal static class BitwardenSecretManagerDeploymentStep
{
    public static async Task ExecuteAsync(PipelineStepContext context, string resourceName)
    {
        BitwardenSecretManagerResource? bitwarden = context.Model.Resources
            .OfType<BitwardenSecretManagerResource>()
            .FirstOrDefault(resource => string.Equals(resource.Name, resourceName, StringComparison.Ordinal));

        if (bitwarden is null)
        {
            return;
        }

        context.Logger.LogInformation("Starting Bitwarden deployment step as part of the deployment pipeline for resource '{ResourceName}'.", resourceName);

        try
        {
            BitwardenSecretManagerReconciler reconciler = context.Services.GetRequiredService<BitwardenSecretManagerReconciler>();
            BitwardenReconciliationResult result = await reconciler.InitializeAsync(bitwarden, context.Services, context.Logger, context.CancellationToken).ConfigureAwait(false);

            context.Logger.LogInformation("Bitwarden deployment step completed successfully for resource '{ResourceName}'. Project ID: {ProjectId}", resourceName, result.ProjectId.ToString("D"));
        }
        catch (Exception ex)
        {
            context.Logger.LogError(ex, "Bitwarden deployment step failed during deployment for resource '{ResourceName}'.", resourceName);
            throw;
        }
    }
}
