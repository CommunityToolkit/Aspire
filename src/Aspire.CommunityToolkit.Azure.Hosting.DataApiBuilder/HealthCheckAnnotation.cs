using System;
using System.Diagnostics.CodeAnalysis;
using Aspire.Hosting.ApplicationModel;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace Aspire.Hosting;

/// <summary>
/// An annotation that associates a health check factory with a resource
/// </summary>
/// <param name="healthCheckFactory">A function that creates the health check</param>
/// <remarks>
/// This has been copied from the David Fowler davidfowl/WaitForDependenciesAspire project and will likely be removed in the future.
/// </remarks>
[Experimental("CTASPIRE002", UrlFormat = "https://aka.ms/communitytoolkit/aspire/diagnostics#{0}")]
public class HealthCheckAnnotation(Func<IResource, CancellationToken, Task<IHealthCheck?>> healthCheckFactory) : IResourceAnnotation
{
    public Func<IResource, CancellationToken, Task<IHealthCheck?>> HealthCheckFactory { get; } = healthCheckFactory;

    public static HealthCheckAnnotation Create(Func<string, IHealthCheck> connectionStringFactory)
    {
        return new(async (resource, token) =>
        {
            if (resource is not IResourceWithConnectionString c)
            {
                return null;
            }

            if (await c.GetConnectionStringAsync(token) is not string cs)
            {
                return null;
            }

            return connectionStringFactory(cs);
        });
    }
}