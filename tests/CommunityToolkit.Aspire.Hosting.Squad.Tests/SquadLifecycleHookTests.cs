using Aspire.Hosting;
using Aspire.Hosting.ApplicationModel;
using Aspire.Hosting.Eventing;
using Aspire.Hosting.Lifecycle;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Hosting.Internal;
using Microsoft.Extensions.Logging.Abstractions;

namespace CommunityToolkit.Aspire.Hosting.Squad.Tests;

/// <summary>
/// Unit-tests <see cref="SquadLifecycleHook"/> directly — no Aspire dashboard/DCP runtime
/// required. We construct a real <see cref="ResourceNotificationService"/>, register the hook
/// against a captured-callback eventing surface, raise the three lifecycle events, and assert
/// the state transitions via <c>notifications.WatchAsync</c>.
///
/// Coverage targets: <c>SquadLifecycleHook</c> (BeforeStart → Spawning, AfterResourcesCreated →
/// Active, ResourceStopped → Finished, non-Squad ResourceStopped is a no-op, no-op when zero
/// squads, null-eventing guard, ReadTeamProperties dashboard-properties path).
/// </summary>
public class SquadLifecycleHookTests : IDisposable
{
    private readonly List<string> _tempRoots = new();

    [Fact]
    public async Task BeforeStart_PublishesSpawningStateOnEachSquad()
    {
        var (hook, notifications, eventing) = BuildHookAndNotificationsAsync();
        var squad = NewSquadWithSeededTeam("alpha");
        var model = new DistributedApplicationModel([squad]);

        await hook.SubscribeAsync(eventing, executionContext: null!, cancellationToken: default);
        await eventing.PublishBeforeStartAsync(model);

        var snapshot = await ReadCurrentSnapshotAsync(notifications, squad);
        Assert.Equal("Spawning", snapshot.State?.Text);
        Assert.Equal(KnownResourceStateStyles.Info, snapshot.State?.Style);

        // Dashboard properties (via SquadDashboardProperties.CreateWithLiveStats) should be populated.
        Assert.Contains(snapshot.Properties, p => p.Name == "Squad location");
        Assert.Contains(snapshot.Properties, p => p.Name == "Protocol");
    }

    [Fact]
    public async Task AfterResourcesCreated_PublishesActiveStateOnEachSquad()
    {
        var (hook, notifications, eventing) = BuildHookAndNotificationsAsync();
        var squad = NewSquadWithSeededTeam("beta");
        var model = new DistributedApplicationModel([squad]);

        await hook.SubscribeAsync(eventing, executionContext: null!, cancellationToken: default);
        await eventing.PublishAfterResourcesCreatedAsync(model);

        var snapshot = await ReadCurrentSnapshotAsync(notifications, squad);
        Assert.Equal("Active", snapshot.State?.Text);
        Assert.Equal(KnownResourceStateStyles.Success, snapshot.State?.Style);
    }

    [Fact]
    public async Task ResourceStopped_OnSquadResource_PublishesFinishedState()
    {
        var (hook, notifications, eventing) = BuildHookAndNotificationsAsync();
        var squad = NewSquadWithSeededTeam("gamma");

        await hook.SubscribeAsync(eventing, executionContext: null!, cancellationToken: default);
        await eventing.PublishResourceStoppedAsync(squad);

        var snapshot = await ReadCurrentSnapshotAsync(notifications, squad);
        Assert.Equal("Finished", snapshot.State?.Text);
    }

    [Fact]
    public async Task ResourceStopped_OnNonSquadResource_DoesNotPublishAnyUpdate()
    {
        var (hook, notifications, eventing) = BuildHookAndNotificationsAsync();
        var nonSquad = new TestResource("not-a-squad");

        await hook.SubscribeAsync(eventing, executionContext: null!, cancellationToken: default);
        await eventing.PublishResourceStoppedAsync(nonSquad);

        // No notification should have been published for the non-Squad resource. Watch with
        // a short timeout and assert nothing arrives.
        await Assert.ThrowsAsync<OperationCanceledException>(async () =>
        {
            using var cts = new CancellationTokenSource(TimeSpan.FromMilliseconds(250));
            await foreach (var _ in notifications.WatchAsync(cts.Token))
            {
                // If anything arrives at all, fail.
                throw new InvalidOperationException("Notification was published for a non-Squad resource.");
            }
        });
    }

