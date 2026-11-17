// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace Aspire.Hosting.ApplicationModel;

/// <summary>A resource that accepts Terraform configuration.</summary>
[AspireExport]
public interface ITerraformConfigurableResource : IResource, IResourceWithEnvironment;

/// <summary>A local Terraform CLI command.</summary>
/// <param name="name">The resource name.</param>
/// <param name="workingDirectory">The module directory.</param>
[AspireExport]
public sealed class TerraformCliResource(string name, string workingDirectory)
    : ExecutableResource(name, "terraform", workingDirectory), ITerraformConfigurableResource
{
    internal TerraformEnvironmentResource? Environment { get; set; }
    internal TerraformCommand? Operation { get; set; }
    internal string[] SelectedArguments { get; set; } = [];
}
