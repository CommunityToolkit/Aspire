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

    /// <summary>Gets or sets the Terraform workspace, or null to use Terraform's default selection.</summary>
    public string? Workspace { get; set; }
    /// <summary>Gets or sets the Terraform executable name or path.</summary>
    public string TerraformExecutablePath { get; set; } = "terraform";
}