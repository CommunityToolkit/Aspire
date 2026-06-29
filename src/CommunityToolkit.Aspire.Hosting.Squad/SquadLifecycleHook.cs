using Aspire.Hosting.ApplicationModel;
using Aspire.Hosting.Eventing;
using Aspire.Hosting.Lifecycle;
using Microsoft.Extensions.Logging;
using System.Security;

namespace Aspire.Hosting;

/// <summary>
/// Manages the Squad agent team lifecycle as a .NET Aspire application event subscriber.
///
/// <list type="bullet">
///   <item><description><b>BeforeStart:</b> publishes the <c>Spawning</c> state on each <see cref="SquadResource"/>.</description></item>
///   <item><description><b>AfterResourcesCreated:</b> transitions each squad to <c>Active</c>.</description></item>
///   <item><description><b>ResourceStopped:</b> transitions stopped Squad resources to <c>Finished</c>.</description></item>
/// </list>
/// </summary>
internal sealed class SquadLifecycleHook(
    ILogger<SquadLifecycleHook> logger,
    ResourceNotificationService notifications) : IDistributedApplicationEventingSubscriber
{
    // Known Aspire dashboard state styles (mirrors KnownResourceStateStyles names).
    private const string StateSpawning = "Spawning";
    private const string StateActive = "Active";
    private const string StateStopped = "Finished";

    // Lifecycle entry points.

    /// <inheritdoc />
    public Task SubscribeAsync(
        IDistributedApplicationEventing eventing,
        DistributedApplicationExecutionContext executionContext,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(eventing);

        eventing.Subscribe<BeforeStartEvent>(OnBeforeStartAsync);
        eventing.Subscribe<AfterResourcesCreatedEvent>(OnAfterResourcesCreatedAsync);
        eventing.Subscribe<ResourceStoppedEvent>(OnResourceStoppedAsync);

        return Task.CompletedTask;
    }

    private Task OnBeforeStartAsync(BeforeStartEvent @event, CancellationToken cancellationToken) =>
        BeforeStartAsync(@event.Model, cancellationToken);

    private Task OnAfterResourcesCreatedAsync(AfterResourcesCreatedEvent @event, CancellationToken cancellationToken) =>
        AfterResourcesCreatedAsync(@event.Model, cancellationToken);

    private Task OnResourceStoppedAsync(ResourceStoppedEvent @event, CancellationToken cancellationToken)
    {
        if (@event.Resource is SquadResource squad)
        {
            return notifications.PublishUpdateAsync(squad, s => s with
            {
                State = new ResourceStateSnapshot(StateStopped, KnownResourceStateStyles.Info),
            });
        }

        return Task.CompletedTask;
    }

    private async Task BeforeStartAsync(
        DistributedApplicationModel appModel,
        CancellationToken cancellationToken)
    {
        var squads = appModel.Resources.OfType<SquadResource>().ToList();
        if (squads.Count == 0) return;

        logger.LogInformation("Squad: {Count} squad team resource(s) discovered.", squads.Count);

        foreach (var squad in squads)
        {
            // Publish the squad resource as Spawning before it becomes Active.
            await notifications.PublishUpdateAsync(squad, s => s with
            {
                State = new ResourceStateSnapshot(StateSpawning, KnownResourceStateStyles.Info),
                Properties = [..ReadTeamProperties(squad)],
            });

            logger.LogInformation(
                "Squad '{Name}' is Spawning - {AgentCount} agent(s) discovered: {Agents}",
                squad.Name, squad.Agents.Count, string.Join(", ", squad.Agents));
        }
    }

    private async Task AfterResourcesCreatedAsync(
        DistributedApplicationModel appModel,
        CancellationToken cancellationToken)
    {
        var squads = appModel.Resources.OfType<SquadResource>().ToList();
        if (squads.Count == 0) return;

        foreach (var squad in squads)
        {
            logger.LogInformation("Squad '{Name}' is Active - {AgentCount} agent(s) ready.", squad.Name, squad.Agents.Count);

            await notifications.PublishUpdateAsync(squad, s => s with
            {
                State = new ResourceStateSnapshot(StateActive, KnownResourceStateStyles.Success),
                Properties = [..ReadTeamProperties(squad)],
            });
        }
    }

    // Helpers.

    /// <summary>
    /// Reads team metadata and live file stats for the dashboard property panel.
    /// </summary>
    private ResourcePropertySnapshot[] ReadTeamProperties(SquadResource squad)
    {
        try
        {
            return SquadDashboardProperties.CreateWithLiveStats(squad);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or SecurityException)
        {
            logger.LogWarning(
                ex,
                "Could not read Squad dashboard metadata for resource '{SquadName}' from '{TeamRoot}'.",
                squad.Name,
                squad.TeamRoot);
            return [];
        }
    }
}