    [Fact]
    public async Task BeforeStart_WithNoSquadResources_IsANoOp()
    {
        var (hook, notifications, eventing) = BuildHookAndNotificationsAsync();
        var model = new DistributedApplicationModel([new TestResource("placeholder")]);

        await hook.SubscribeAsync(eventing, executionContext: null!, cancellationToken: default);
        await eventing.PublishBeforeStartAsync(model);

        // Nothing should have been published. Same assertion pattern as above.
        await Assert.ThrowsAsync<OperationCanceledException>(async () =>
        {
            using var cts = new CancellationTokenSource(TimeSpan.FromMilliseconds(250));
            await foreach (var _ in notifications.WatchAsync(cts.Token))
            {
                throw new InvalidOperationException("BeforeStart should be a no-op with no SquadResources.");
            }
        });
    }

    [Fact]
    public async Task SubscribeAsync_WithNullEventing_ThrowsArgumentNullException()
    {
        var (hook, _, _) = BuildHookAndNotificationsAsync();
        await Assert.ThrowsAsync<ArgumentNullException>(
            () => hook.SubscribeAsync(eventing: null!, executionContext: null!, cancellationToken: default));
    }

    [Fact]
    public async Task BeforeStart_WhenDashboardPropertiesThrowsIOException_PublishesSpawningWithoutProperties()
    {
        var (hook, notifications, eventing) = BuildHookAndNotificationsAsync();
        // Seed a team root, then immediately delete it so SquadDashboardProperties.CreateWithLiveStats
        // throws (Directory.GetFiles on a deleted folder → IOException/DirectoryNotFoundException).
        // The hook should catch and degrade gracefully.
        var squad = NewSquadWithSeededTeam("delta");
        var teamRoot = squad.TeamRoot;
        Directory.Delete(teamRoot, recursive: true);
        _tempRoots.Remove(teamRoot);

        await hook.SubscribeAsync(eventing, executionContext: null!, cancellationToken: default);
        await eventing.PublishBeforeStartAsync(new DistributedApplicationModel([squad]));

        var snapshot = await ReadCurrentSnapshotAsync(notifications, squad);
        Assert.Equal("Spawning", snapshot.State?.Text);
        // Properties may be empty (graceful degradation) but the state transition still happened.
    }

    // ─────────────────────────────── helpers ───────────────────────────────

    private static (SquadLifecycleHook hook, ResourceNotificationService notifications, CapturingEventing eventing)
        BuildHookAndNotificationsAsync()
    {
        var lifetime = new ApplicationLifetime(NullLogger<ApplicationLifetime>.Instance);
#pragma warning disable CS0618 // 2-arg ResourceNotificationService ctor is marked obsolete in 13.5+ but is exactly what we need for an isolated unit test (no IServiceProvider / ResourceLoggerService spin-up required).
        var notifications = new ResourceNotificationService(
            NullLogger<ResourceNotificationService>.Instance,
            lifetime);
#pragma warning restore CS0618
        var hook = new SquadLifecycleHook(NullLogger<SquadLifecycleHook>.Instance, notifications);
        return (hook, notifications, new CapturingEventing());
    }

    private static async Task<CustomResourceSnapshot> ReadCurrentSnapshotAsync(
        ResourceNotificationService notifications,
        IResource resource)
    {
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        await foreach (var update in notifications.WatchAsync(cts.Token))
        {
            if (ReferenceEquals(update.Resource, resource))
            {
                return update.Snapshot;
            }
        }
        throw new TimeoutException($"No snapshot ever arrived for resource '{resource.Name}'.");
    }

