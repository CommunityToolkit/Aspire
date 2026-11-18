// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace Aspire.Hosting;

/// <summary>Methods for adding and configuring Terraform CLI resources.</summary>
public static class TerraformCliExtensions
{
    /// <summary>Adds a standalone Terraform command.</summary>
    /// <param name="builder">The application builder.</param>
    /// <param name="name">The resource name.</param>
    /// <param name="workingDirectory">The module path relative to the AppHost, or an absolute path.</param>
    /// <returns>The command resource builder.</returns>
    [AspireExport]
    public static IResourceBuilder<TerraformCliResource> AddTerraformCli(
        this IDistributedApplicationBuilder builder, [ResourceName] string name, string workingDirectory)
    {
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        ArgumentException.ThrowIfNullOrWhiteSpace(workingDirectory);

        var resource = new TerraformCliResource(name, Path.GetFullPath(workingDirectory, builder.AppHostDirectory));
        return builder.AddResource(resource)
            .WithEnvironment("TF_IN_AUTOMATION", "true")
            .WithArgs(context =>
            {
                foreach (var argument in resource.SelectedArguments)
                {
                    context.Args.Add(argument);
                }
            })
            .ExcludeFromManifest();
    }

    /// <summary>Selects the init command.</summary>
    /// <param name="builder">The CLI resource.</param>
    /// <param name="args">Additional command arguments.</param>
    /// <returns>The CLI resource.</returns>
    [AspireExport("withTerraformCliInit", MethodName = "withInit")]
    public static IResourceBuilder<TerraformCliResource> WithInit(this IResourceBuilder<TerraformCliResource> builder, params string[] args)
        => Select(builder, ["init", .. args]);

    /// <summary>Selects the plan command.</summary>
    /// <param name="builder">The CLI resource.</param>
    /// <param name="args">Additional command arguments.</param>
    /// <returns>The CLI resource.</returns>
    [AspireExport("withTerraformCliPlan", MethodName = "withPlan")]
    public static IResourceBuilder<TerraformCliResource> WithPlan(this IResourceBuilder<TerraformCliResource> builder, params string[] args)
        => Select(builder, ["plan", .. args]);

    /// <summary>Selects the apply command.</summary>
    /// <param name="builder">The CLI resource.</param>
    /// <param name="autoApprove">Whether to include <c>-auto-approve</c>.</param>
    /// <param name="args">Additional command arguments.</param>
    /// <returns>The CLI resource.</returns>
    [AspireExportIgnore(Reason = "Use the polyglot overload with arguments before optional settings.")]
    public static IResourceBuilder<TerraformCliResource> WithApply(this IResourceBuilder<TerraformCliResource> builder, bool autoApprove = false, params string[] args)
        => Select(builder, autoApprove ? ["apply", "-auto-approve", .. args] : ["apply", .. args]);

    /// <summary>Selects the destroy command.</summary>
    /// <param name="builder">The CLI resource.</param>
    /// <param name="autoApprove">Whether to include <c>-auto-approve</c>.</param>
    /// <param name="explicitStart">Whether to require an explicit local start.</param>
    /// <param name="args">Additional command arguments.</param>
    /// <returns>The CLI resource.</returns>
    [AspireExportIgnore(Reason = "Use the polyglot overload with arguments before optional settings.")]
    public static IResourceBuilder<TerraformCliResource> WithDestroy(this IResourceBuilder<TerraformCliResource> builder, bool autoApprove = false, bool explicitStart = true, params string[] args)
    {
        Select(builder, autoApprove ? ["destroy", "-auto-approve", .. args] : ["destroy", .. args]);
        return explicitStart ? builder.WithExplicitStart() : builder;
    }

    /// <summary>Selects the apply command.</summary>
    /// <param name="builder">The CLI resource.</param>
    /// <param name="args">Additional command arguments.</param>
    /// <param name="autoApprove">Whether to include <c>-auto-approve</c>.</param>
    /// <returns>The CLI resource.</returns>
    [AspireExport("withTerraformCliApply", MethodName = "withApply")]
    internal static IResourceBuilder<TerraformCliResource> WithApplyForExport(this IResourceBuilder<TerraformCliResource> builder, string[] args, bool autoApprove = false)
        => builder.WithApply(autoApprove, args);

    /// <summary>Selects the destroy command.</summary>
    /// <param name="builder">The CLI resource.</param>
    /// <param name="args">Additional command arguments.</param>
    /// <param name="autoApprove">Whether to include <c>-auto-approve</c>.</param>
    /// <param name="explicitStart">Whether to require an explicit local start.</param>
    /// <returns>The CLI resource.</returns>
    [AspireExport("withTerraformCliDestroy", MethodName = "withDestroy")]
    internal static IResourceBuilder<TerraformCliResource> WithDestroyForExport(this IResourceBuilder<TerraformCliResource> builder, string[] args, bool autoApprove = false, bool explicitStart = true)
        => builder.WithDestroy(autoApprove, explicitStart, args);

    private static IResourceBuilder<TerraformCliResource> Select(IResourceBuilder<TerraformCliResource> builder, string[] args)
    {
        ArgumentNullException.ThrowIfNull(builder);
        builder.Resource.SelectedArguments = args;
        return builder;
    }
}