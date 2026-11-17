// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace Aspire.Hosting.ApplicationModel;

/// <summary>Identifies a Terraform command managed by a Terraform environment.</summary>
public enum TerraformCommand
{
    /// <summary>The <c>terraform init</c> command.</summary>
    Init,
    /// <summary>The <c>terraform plan</c> command.</summary>
    Plan,
    /// <summary>The <c>terraform apply</c> command.</summary>
    Apply,
    /// <summary>The <c>terraform destroy</c> command.</summary>
    Destroy
}

/// <summary>Declares a Terraform command on an environment and the local resource executing it.</summary>
/// <param name="command">The Terraform command.</param>
/// <param name="resource">The local command resource, also the source of pipeline arguments and environment.</param>
public sealed class TerraformCommandAnnotation(TerraformCommand command, TerraformCliResource resource)
    : IResourceAnnotation
{
    /// <summary>Gets the Terraform command.</summary>
    public TerraformCommand Command { get; } = command;

    /// <summary>Gets the local command resource.</summary>
    public TerraformCliResource Resource { get; } = resource;
}

/// <summary>Configures the saved plan shared by the plan and apply commands of an environment.</summary>
/// <param name="outputPath">The absolute path of the saved plan file.</param>
public sealed class TerraformPlanAnnotation(string outputPath) : IResourceAnnotation
{
    /// <summary>Gets the absolute path of the saved plan file.</summary>
    public string OutputPath { get; } = outputPath;
}

/// <summary>Configures how Terraform environment steps are attached to the Aspire pipeline.</summary>
[AspireExport(ExposeProperties = true)]
public sealed class TerraformPipelineOptions
{
    /// <summary>
    /// Gets or sets whether steps depend on each other: plan on init, apply on plan (or init), and destroy on init.
    /// </summary>
    /// <remarks>Disabled by default so plan and apply can run in separate stages divided by an approval.</remarks>
    public bool ChainSteps { get; set; }

    /// <summary>Gets or sets whether the apply step runs as part of <c>aspire deploy</c>.</summary>
    public bool RequiredByDeploy { get; set; } = true;

    /// <summary>Gets or sets whether the destroy step runs as part of <c>aspire destroy</c>.</summary>
    public bool RequiredByDestroy { get; set; } = true;
}

/// <summary>Stores the pipeline options of a Terraform environment.</summary>
/// <param name="options">The pipeline options.</param>
public sealed class TerraformPipelineOptionsAnnotation(TerraformPipelineOptions options) : IResourceAnnotation
{
    /// <summary>Gets the pipeline options.</summary>
    public TerraformPipelineOptions Options { get; } = options;
}
