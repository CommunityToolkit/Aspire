// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace Aspire.Hosting;

/// <summary>Configuration shared by Terraform environments and CLI resources.</summary>
public static class TerraformConfigurationExtensions
{
    /// <summary>Sets a Terraform variable from a literal.</summary>
    /// <typeparam name="TResource">The Terraform resource type.</typeparam>
    /// <param name="builder">The Terraform resource builder.</param>
    /// <param name="name">The variable name without the TF_VAR_ prefix.</param>
    /// <param name="value">The variable value.</param>
    /// <returns>The resource builder.</returns>
    [AspireExportIgnore(Reason = "Use the union-shaped withTfVar export.")]
    public static IResourceBuilder<TResource> WithTfVar<TResource>(this IResourceBuilder<TResource> builder, string name, string value)
        where TResource : ITerraformConfigurableResource
    {
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        ArgumentNullException.ThrowIfNull(value);
        return builder.WithEnvironment($"TF_VAR_{name}", value);
    }

    /// <summary>Sets a Terraform variable from a deferred Aspire value.</summary>
    /// <typeparam name="TResource">The Terraform resource type.</typeparam>
    /// <typeparam name="TValue">The value provider type.</typeparam>
    /// <param name="builder">The Terraform resource builder.</param>
    /// <param name="name">The variable name without the TF_VAR_ prefix.</param>
    /// <param name="value">The value provider.</param>
    /// <returns>The resource builder.</returns>
    [AspireExportIgnore(Reason = "Use the union-shaped withTfVar export.")]
    public static IResourceBuilder<TResource> WithTfVar<TResource, TValue>(this IResourceBuilder<TResource> builder, string name, TValue value)
        where TResource : ITerraformConfigurableResource
        where TValue : IValueProvider, IManifestExpressionProvider
    {
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        ArgumentNullException.ThrowIfNull(value);
        return builder.WithEnvironment($"TF_VAR_{name}", value);
    }

    /// <summary>Sets a Terraform variable from an Aspire parameter.</summary>
    /// <typeparam name="TResource">The Terraform resource type.</typeparam>
    /// <param name="builder">The Terraform resource builder.</param>
    /// <param name="name">The variable name without the TF_VAR_ prefix.</param>
    /// <param name="value">The parameter builder.</param>
    /// <returns>The resource builder.</returns>
    [AspireExportIgnore(Reason = "Use the union-shaped withTfVar export.")]
    public static IResourceBuilder<TResource> WithTfVar<TResource>(this IResourceBuilder<TResource> builder, string name, IResourceBuilder<ParameterResource> value)
        where TResource : ITerraformConfigurableResource
    {
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        ArgumentNullException.ThrowIfNull(value);
        return builder.WithEnvironment($"TF_VAR_{name}", value);
    }

    /// <summary>Loads Terraform variables from a file for plan, apply, and destroy.</summary>
    /// <typeparam name="TResource">The Terraform resource type.</typeparam>
    /// <param name="builder">The Terraform resource builder.</param>
    /// <param name="path">The path relative to the AppHost directory or an absolute path.</param>
    /// <returns>The resource builder.</returns>
    [AspireExport]
    public static IResourceBuilder<TResource> WithTfVarsFile<TResource>(this IResourceBuilder<TResource> builder, string path)
        where TResource : ITerraformConfigurableResource
    {
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        return AppendArgument(builder, ["plan", "apply", "destroy"], "-var-file=", Path.GetFullPath(path, builder.ApplicationBuilder.AppHostDirectory), true);
    }

    /// <summary>Configures an init backend value.</summary>
    /// <typeparam name="TResource">The Terraform resource type.</typeparam>
    /// <param name="builder">The Terraform resource builder.</param>
    /// <param name="key">The backend key.</param>
    /// <param name="value">The backend value.</param>
    /// <returns>The resource builder.</returns>
    [AspireExportIgnore(Reason = "Use the union-shaped withBackendConfig export.")]
    public static IResourceBuilder<TResource> WithBackendConfig<TResource>(this IResourceBuilder<TResource> builder, string key, string value)
        where TResource : ITerraformConfigurableResource => BackendConfig(builder, key, value);

    /// <summary>Configures an init backend value from an Aspire expression.</summary>
    /// <typeparam name="TResource">The Terraform resource type.</typeparam>
    /// <param name="builder">The Terraform resource builder.</param>
    /// <param name="key">The backend key.</param>
    /// <param name="value">The backend value expression.</param>
    /// <returns>The resource builder.</returns>
    [AspireExportIgnore(Reason = "Use the union-shaped withBackendConfig export.")]
    public static IResourceBuilder<TResource> WithBackendConfig<TResource>(this IResourceBuilder<TResource> builder, string key, ReferenceExpression value)
        where TResource : ITerraformConfigurableResource => BackendConfig(builder, key, value);

    /// <summary>Configures an init backend value from an Aspire parameter.</summary>
    /// <typeparam name="TResource">The Terraform resource type.</typeparam>
    /// <param name="builder">The Terraform resource builder.</param>
    /// <param name="key">The backend key.</param>
    /// <param name="value">The backend value parameter.</param>
    /// <returns>The resource builder.</returns>
    [AspireExportIgnore(Reason = "Use the union-shaped withBackendConfig export.")]
    public static IResourceBuilder<TResource> WithBackendConfig<TResource>(this IResourceBuilder<TResource> builder, string key, IResourceBuilder<ParameterResource> value)
        where TResource : ITerraformConfigurableResource
    {
        ArgumentNullException.ThrowIfNull(value);
        return BackendConfig(builder, key, value.Resource);
    }

