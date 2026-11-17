// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace Aspire.Hosting;

/// <summary>Provides builders for Terraform environments.</summary>
public static class TerraformEnvironmentExtensions
{
    /// <summary>Adds a logical Terraform environment and registers its pipeline.</summary>
    /// <remarks>
    /// The environment runs no process. Declare commands with <see cref="WithInit"/>, <see cref="WithPlan"/>,
    /// <see cref="WithApply"/>, and <see cref="WithDestroy"/>; only declared commands get resources and pipeline steps.
    /// </remarks>
    /// <param name="builder">The application builder.</param>
    /// <param name="name">The resource name.</param>
    /// <param name="workingDirectory">The module directory, relative to the AppHost directory or absolute.</param>
    /// <returns>The environment builder.</returns>
    [AspireExport]
    public static IResourceBuilder<TerraformEnvironmentResource> AddTerraformEnvironment(
        this IDistributedApplicationBuilder builder, [ResourceName] string name, string workingDirectory)
    {
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        ArgumentException.ThrowIfNullOrWhiteSpace(workingDirectory);

        var environment = builder.AddResource(new TerraformEnvironmentResource(name,
                Path.GetFullPath(workingDirectory, builder.AppHostDirectory)))
            .WithEnvironment("TF_IN_AUTOMATION", "true")
            .WithEnvironment("TF_INPUT", "false")
            .ExcludeFromManifest();

        return environment.RegisterTerraformPipeline();
    }

    /// <summary>Sets the Terraform executable name or path used by local resources and pipeline steps.</summary>
    /// <param name="builder">The Terraform environment builder.</param>
    /// <param name="terraformExecutablePath">The executable name or path.</param>
    /// <returns>The environment builder.</returns>
    [AspireExport]
    public static IResourceBuilder<TerraformEnvironmentResource> WithTerraformExecutablePath(
        this IResourceBuilder<TerraformEnvironmentResource> builder, string terraformExecutablePath)
    {
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentException.ThrowIfNullOrWhiteSpace(terraformExecutablePath);
        builder.Resource.TerraformExecutablePath = terraformExecutablePath;
        foreach (var annotation in builder.Resource.Annotations.OfType<TerraformCommandAnnotation>())
        {
            builder.ApplicationBuilder.CreateResourceBuilder(annotation.Resource).WithCommand(terraformExecutablePath);
        }

        return builder;
    }

    /// <summary>Selects an existing Terraform workspace for all commands in the environment.</summary>
    /// <param name="builder">The environment builder.</param>
    /// <param name="workspace">The existing workspace name; workspaces are not created automatically.</param>
    /// <returns>The environment builder.</returns>
    [AspireExport]
    public static IResourceBuilder<TerraformEnvironmentResource> WithWorkspace(
        this IResourceBuilder<TerraformEnvironmentResource> builder, string workspace)
    {
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentException.ThrowIfNullOrWhiteSpace(workspace);
        builder.Resource.Workspace = workspace;
        return builder.WithEnvironment("TF_WORKSPACE", workspace);
    }

    /// <summary>Declares <c>terraform init</c>, adding the <c>{name}-init</c> resource and pipeline step.</summary>
    /// <remarks>
    /// Arguments added with <c>WithArgs</c> in <paramref name="configure"/> are used both locally and in the pipeline.
    /// Calling the method again reuses the existing resource.
    /// </remarks>
    /// <param name="builder">The environment builder.</param>
    /// <param name="configure">Optional configuration of the command resource.</param>
    /// <returns>The environment builder.</returns>
    [AspireExport("withTerraformEnvironmentInit", MethodName = "withInit", RunSyncOnBackgroundThread = true)]
    public static IResourceBuilder<TerraformEnvironmentResource> WithInit(
        this IResourceBuilder<TerraformEnvironmentResource> builder,
        Action<IResourceBuilder<TerraformCliResource>>? configure = null)
        => builder.WithTerraformCommand(TerraformCommand.Init, configure);

