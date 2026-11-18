// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace Aspire.Hosting.ApplicationModel;

/// <summary>Represents a Terraform module and its shared execution configuration, without running a process.</summary>
/// <param name="name">The environment resource name.</param>
/// <param name="workingDirectory">The absolute path to the Terraform module.</param>
[AspireExport]
public sealed class TerraformEnvironmentResource(string name, string workingDirectory)
    : Resource(name), ITerraformConfigurableResource, IResourceWithoutLifetime
{
    /// <summary>Gets the absolute path to the Terraform module.</summary>
    public string WorkingDirectory { get; } = workingDirectory;

    /// <summary>Gets the Terraform workspace selected with <c>WithWorkspace</c>, or null if none was selected.</summary>
    public string? Workspace { get; internal set; }

    /// <summary>Gets the Terraform executable name or path configured with <c>WithTerraformExecutablePath</c>.</summary>
    public string TerraformExecutablePath { get; internal set; } = "terraform";
}