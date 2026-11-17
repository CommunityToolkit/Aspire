// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Aspire.Hosting.Pipelines;
using Microsoft.Extensions.Logging;
using System.Diagnostics;

namespace Aspire.Hosting;

/// <summary>Provides pipeline steps for Terraform environments.</summary>
internal static class TerraformPipelineExtensions
{
    /// <summary>Registers a step for every command declared on the environment.</summary>
    /// <param name="builder">The environment builder.</param>
    /// <returns>The environment builder.</returns>
    /// <remarks>
    /// Steps are named terraform-COMMAND-RESOURCE and reuse the arguments and environment of the local command
    /// resources. Dependencies and global deploy/destroy attachment follow <see cref="TerraformPipelineOptions"/>.
    /// </remarks>
    internal static IResourceBuilder<TerraformEnvironmentResource> RegisterTerraformPipeline(
        this IResourceBuilder<TerraformEnvironmentResource> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);
        return builder.WithPipelineStepFactory(_ => CreateSteps(builder.Resource));
    }

    /// <summary>Creates pipeline steps for the commands declared on an environment.</summary>
    /// <param name="environment">The Terraform environment.</param>
    /// <returns>The configured pipeline steps.</returns>
    internal static PipelineStep[] CreateSteps(TerraformEnvironmentResource environment)
    {
        var options = environment.GetPipelineOptions();
        var steps = environment.Annotations.OfType<TerraformCommandAnnotation>()
            .ToDictionary(annotation => annotation.Command, annotation => CreateStep(environment, annotation));

        if (options.ChainSteps)
        {
            DependsOn(TerraformCommand.Plan, TerraformCommand.Init);
            DependsOn(TerraformCommand.Apply, steps.ContainsKey(TerraformCommand.Plan) ? TerraformCommand.Plan : TerraformCommand.Init);
            DependsOn(TerraformCommand.Destroy, TerraformCommand.Init);
        }

        if (options.RequiredByDeploy && steps.TryGetValue(TerraformCommand.Apply, out var apply))
        {
            apply.RequiredBy(WellKnownPipelineSteps.Deploy);
        }

        if (options.RequiredByDestroy && steps.TryGetValue(TerraformCommand.Destroy, out var destroy))
        {
            destroy.RequiredBy(WellKnownPipelineSteps.Destroy);
        }

        return steps.Values.ToArray();

        void DependsOn(TerraformCommand command, TerraformCommand dependency)
        {
            if (steps.TryGetValue(command, out var step) && steps.TryGetValue(dependency, out var dependencyStep))
            {
                step.DependsOn(dependencyStep);
            }
        }
    }

    private static PipelineStep CreateStep(TerraformEnvironmentResource environment, TerraformCommandAnnotation annotation)
    {
        var name = environment.Name;
        var description = annotation.Command switch
        {
            TerraformCommand.Init => $"Initialize Terraform environment {name}.",
            TerraformCommand.Plan => $"Plan Terraform changes for {name}.",
            TerraformCommand.Apply => $"Apply Terraform changes for {name}.",
            _ => $"Destroy infrastructure managed by Terraform environment {name}."
        };

        return new PipelineStep
        {
            Name = $"terraform-{annotation.Command.ToString().ToLowerInvariant()}-{name}",
            Description = description,
            Resource = environment,
            Action = async context =>
            {
                var planPath = environment.GetPlanPath();
                if (annotation.Command == TerraformCommand.Plan && Path.GetDirectoryName(planPath) is { Length: > 0 } directory)
                {
                    Directory.CreateDirectory(directory);
                }
                else if (annotation.Command == TerraformCommand.Apply && planPath is not null && !File.Exists(planPath))
                {
                    throw new DistributedApplicationException(
                        $"Saved Terraform plan '{planPath}' for '{name}' was not found. Run terraform-plan-{name} first or provide the plan artifact.");
                }

                var (variables, redactions) = await ResolveEnvironmentAsync(annotation.Resource, context).ConfigureAwait(false);
                var arguments = await ResolveArgumentsAsync(annotation.Resource, context).ConfigureAwait(false);
                arguments.Insert(1, "-no-color");
                if (annotation.Command == TerraformCommand.Plan)
                {
                    arguments.Insert(2, "-detailed-exitcode");
                }

                await RunAsync(environment, annotation.Resource, [.. arguments], variables, redactions, context,
                    allowChangesExitCode: annotation.Command == TerraformCommand.Plan).ConfigureAwait(false);
            }
        };
    }

    private static async Task<List<string>> ResolveArgumentsAsync(
        TerraformCliResource resource, PipelineStepContext context)
    {
        var values = new List<object>();
        var callbackContext = new CommandLineArgsCallbackContext(values, resource, context.CancellationToken)
        {
            ExecutionContext = context.ExecutionContext,
            Logger = context.Logger
        };
        foreach (var annotation in resource.Annotations.OfType<CommandLineArgsCallbackAnnotation>())
        {
            await annotation.Callback(callbackContext).ConfigureAwait(false);
        }

        var arguments = new List<string>();
        foreach (var value in values)
        {
            var resolved = value switch
            {
                string literal => literal,
                IValueProvider provider => await provider.GetValueAsync(context.CancellationToken).ConfigureAwait(false),
                _ => throw new InvalidOperationException($"Unsupported Terraform argument for '{resource.Name}'.")
            };
            arguments.Add(resolved
                ?? throw new InvalidOperationException($"A Terraform argument for '{resource.Name}' could not be resolved."));
        }

        return arguments;
    }

    private static async Task<(Dictionary<string, string> Variables, string[] Redactions)> ResolveEnvironmentAsync(
        TerraformCliResource resource, PipelineStepContext context)
    {
        var values = new Dictionary<string, object>();
        var callbackContext = new EnvironmentCallbackContext(context.ExecutionContext, resource, values, context.CancellationToken)
        {
            Logger = context.Logger
        };
        foreach (var annotation in resource.Annotations.OfType<EnvironmentCallbackAnnotation>())
        {
            await annotation.Callback(callbackContext).ConfigureAwait(false);
        }

        var environment = new Dictionary<string, string>();
        var redactions = new HashSet<string>(StringComparer.Ordinal);
        var visited = new HashSet<object>(ReferenceEqualityComparer.Instance);
        foreach (var (key, value) in values)
        {
            var resolved = value switch
            {
                string literal => literal,
                IValueProvider provider => await provider.GetValueAsync(context.CancellationToken).ConfigureAwait(false),
                _ => throw new InvalidOperationException($"Unsupported Terraform environment value for '{key}'.")
            };
            environment[key] = resolved
                ?? throw new InvalidOperationException($"Terraform environment variable '{key}' could not be resolved.");
            await AddSecretReferencesAsync(value).ConfigureAwait(false);
        }

        environment["TF_IN_AUTOMATION"] = "true";
        environment["TF_INPUT"] = "false";
        foreach (var (key, value) in environment)
        {
            if (IsSensitiveKey(key) && !string.IsNullOrEmpty(value))
            {
                redactions.Add(value);
            }
        }

        return (environment, redactions.OrderByDescending(value => value.Length).ToArray());

        async Task AddSecretReferencesAsync(object value)
        {
            if (!visited.Add(value))
            {
                return;
            }
            if (value is ParameterResource { Secret: true } parameter)
            {
                var secret = await parameter.GetValueAsync(context.CancellationToken).ConfigureAwait(false);
                if (!string.IsNullOrEmpty(secret))
                {
                    redactions.Add(secret);
                }
            }
            if (value is IValueWithReferences references)
            {
                foreach (var reference in references.References)
                {
                    await AddSecretReferencesAsync(reference).ConfigureAwait(false);
                }
            }
        }
    }

    private static bool IsSensitiveKey(string key)
        => key.Contains("PASSWORD", StringComparison.OrdinalIgnoreCase)
            || key.Contains("SECRET", StringComparison.OrdinalIgnoreCase)
            || key.Contains("TOKEN", StringComparison.OrdinalIgnoreCase)
            || key.Contains("CONN_STR", StringComparison.OrdinalIgnoreCase)
            || key.Contains("ACCESS_KEY", StringComparison.OrdinalIgnoreCase);

    private static async Task RunAsync(TerraformEnvironmentResource resource, TerraformCliResource command,
        string[] arguments, Dictionary<string, string> environment, string[] redactions, PipelineStepContext context, bool allowChangesExitCode = false)
    {
        if (!Directory.Exists(command.WorkingDirectory))
        {
            throw new DistributedApplicationException($"Terraform module directory does not exist: {command.WorkingDirectory}");
        }

        context.CancellationToken.ThrowIfCancellationRequested();
        var startInfo = new ProcessStartInfo(command.Command)
        {
            WorkingDirectory = command.WorkingDirectory,
            UseShellExecute = false,
            RedirectStandardInput = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true
        };
        foreach (var argument in arguments)
        {
            startInfo.ArgumentList.Add(argument);
        }

        foreach (var (key, value) in environment)
        {
            startInfo.Environment[key] = value;
        }

        redactions = redactions.Concat(startInfo.Environment
            .Where(item => IsSensitiveKey(item.Key) && !string.IsNullOrEmpty(item.Value))
            .Select(item => item.Value!)).Distinct(StringComparer.Ordinal)
            .OrderByDescending(value => value.Length).ToArray();
        var operationContext = RedactOutput(
            $"Terraform {arguments[0]} for '{resource.Name}'. Module: {command.WorkingDirectory}. Executable: {command.Command}.", redactions);
        context.Logger.LogInformation("{TerraformContext}", operationContext);
        using var process = new Process { StartInfo = startInfo };
        process.Start();
        process.StandardInput.Close();
        var output = LogOutputAsync(process.StandardOutput, context.Logger, redactions);
        var error = LogOutputAsync(process.StandardError, context.Logger, redactions);
        try
        {
            await process.WaitForExitAsync(context.CancellationToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            if (!process.HasExited)
            {
                process.Kill(entireProcessTree: true);
            }

            await process.WaitForExitAsync(CancellationToken.None).ConfigureAwait(false);
            throw;
        }
        finally
        {
            await Task.WhenAll(output, error).ConfigureAwait(false);
        }

        if (process.ExitCode != 0 && !(allowChangesExitCode && process.ExitCode == 2))
        {
            throw new DistributedApplicationException(
                $"{operationContext}{Environment.NewLine}Terraform failed with exit code {process.ExitCode}.{Environment.NewLine}" +
                $"Last stdout:{Environment.NewLine}{await output.ConfigureAwait(false)}{Environment.NewLine}" +
                $"Last stderr:{Environment.NewLine}{await error.ConfigureAwait(false)}");
        }
    }

    private static async Task<string> LogOutputAsync(StreamReader reader, ILogger logger, string[] redactions)
    {
        var recentOutput = new Queue<string>();
        while (await reader.ReadLineAsync().ConfigureAwait(false) is { } line)
        {
            var output = RedactOutput(line, redactions);
            if (recentOutput.Count == 20)
            {
                recentOutput.Dequeue();
            }

            recentOutput.Enqueue(output.Length > 2000 ? output[..2000] + " [truncated]" : output);
            var message = output.TrimStart();
            var level = message.StartsWith("Warning:", StringComparison.OrdinalIgnoreCase)
                ? LogLevel.Warning
                : message.StartsWith("Error:", StringComparison.OrdinalIgnoreCase)
                    ? LogLevel.Error
                    : LogLevel.Information;
            logger.Log(level, "{TerraformOutput}", output);
        }

        return recentOutput.Count == 0 ? "(no output captured)" : string.Join(Environment.NewLine, recentOutput);
    }

    private static string RedactOutput(string line, string[] redactions)
    {
        foreach (var value in redactions)
        {
            line = line.Replace(value, "[REDACTED]", StringComparison.Ordinal);
        }

        return line;
    }
}