    /// <summary>Declares <c>terraform plan</c>, saving the plan consumed by <see cref="WithApply"/>.</summary>
    /// <remarks>
    /// The plan is saved with <c>-out</c>. When a plan is declared, apply executes the saved plan instead of
    /// recalculating changes, both locally and in the pipeline.
    /// </remarks>
    /// <param name="builder">The environment builder.</param>
    /// <param name="outputPath">
    /// The saved plan path, relative to the AppHost directory or absolute. If omitted, an existing path is retained;
    /// otherwise it defaults to <c>{name}.tfplan</c> in the module.
    /// </param>
    /// <param name="configure">Optional configuration of the command resource.</param>
    /// <returns>The environment builder.</returns>
    [AspireExport("withTerraformEnvironmentPlan", MethodName = "withPlan", RunSyncOnBackgroundThread = true)]
    public static IResourceBuilder<TerraformEnvironmentResource> WithPlan(
        this IResourceBuilder<TerraformEnvironmentResource> builder,
        string? outputPath = null,
        Action<IResourceBuilder<TerraformCliResource>>? configure = null)
    {
        ArgumentNullException.ThrowIfNull(builder);
        var path = outputPath is null
            ? builder.Resource.GetPlanPath() ?? Path.Combine(builder.Resource.WorkingDirectory, $"{builder.Resource.Name}.tfplan")
            : Path.GetFullPath(outputPath, builder.ApplicationBuilder.AppHostDirectory);
        builder.WithAnnotation(new TerraformPlanAnnotation(path), ResourceAnnotationMutationBehavior.Replace);
        return builder.WithTerraformCommand(TerraformCommand.Plan, configure);
    }

    /// <summary>Declares <c>terraform apply</c>, adding the <c>{name}-apply</c> resource and pipeline step.</summary>
    /// <remarks>
    /// Applies the saved plan when <see cref="WithPlan"/> is declared; otherwise runs with <c>-auto-approve</c>.
    /// The local resource starts automatically after its prerequisites unless configured with explicit start.
    /// </remarks>
    /// <param name="builder">The environment builder.</param>
    /// <param name="configure">Optional configuration of the command resource.</param>
    /// <returns>The environment builder.</returns>
    [AspireExport("withTerraformEnvironmentApply", MethodName = "withApply", RunSyncOnBackgroundThread = true)]
    public static IResourceBuilder<TerraformEnvironmentResource> WithApply(
        this IResourceBuilder<TerraformEnvironmentResource> builder,
        Action<IResourceBuilder<TerraformCliResource>>? configure = null)
        => builder.WithTerraformCommand(TerraformCommand.Apply, configure);

    /// <summary>Declares <c>terraform destroy -auto-approve</c>, adding the <c>{name}-destroy</c> resource and step.</summary>
    /// <remarks>The local resource always requires explicit start.</remarks>
    /// <param name="builder">The environment builder.</param>
    /// <param name="configure">Optional configuration of the command resource.</param>
    /// <returns>The environment builder.</returns>
    [AspireExport("withTerraformEnvironmentDestroy", MethodName = "withDestroy", RunSyncOnBackgroundThread = true)]
    public static IResourceBuilder<TerraformEnvironmentResource> WithDestroy(
        this IResourceBuilder<TerraformEnvironmentResource> builder,
        Action<IResourceBuilder<TerraformCliResource>>? configure = null)
        => builder.WithTerraformCommand(TerraformCommand.Destroy, configure);

    /// <summary>Configures how the environment steps are attached to the Aspire pipeline.</summary>
    /// <param name="builder">The environment builder.</param>
    /// <param name="configure">Updates the current options.</param>
    /// <returns>The environment builder.</returns>
    [AspireExport(RunSyncOnBackgroundThread = true)]
    public static IResourceBuilder<TerraformEnvironmentResource> WithPipelineOptions(
        this IResourceBuilder<TerraformEnvironmentResource> builder, Action<TerraformPipelineOptions> configure)
    {
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentNullException.ThrowIfNull(configure);
        configure(builder.Resource.GetPipelineOptions());
        return builder;
    }

    /// <summary>Gets or creates the pipeline options for an environment.</summary>
    /// <param name="resource">The Terraform environment.</param>
    /// <returns>The environment's pipeline options.</returns>
    internal static TerraformPipelineOptions GetPipelineOptions(this TerraformEnvironmentResource resource)
    {
        if (resource.Annotations.OfType<TerraformPipelineOptionsAnnotation>().LastOrDefault() is { } existing)
        {
            return existing.Options;
        }

        var options = new TerraformPipelineOptions();
        resource.Annotations.Add(new TerraformPipelineOptionsAnnotation(options));
        return options;
    }

    /// <summary>Gets the resource for a declared Terraform command.</summary>
    /// <param name="resource">The Terraform environment.</param>
    /// <param name="command">The command to find.</param>
    /// <returns>The command resource, or <see langword="null"/> if it has not been declared.</returns>
    internal static TerraformCliResource? GetCommandResource(
        this TerraformEnvironmentResource resource, TerraformCommand command)
        => resource.Annotations.OfType<TerraformCommandAnnotation>()
            .FirstOrDefault(annotation => annotation.Command == command)?.Resource;

    /// <summary>Gets the path of the saved plan for an environment.</summary>
    /// <param name="resource">The Terraform environment.</param>
    /// <returns>The plan path, or <see langword="null"/> if no plan has been declared.</returns>
    internal static string? GetPlanPath(this TerraformEnvironmentResource resource)
        => resource.Annotations.OfType<TerraformPlanAnnotation>().LastOrDefault()?.OutputPath;

