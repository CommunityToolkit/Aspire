using Aspire.Hosting.ApplicationModel;
using System.Diagnostics.CodeAnalysis;

namespace Aspire.Hosting.ApplicationModel;

/// <summary>
/// Represents a Vercel deployment target for Dockerfile-based services.
/// </summary>
/// <param name="name">The Aspire resource name for the Vercel environment.</param>
[Experimental("CTASPIREVERCEL001")]
[AspireExport(ExposeProperties = true)]
public sealed class VercelEnvironmentResource(string name) : Resource(name), IComputeEnvironmentResource;