    /// <summary>Loads Terraform backend configuration from a file.</summary>
    /// <typeparam name="TResource">The Terraform resource type.</typeparam>
    /// <param name="builder">The Terraform resource builder.</param>
    /// <param name="path">The path relative to the AppHost directory or an absolute path.</param>
    /// <returns>The resource builder.</returns>
    [AspireExport]
    public static IResourceBuilder<TResource> WithBackendConfigFile<TResource>(this IResourceBuilder<TResource> builder, string path)
        where TResource : ITerraformConfigurableResource
    {
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        return AppendArgument(builder, ["init"], "-backend-config=", Path.GetFullPath(path, builder.ApplicationBuilder.AppHostDirectory), false);
    }

    /// <summary>Configures the PostgreSQL backend environment.</summary>
    /// <typeparam name="TResource">The Terraform resource type.</typeparam>
    /// <param name="builder">The Terraform resource builder.</param>
    /// <param name="connectionString">The backend connection string.</param>
    /// <param name="schema">The optional backend schema.</param>
    /// <returns>The resource builder.</returns>
    [AspireExport]
    public static IResourceBuilder<TResource> WithPostgresBackend<TResource>(this IResourceBuilder<TResource> builder, ReferenceExpression connectionString, string? schema = null)
        where TResource : ITerraformConfigurableResource
    {
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentNullException.ThrowIfNull(connectionString);
        builder.WithEnvironment("PG_CONN_STR", connectionString);
        if (schema is not null)
        {
            builder.WithEnvironment("PG_SCHEMA_NAME", schema);
        }
        return builder;
    }

    /// <summary>Sets a Terraform variable from a literal, parameter, endpoint, or expression.</summary>
    /// <typeparam name="TResource">The Terraform resource type.</typeparam>
    /// <param name="builder">The Terraform resource builder.</param>
    /// <param name="name">The Terraform variable name.</param>
    /// <param name="value">The variable value.</param>
    /// <returns>The resource builder.</returns>
    [AspireExport("withTfVar")]
    internal static IResourceBuilder<TResource> WithTfVarForExport<TResource>(this IResourceBuilder<TResource> builder, string name,
        [AspireUnion(typeof(string), typeof(IResourceBuilder<ParameterResource>), typeof(EndpointReference), typeof(EndpointReferenceExpression), typeof(ReferenceExpression))] object value)
        where TResource : ITerraformConfigurableResource => value switch
        {
            string literal => builder.WithTfVar(name, literal),
            IResourceBuilder<ParameterResource> parameter => builder.WithTfVar(name, parameter),
            EndpointReference endpoint => builder.WithTfVar(name, endpoint),
            EndpointReferenceExpression expression => builder.WithTfVar(name, expression),
            ReferenceExpression expression => builder.WithTfVar(name, expression),
            _ => throw new ArgumentException("Expected a string, parameter, endpoint, or reference expression.", nameof(value))
        };

    /// <summary>Configures an init backend value.</summary>
    /// <typeparam name="TResource">The Terraform resource type.</typeparam>
    /// <param name="builder">The Terraform resource builder.</param>
    /// <param name="key">The backend key.</param>
    /// <param name="value">The backend value.</param>
    /// <returns>The resource builder.</returns>
    [AspireExport("withBackendConfig")]
    internal static IResourceBuilder<TResource> WithBackendConfigForExport<TResource>(this IResourceBuilder<TResource> builder, string key,
        [AspireUnion(typeof(string), typeof(IResourceBuilder<ParameterResource>), typeof(ReferenceExpression))] object value)
        where TResource : ITerraformConfigurableResource => value switch
        {
            string literal => builder.WithBackendConfig(key, literal),
            IResourceBuilder<ParameterResource> parameter => builder.WithBackendConfig(key, parameter),
            ReferenceExpression expression => builder.WithBackendConfig(key, expression),
            _ => throw new ArgumentException("Expected a string, parameter, or reference expression.", nameof(value))
        };

    private static IResourceBuilder<TResource> BackendConfig<TResource>(IResourceBuilder<TResource> builder, string key, object value)
        where TResource : ITerraformConfigurableResource
    {
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentException.ThrowIfNullOrWhiteSpace(key);
        ArgumentNullException.ThrowIfNull(value);
        return AppendArgument(builder, ["init"], $"-backend-config={key}=", value, false);
    }

    private static IResourceBuilder<TResource> AppendArgument<TResource>(IResourceBuilder<TResource> builder, string[] commands, string prefix, object value, bool variableFile)
        where TResource : ITerraformConfigurableResource
    {
        var argument = new TerraformCliArgument(prefix, value);
        return builder.WithEnvironment(context =>
        {
            foreach (var command in commands)
            {
                if (variableFile && command == "apply" && context.Resource is TerraformCliResource
                    { Operation: TerraformCommand.Apply, Environment: { } environment } && environment.GetPlanPath() is not null)
                {
                    continue;
                }

                var key = $"TF_CLI_ARGS_{command}";
                var expression = new ReferenceExpressionBuilder();
                if (context.EnvironmentVariables.TryGetValue(key, out var existing))
                {
                    switch (existing)
                    {
                        case string literal:
                            expression.AppendLiteral(literal);
                            break;
                        case IValueProvider and IManifestExpressionProvider:
                            expression.AppendValueProvider(existing);
                            break;
                        default:
                            throw new InvalidOperationException($"Unsupported value for Terraform environment variable '{key}'.");
                    }
                    expression.AppendLiteral(" ");
                }
                expression.AppendValueProvider(argument);
                context.EnvironmentVariables[key] = expression.Build();
            }
        });
    }
}