    /// <summary>Gets the arguments computed by the integration, before user arguments.</summary>
    /// <param name="resource">The Terraform environment.</param>
    /// <param name="command">The command to execute.</param>
    /// <returns>The command and its integration-provided arguments.</returns>
    internal static List<object> GetBaseArguments(this TerraformEnvironmentResource resource, TerraformCommand command)
    {
        var planPath = resource.GetPlanPath();
        return command switch
        {
            TerraformCommand.Init => ["init", "-input=false"],
            TerraformCommand.Plan => ["plan", "-input=false", $"-out={planPath}"],
            TerraformCommand.Apply when planPath is not null => ["apply", "-input=false"],
            TerraformCommand.Apply => ["apply", "-input=false", "-auto-approve"],
            TerraformCommand.Destroy => ["destroy", "-input=false", "-auto-approve"],
            _ => throw new ArgumentOutOfRangeException(nameof(command))
        };
    }

    private static IResourceBuilder<TerraformEnvironmentResource> WithTerraformCommand(
        this IResourceBuilder<TerraformEnvironmentResource> builder, TerraformCommand command,
        Action<IResourceBuilder<TerraformCliResource>>? configure)
    {
        ArgumentNullException.ThrowIfNull(builder);
        var environment = builder.Resource;
        var resource = environment.GetCommandResource(command);
        var commandBuilder = resource is null
            ? builder.AddCommandResource(command)
            : builder.ApplicationBuilder.CreateResourceBuilder(resource);

        WireLocalDependencies(builder);
        if (command == TerraformCommand.Apply && commandBuilder.Resource.SavedPlanArgsAnnotation is { } previous)
        {
            commandBuilder.Resource.Annotations.Remove(previous);
        }
        configure?.Invoke(commandBuilder);
        if (command == TerraformCommand.Apply)
        {
            commandBuilder.WithArgs(context =>
            {
                if (environment.GetPlanPath() is { } planPath)
                {
                    context.Args.Add(planPath);
                }
            });
            commandBuilder.Resource.SavedPlanArgsAnnotation = commandBuilder.Resource.Annotations.OfType<CommandLineArgsCallbackAnnotation>().Last();
        }
        return builder;
    }

    private static IResourceBuilder<TerraformCliResource> AddCommandResource(
        this IResourceBuilder<TerraformEnvironmentResource> builder, TerraformCommand command)
    {
        var environment = builder.Resource;
        var commandBuilder = builder.ApplicationBuilder
            .AddTerraformCli($"{environment.Name}-{command.ToString().ToLowerInvariant()}", environment.WorkingDirectory)
            .WithCommand(environment.TerraformExecutablePath)
            .WithParentRelationship(environment)
            .ExcludeFromManifest()
            .WithEnvironment(async context =>
            {
                foreach (var annotation in environment.Annotations.OfType<EnvironmentCallbackAnnotation>())
                {
                    await annotation.Callback(context).ConfigureAwait(false);
                }

            })
            .WithArgs(context =>
            {
                if (command == TerraformCommand.Plan && context.ExecutionContext.IsRunMode &&
                    Path.GetDirectoryName(environment.GetPlanPath()) is { Length: > 0 } directory)
                {
                    Directory.CreateDirectory(directory);
                }

                foreach (var argument in environment.GetBaseArguments(command))
                {
                    context.Args.Add(argument);
                }
            });

        commandBuilder.Resource.Environment = environment;
        commandBuilder.Resource.Operation = command;

        if (command == TerraformCommand.Destroy)
        {
            commandBuilder.WithExplicitStart();
        }

        builder.WithAnnotation(new TerraformCommandAnnotation(command, commandBuilder.Resource));
        return commandBuilder;
    }

    private static void WireLocalDependencies(IResourceBuilder<TerraformEnvironmentResource> builder)
    {
        var environment = builder.Resource;
        var init = environment.GetCommandResource(TerraformCommand.Init);
        var plan = environment.GetCommandResource(TerraformCommand.Plan);
        WaitFor(environment.GetCommandResource(TerraformCommand.Plan), init);
        WaitFor(environment.GetCommandResource(TerraformCommand.Apply), plan ?? init);
        WaitFor(environment.GetCommandResource(TerraformCommand.Destroy), init);

        void WaitFor(TerraformCliResource? resource, TerraformCliResource? dependency)
        {
            if (resource is null || dependency is null ||
                resource.Annotations.OfType<WaitAnnotation>().Any(wait => wait.Resource == dependency))
            {
                return;
            }

            builder.ApplicationBuilder.CreateResourceBuilder(resource)
                .WaitForCompletion(builder.ApplicationBuilder.CreateResourceBuilder(dependency));
        }
    }
}