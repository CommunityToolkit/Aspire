using System;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;

namespace Aspire.Hosting.ApplicationModel;

/// <summary>
/// A resource that represents a Uvicorn application.
/// </summary>
/// <param name="name">The name of the resource.</param>
/// <param name="workingDirectory">The working directory to use for the command.</param>
public class UvicornAppResource(string name, string workingDirectory)
    : ExecutableResource(name, "uv", workingDirectory), IResourceWithServiceDiscovery;