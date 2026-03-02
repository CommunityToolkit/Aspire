using Aspire.Hosting;
using Aspire.Hosting.ApplicationModel;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
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

        var poolBuilder = builder.AddResource(pool)
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

        poolBuilder.OnInitializeResource(async (res, e, ct) =>
        {
            var loggerService = e.Services.GetRequiredService<ResourceLoggerService>();
            var notificationService = e.Services.GetRequiredService<ResourceNotificationService>();
            var hostLifetime = e.Services.GetRequiredService<IHostApplicationLifetime>();

            var sessionState = InitialSessionState.CreateDefault();
            sessionState.UseFullLanguageModeInDebugger = true;

            // This will block until explicit and implied WaitFor calls are completed
            await builder.Eventing.PublishAsync(
                new BeforeResourceStartedEvent(res, e.Services), ct);

            foreach (var annotation in res.Annotations.OfType<PowerShellVariableReferenceAnnotation<ConnectionStringReference>>())
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

            var poolName = res.Name;
            var poolLogger = loggerService.GetLogger(poolName);

            _ = res.StartAsync(sessionState, notificationService, poolLogger, hostLifetime, ct);
        });

        return poolBuilder;
    }
}