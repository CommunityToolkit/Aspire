using Aspire.Components.Common.Tests;
using Aspire.Hosting;
using Aspire.Hosting.Utils;
using EventStore.Client;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Hosting;
using System.Text;
using System.Text.Json;
using Xunit.Abstractions;

namespace CommunityToolkit.Aspire.Hosting.EventStore.Tests;

[RequiresDocker]
public class EventStoreFunctionalTests(ITestOutputHelper testOutputHelper)
{
    public const string TestStreamNamePrefix = "account-";
    public const string TestAccountName = "John Doe";

    [Fact]
    public async Task VerifyEventStoreResource()
    {
        using var builder = TestDistributedApplicationBuilder.Create().WithTestAndResourceLogging(testOutputHelper);

        var eventstore = builder.AddEventStore("eventstore");

        using var app = builder.Build();

        await app.StartAsync();

        var rns = app.Services.GetRequiredService<ResourceNotificationService>();

        await rns.WaitForResourceHealthyAsync(eventstore.Resource.Name, default);

        var hostBuilder = Host.CreateApplicationBuilder();

        hostBuilder.Configuration[$"ConnectionStrings:{eventstore.Resource.Name}"] = await eventstore.Resource.ConnectionStringExpression.GetValueAsync(default);

        hostBuilder.AddEventStoreClient(eventstore.Resource.Name);

        using var host = hostBuilder.Build();

        await host.StartAsync();

        var eventStoreClient = host.Services.GetRequiredService<EventStoreClient>();

        var id = await CreateTestDataAsync(eventStoreClient);
        await VerifyTestDataAsync(eventStoreClient, id);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task WithDataShouldPersistStateBetweenUsages(bool useVolume)
    {
        string? volumeName = null;
        string? bindMountPath = null;
        Guid? id = null;

        try
        {
            using var builder1 = TestDistributedApplicationBuilder.Create().WithTestAndResourceLogging(testOutputHelper);
            var eventstore1 = builder1.AddEventStore("eventstore");

            if (useVolume)
            {
                // Use a deterministic volume name to prevent them from exhausting the machines if deletion fails
                volumeName = VolumeNameGenerator.Generate(eventstore1, nameof(WithDataShouldPersistStateBetweenUsages));

                // if the volume already exists (because of a crashing previous run), delete it
                DockerUtils.AttemptDeleteDockerVolume(volumeName, throwOnFailure: true);
                eventstore1.WithDataVolume(volumeName);
            }
            else
            {
                bindMountPath = Directory.CreateTempSubdirectory().FullName;

                if (!OperatingSystem.IsWindows())
                {
                    // Change permissions for non-root accounts (container user account)
                    const UnixFileMode OwnershipPermissions =
                       UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute |
                       UnixFileMode.GroupRead | UnixFileMode.GroupWrite | UnixFileMode.GroupExecute |
                       UnixFileMode.OtherRead | UnixFileMode.OtherWrite | UnixFileMode.OtherExecute;

                    File.SetUnixFileMode(bindMountPath, OwnershipPermissions);
                }

                eventstore1.WithDataBindMount(bindMountPath);
            }

            using (var app = builder1.Build())
            {
                await app.StartAsync();

                var rns = app.Services.GetRequiredService<ResourceNotificationService>();

                await rns.WaitForResourceHealthyAsync(eventstore1.Resource.Name, default);

                try
                {
                    var hostBuilder = Host.CreateApplicationBuilder();

                    hostBuilder.Configuration[$"ConnectionStrings:{eventstore1.Resource.Name}"] = await eventstore1.Resource.ConnectionStringExpression.GetValueAsync(default);

                    hostBuilder.AddEventStoreClient(eventstore1.Resource.Name);

                    using (var host = hostBuilder.Build())
                    {
                        await host.StartAsync();

                        var eventStoreClient = host.Services.GetRequiredService<EventStoreClient>();
                        id = await CreateTestDataAsync(eventStoreClient);
                        await VerifyTestDataAsync(eventStoreClient, id.Value);
                    }
                }
                finally
                {
                    // Stops the container, or the Volume would still be in use
                    await app.StopAsync();
                }
            }

            using var builder2 = TestDistributedApplicationBuilder.Create().WithTestAndResourceLogging(testOutputHelper);
            var eventstore2 = builder2.AddEventStore("eventstore");

            if (useVolume)
            {
                eventstore2.WithDataVolume(volumeName);
            }
            else
            {
                //EventStore shutdown can be slightly delayed, so second instance might fail to start when using the same bind mount before shutdown.
                await Task.Delay(TimeSpan.FromSeconds(5));
                eventstore2.WithDataBindMount(bindMountPath!);
            }

            using (var app = builder2.Build())
            {
                await app.StartAsync();

                var rns = app.Services.GetRequiredService<ResourceNotificationService>();

                await rns.WaitForResourceHealthyAsync(eventstore1.Resource.Name, default);

                try
                {
                    var hostBuilder = Host.CreateApplicationBuilder();

                    hostBuilder.Configuration[$"ConnectionStrings:{eventstore2.Resource.Name}"] = await eventstore2.Resource.ConnectionStringExpression.GetValueAsync(default);

                    hostBuilder.AddEventStoreClient(eventstore2.Resource.Name);

                    using (var host = hostBuilder.Build())
                    {
                        await host.StartAsync();
                        var eventStoreClient = host.Services.GetRequiredService<EventStoreClient>();

                        await VerifyTestDataAsync(eventStoreClient, id.Value);
                    }
                }
                finally
                {
                    // Stops the container, or the Volume would still be in use
                    await app.StopAsync();
                }
            }

        }
        finally
        {
            if (volumeName is not null)
            {
                DockerUtils.AttemptDeleteDockerVolume(volumeName);
            }

            if (bindMountPath is not null)
            {
                try
                {
                    Directory.Delete(bindMountPath, recursive: true);
                }
                catch
                {
                    // Don't fail test if we can't clean the temporary folder
                }
            }
        }
    }

    [Fact]
    public async Task VerifyWaitForEventStoreBlocksDependentResources()
    {
        var cts = new CancellationTokenSource(TimeSpan.FromMinutes(5));
        using var builder = TestDistributedApplicationBuilder.Create().WithTestAndResourceLogging(testOutputHelper);

        var healthCheckTcs = new TaskCompletionSource<HealthCheckResult>();
        builder.Services.AddHealthChecks().AddAsyncCheck("blocking_check", () =>
        {
            return healthCheckTcs.Task;
        });

        var resource = builder.AddEventStore("resource")
                              .WithHealthCheck("blocking_check");

        var dependentResource = builder.AddContainer("nginx", "mcr.microsoft.com/cbl-mariner/base/nginx", "1.22")
                                       .WaitFor(resource);

        using var app = builder.Build();

        var pendingStart = app.StartAsync(cts.Token);

        var rns = app.Services.GetRequiredService<ResourceNotificationService>();

        await rns.WaitForResourceAsync(resource.Resource.Name, KnownResourceStates.Running, cts.Token);

        await rns.WaitForResourceAsync(dependentResource.Resource.Name, KnownResourceStates.Waiting, cts.Token);

        healthCheckTcs.SetResult(HealthCheckResult.Healthy());

        await rns.WaitForResourceHealthyAsync(resource.Resource.Name, cts.Token);

        await rns.WaitForResourceAsync(dependentResource.Resource.Name, KnownResourceStates.Running, cts.Token);

        await pendingStart;

        await app.StopAsync();
    }

    private static async Task<Guid> CreateTestDataAsync(EventStoreClient eventStoreClient)
    {
        var id = Guid.NewGuid();
        var accountCreated = new AccountCreated(id, TestAccountName);
        var data = Encoding.UTF8.GetBytes(JsonSerializer.Serialize(accountCreated));
        var eventData = new EventData(Uuid.NewUuid(), nameof(AccountCreated), data);
        var streamName = $"{TestStreamNamePrefix}{id}";

        var writeResult = await eventStoreClient.AppendToStreamAsync(streamName, StreamRevision.None, [eventData]);
        Assert.NotNull(writeResult);

        return id;
    }

    private static async Task VerifyTestDataAsync(EventStoreClient eventStoreClient, Guid id)
    {
        var streamName = $"{TestStreamNamePrefix}{id}";

        var readResult = eventStoreClient.ReadStreamAsync(Direction.Forwards, streamName, StreamPosition.Start);
        Assert.NotNull(readResult);

        var readState = await readResult.ReadState;
        Assert.Equal(ReadState.Ok, readState);

        await foreach (var resolvedEvent in readResult)
        {
            var @event = JsonSerializer.Deserialize<AccountCreated>(Encoding.UTF8.GetString(resolvedEvent.Event.Data.Span));
            Assert.NotNull(@event);
            Assert.Equal(id, @event.Id);
            Assert.Equal(TestAccountName, @event.Name);
        }
    }

    private sealed record AccountCreated(Guid Id, string Name);
}