    private SquadResource NewSquadWithSeededTeam(string name)
    {
        var teamRoot = CreateTeamRoot();
        var squadDir = Path.Combine(teamRoot, ".squad");
        Directory.CreateDirectory(squadDir);
        File.WriteAllText(Path.Combine(squadDir, "team.md"), "| Ralph | Work Monitor |");
        var ralphDir = Path.Combine(squadDir, "agents", "ralph");
        Directory.CreateDirectory(ralphDir);
        File.WriteAllText(Path.Combine(ralphDir, "charter.md"), "# ralph");
        return new SquadResource(name, teamRoot);
    }

    private string CreateTeamRoot()
    {
        var dir = Path.Combine(Path.GetTempPath(), "ctk-aspire-squad-lifecycle-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        _tempRoots.Add(dir);
        return dir;
    }

    public void Dispose()
    {
        foreach (var dir in _tempRoots)
        {
            try
            {
                if (Directory.Exists(dir)) Directory.Delete(dir, recursive: true);
            }
            catch
            {
                // Best-effort cleanup.
            }
        }
    }

    // ─── Test doubles ────────────────────────────────────────────────────

    /// <summary>
    /// Captures callbacks registered via <c>Subscribe&lt;T&gt;</c> and lets the test fire them
    /// synchronously. Avoids spinning up the full Aspire runtime (DCP / Dashboard binaries).
    /// </summary>
    private sealed class CapturingEventing : IDistributedApplicationEventing
    {
        private readonly Dictionary<Type, List<Delegate>> _subscriptions = new();

        public DistributedApplicationEventSubscription Subscribe<T>(
            Func<T, CancellationToken, Task> callback)
            where T : IDistributedApplicationEvent
        {
            if (!_subscriptions.TryGetValue(typeof(T), out var list))
            {
                list = new List<Delegate>();
                _subscriptions[typeof(T)] = list;
            }
            list.Add(callback);
            // Subscription token isn't used by SquadLifecycleHook; return default.
            return null!;
        }

        public DistributedApplicationEventSubscription Subscribe<T>(
            IResource resource,
            Func<T, CancellationToken, Task> callback)
            where T : IDistributedApplicationResourceEvent =>
            Subscribe<T>((evt, ct) => callback((T)evt, ct));

        public void Unsubscribe(DistributedApplicationEventSubscription subscription) { }

        public Task PublishAsync<T>(T @event, CancellationToken cancellationToken = default)
            where T : IDistributedApplicationEvent => PublishCore(@event, cancellationToken);

        public Task PublishAsync<T>(T @event, EventDispatchBehavior dispatchBehavior, CancellationToken cancellationToken = default)
            where T : IDistributedApplicationEvent => PublishCore(@event, cancellationToken);

        private async Task PublishCore<T>(T @event, CancellationToken cancellationToken) where T : IDistributedApplicationEvent
        {
            if (_subscriptions.TryGetValue(typeof(T), out var list))
            {
                foreach (var cb in list)
                {
                    await ((Func<T, CancellationToken, Task>)cb)(@event, cancellationToken);
                }
            }
        }

        // Convenience publishers for our 3 events.
        public Task PublishBeforeStartAsync(DistributedApplicationModel model) =>
            PublishCore(new BeforeStartEvent(services: null!, model), default);

        public Task PublishAfterResourcesCreatedAsync(DistributedApplicationModel model) =>
            PublishCore(new AfterResourcesCreatedEvent(services: null!, model), default);

        public Task PublishResourceStoppedAsync(IResource resource)
        {
            var resourceEvent = new ResourceEvent(resource, resourceId: "test", new CustomResourceSnapshot
            {
                ResourceType = "Test",
                CreationTimeStamp = DateTime.UtcNow,
                Properties = [],
            });
            return PublishCore(new ResourceStoppedEvent(resource, services: null!, resourceEvent), default);
        }
    }

    /// <summary>Lightweight non-Squad resource for the negative ResourceStopped test.</summary>
    private sealed class TestResource(string name) : Resource(name);
}
