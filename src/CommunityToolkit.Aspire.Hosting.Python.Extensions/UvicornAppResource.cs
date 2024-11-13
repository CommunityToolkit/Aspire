using System;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;

namespace Aspire.Hosting.ApplicationModel;

public class UvicornAppResource(string name, string workingDirectory)
    : ExecutableResource(name, "uvicorn", workingDirectory), IResourceWithServiceDiscovery;