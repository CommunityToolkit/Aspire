using Aspire.Hosting;
using Aspire.Hosting.ApplicationModel;
using Microsoft.Extensions.DependencyInjection;
using System.Diagnostics;
using System.Management.Automation;
using System.Management.Automation.Runspaces;

namespace CommunityToolkit.Aspire.Hosting.PowerShell;

/// <summary>
/// Extensions for the <see cref="IDistributedApplicationBuilder"/> to add PowerShell runspace pool resources.
/// </summary>
public static class DistributedApplicationBuilderExtensions
{
    /// <summary>
    /// Adds a PowerShell runspace pool resource to the distributed application.
    /// </summary>
    /// <param name="builder"></param>
    /// <param name="name"></param>
    /// <param name="languageMode"></param>
    /// <param name="minRunspaces"></param>
    /// <param name="maxRunspaces"></param>
    /// <returns></returns>
    /// <exception cref="ArgumentException"></exception>
    /// <exception cref="DistributedApplicationException"></exception>
    public static IResourceBuilder<PowerShellRunspacePoolResource> AddPowerShell(
        this IDistributedApplicationBuilder builder,
        [ResourceName] string name,
        PSLanguageMode languageMode = PSLanguageMode.ConstrainedLanguage,
        int minRunspaces = 1,
        int maxRunspaces = 5)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);

        var pool = new PowerShellRunspacePoolResource(name, languageMode, minRunspaces, maxRunspaces);

        builder.Eventing.Subscribe<InitializeResourceEvent>(pool, async (e, ct) =>
        {
            var poolResource = e.Resource as PowerShellRunspacePoolResource;

            Debug.Assert(poolResource is not null);

            var loggerService = e.Services.GetRequiredService<ResourceLoggerService>();
            var notificationService = e.Services.GetRequiredService<ResourceNotificationService>();

            var sessionState = InitialSessionState.CreateDefault();

            // This will block until explicit and implied WaitFor calls are completed
            await builder.Eventing.PublishAsync(
                new BeforeResourceStartedEvent(poolResource, e.Services), ct);

            foreach (var annotation in poolResource.Annotations.OfType<PowerShellVariableReferenceAnnotation<ConnectionStringReference>>())
            {
                if (annotation is { } reference)
                {
                    var connectionString = await reference.Value.Resource.GetConnectionStringAsync(ct);
                    sessionState.Variables.Add(
                        new SessionStateVariableEntry(reference.Name, connectionString,
                            $"ConnectionString for {reference.Value.Resource.GetType().Name} '{reference.Name}'",
                            ScopedItemOptions.ReadOnly | ScopedItemOptions.AllScope));
                }
            }

            var poolName = poolResource.Name;
            var poolLogger = loggerService.GetLogger(poolName);

            _ = poolResource.StartAsync(sessionState, notificationService, poolLogger, ct);
        });
        

        return builder.AddResource(pool)
            .WithInitialState(new()
            {
                ResourceType = "PowerShellRunspacePool",
                State = KnownResourceStates.NotStarted,
                Properties = [

                    new ("LanguageMode", pool.LanguageMode.ToString()),
                    new ("MinRunspaces", pool.MinRunspaces.ToString()),
                    new ("MaxRunspaces", pool.MaxRunspaces.ToString())
                ]
            })
            .ExcludeFromManifest();
    }
}