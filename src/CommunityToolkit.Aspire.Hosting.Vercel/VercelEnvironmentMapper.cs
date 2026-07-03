#pragma warning disable ASPIRECOMPUTE003

using Aspire.Hosting.ApplicationModel;
using Aspire.Hosting.Publishing;
using Aspire.Hosting;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;

namespace CommunityToolkit.Aspire.Hosting.Vercel;

internal static class VercelEnvironmentMapper
{
    public static async Task<VercelEnvironmentConfiguration> GetConfigurationAsync(
        IResource resource,
        VercelEnvironmentOptionsAnnotation options,
        IExecutionConfigurationResult executionConfiguration,
        IReadOnlyDictionary<string, VercelDeploymentEntry> entriesByResourceName,
        bool resolveProjectEnvironmentVariableValues,
        CancellationToken cancellationToken)
    {
        List<KeyValuePair<string, string>> deploymentEnvironmentVariables = [];
        List<KeyValuePair<string, string>> projectEnvironmentVariables = [];
        HashSet<string> names = new(StringComparer.Ordinal);

        // This is the env-var edge-case boundary. Vercel has deployment env args and
        // project env secrets, but no Aspire-style connection binding/service-discovery
        // object in this preview. Only deterministic production endpoint URLs are projected;
        // other resource references fail here instead of becoming misleading strings.
        // Keep a single pass over Aspire's unprocessed environment dictionary. The processed
        // value can contain publish-mode manifest expressions, but deployment needs the
        // original graph value so it can choose Vercel's concrete URL/secret mechanism.
        foreach (var environmentVariable in executionConfiguration.EnvironmentVariablesWithUnprocessed)
        {
            string name = environmentVariable.Key;
            object unprocessedValue = environmentVariable.Value.Item1;
            string value = environmentVariable.Value.Item2;

            ValidateEnvironmentVariableName(resource, name);
            if (!names.Add(name))
            {
                throw new DistributedApplicationException(
                    $"Resource '{resource.Name}' configures environment variable '{name}' more than once. Vercel project environment variable names must be unique.");
            }

            if (ContainsUnsupportedResourceReference(resource, unprocessedValue))
            {
                throw new DistributedApplicationException(
                    $"Environment variable '{name}' for resource '{resource.Name}' references another Aspire resource or service in a way that cannot be represented as a Vercel deployment URL. Use endpoint references to Vercel production workloads, or configure the value in Vercel project environment variables.");
            }

            // Non-secrets can ride on `vercel deploy --env`; secret-bearing values must use
            // Vercel project environment variables so values never appear in CLI arguments.
            bool containsSecret = ContainsSecretReference(unprocessedValue);
            if (containsSecret)
            {
                value = resolveProjectEnvironmentVariableValues
                    ? await GetProjectEnvironmentVariableValueAsync(
                        resource,
                        options,
                        entriesByResourceName,
                        unprocessedValue,
                        value,
                        cancellationToken).ConfigureAwait(false)
                    : "<value>";
            }
            else if (TryGetEnvironmentVariableValue(resource, options, entriesByResourceName, unprocessedValue, out string? vercelValue))
            {
                value = vercelValue;
            }

            if (containsSecret)
            {
                projectEnvironmentVariables.Add(new(name, value));
            }
            else
            {
                deploymentEnvironmentVariables.Add(new(name, value));
            }
        }

        return new(deploymentEnvironmentVariables, projectEnvironmentVariables);
    }

    public static void ValidateUnsupportedRuntimeConfiguration(
        IResource resource,
        IExecutionConfigurationResult executionConfiguration)
    {
        // These Aspire concepts have no faithful Vercel Dockerfile-deploy equivalent in this
        // preview. Rejecting them is safer than silently dropping entrypoint, args, or build
        // values that would change the workload's deployed behavior. Aspire's built-in
        // image build/push pipeline owns build-time Docker options; this validation only
        // rejects runtime concepts the Vercel Build Output API path cannot preserve.
        if (resource is ContainerResource { Entrypoint: not null })
        {
            throw new DistributedApplicationException(
                $"Resource '{resource.Name}' configures a container entrypoint, but Vercel Dockerfile deployments use the CMD/ENTRYPOINT from Aspire's publish output. Configure the workload's publish behavior or Vercel project settings instead.");
        }

        if (executionConfiguration.ArgumentsWithUnprocessed.Any())
        {
            throw new DistributedApplicationException(
                $"Resource '{resource.Name}' configures Aspire command-line arguments, but Vercel Dockerfile deployments cannot override Docker CMD/ENTRYPOINT. Configure the workload's publish behavior or express the values as environment variables.");
        }
    }

    private static async ValueTask<string> GetProjectEnvironmentVariableValueAsync(
        IResource resource,
        VercelEnvironmentOptionsAnnotation options,
        IReadOnlyDictionary<string, VercelDeploymentEntry> entriesByResourceName,
        object? value,
        string processedValue,
        CancellationToken cancellationToken)
    {
        // This path resolves values for `vercel env add`, not `vercel deploy --env`.
        // Values can be secret-bearing because they are sent on stdin to Vercel's secret
        // store; they must not be copied into publish plans, command lines, or state.
        switch (value)
        {
            case null:
                return processedValue;
            case string stringValue:
                return stringValue;
            case ParameterResource parameter:
                return await GetParameterValueAsync(parameter, cancellationToken).ConfigureAwait(false);
            case IResourceBuilder<ParameterResource> parameterBuilder:
                return await GetParameterValueAsync(parameterBuilder.Resource, cancellationToken).ConfigureAwait(false);
            case IResourceWithConnectionString connectionStringResource:
                return await GetValueProviderValueAsync(connectionStringResource.ConnectionStringExpression, $"connection string for resource '{connectionStringResource.Name}'", cancellationToken).ConfigureAwait(false);
            case IResourceBuilder<IResourceWithConnectionString> connectionStringBuilder:
                return await GetValueProviderValueAsync(connectionStringBuilder.Resource.ConnectionStringExpression, $"connection string for resource '{connectionStringBuilder.Resource.Name}'", cancellationToken).ConfigureAwait(false);
            case ReferenceExpression referenceExpression:
                return await GetProjectReferenceExpressionValueAsync(resource, options, entriesByResourceName, referenceExpression, cancellationToken).ConfigureAwait(false);
            case IValueProvider valueProvider:
                return await GetValueProviderValueAsync(valueProvider, "environment variable value", cancellationToken).ConfigureAwait(false);
            default:
                return processedValue;
        }
    }

    private static async ValueTask<string> GetProjectReferenceExpressionValueAsync(
        IResource resource,
        VercelEnvironmentOptionsAnnotation options,
        IReadOnlyDictionary<string, VercelDeploymentEntry> entriesByResourceName,
        ReferenceExpression referenceExpression,
        CancellationToken cancellationToken)
    {
        if (referenceExpression.IsConditional)
        {
            throw new DistributedApplicationException("Vercel project environment variables do not support conditional reference expressions. Configure a concrete Vercel project environment variable instead.");
        }

        var arguments = new object?[referenceExpression.ValueProviders.Count];
        for (int i = 0; i < referenceExpression.ValueProviders.Count; i++)
        {
            IValueProvider valueProvider = referenceExpression.ValueProviders[i];
            arguments[i] = valueProvider switch
            {
                // Secret-bearing project env vars may combine endpoint URLs with secret
                // parameters because the final value is sent through Vercel's secret path.
                EndpointReference endpointReference when IsCrossResourceEndpointReference(resource, endpointReference) => GetEndpointPropertyValue(resource, options, entriesByResourceName, endpointReference.Property(EndpointProperty.Url)),
                EndpointReferenceExpression endpointReferenceExpression when IsCrossResourceEndpointReference(resource, endpointReferenceExpression.Endpoint) => GetEndpointPropertyValue(resource, options, entriesByResourceName, endpointReferenceExpression),
                ParameterResource parameter => await GetParameterValueAsync(parameter, cancellationToken).ConfigureAwait(false),
                IResourceWithConnectionString connectionStringResource => await GetValueProviderValueAsync(connectionStringResource.ConnectionStringExpression, $"connection string for resource '{connectionStringResource.Name}'", cancellationToken).ConfigureAwait(false),
                _ => await GetValueProviderValueAsync(valueProvider, "reference expression value", cancellationToken).ConfigureAwait(false)
            };

            if (referenceExpression.StringFormats[i] is "uri" && arguments[i] is string stringValue)
            {
                arguments[i] = Uri.EscapeDataString(stringValue);
            }
        }

        return string.Format(CultureInfo.InvariantCulture, referenceExpression.Format, arguments);
    }

    private static async ValueTask<string> GetParameterValueAsync(ParameterResource parameter, CancellationToken cancellationToken)
    {
        try
        {
            string? value = await parameter.GetValueAsync(cancellationToken).ConfigureAwait(false);
            return value ?? throw new DistributedApplicationException($"Secret parameter '{parameter.Name}' did not produce a value for Vercel project environment configuration.");
        }
        catch (MissingParameterValueException ex)
        {
            throw new DistributedApplicationException($"Secret parameter '{parameter.Name}' does not have a value. Provide a value before deploying to Vercel.", ex);
        }
    }

    private static async ValueTask<string> GetValueProviderValueAsync(IValueProvider valueProvider, string description, CancellationToken cancellationToken)
    {
        try
        {
            string? value = await valueProvider.GetValueAsync(cancellationToken).ConfigureAwait(false);
            return value ?? throw new DistributedApplicationException($"The {description} did not produce a value for Vercel project environment configuration.");
        }
        catch (MissingParameterValueException ex)
        {
            throw new DistributedApplicationException($"The {description} does not have a value. Provide a value before deploying to Vercel.", ex);
        }
    }

    private static bool TryGetEnvironmentVariableValue(
        IResource resource,
        VercelEnvironmentOptionsAnnotation options,
        IReadOnlyDictionary<string, VercelDeploymentEntry> entriesByResourceName,
        object? value,
        [NotNullWhen(true)] out string? vercelValue)
    {
        // Service-discovery env vars generated by WithReference also arrive as structured
        // endpoint values, so translate by value shape instead of by environment variable name.
        switch (value)
        {
            case EndpointReference endpointReference when IsCrossResourceEndpointReference(resource, endpointReference):
                vercelValue = GetEndpointPropertyValue(resource, options, entriesByResourceName, endpointReference.Property(EndpointProperty.Url));
                return true;
            case EndpointReferenceExpression endpointReferenceExpression when IsCrossResourceEndpointReference(resource, endpointReferenceExpression.Endpoint):
                vercelValue = GetEndpointPropertyValue(resource, options, entriesByResourceName, endpointReferenceExpression);
                return true;
            case ReferenceExpression referenceExpression when ContainsCrossResourceEndpointReference(resource, referenceExpression):
                vercelValue = GetReferenceExpressionValue(resource, options, entriesByResourceName, referenceExpression);
                return true;
            default:
                vercelValue = null;
                return false;
        }
    }

    private static string GetReferenceExpressionValue(
        IResource resource,
        VercelEnvironmentOptionsAnnotation options,
        IReadOnlyDictionary<string, VercelDeploymentEntry> entriesByResourceName,
        ReferenceExpression referenceExpression)
    {
        if (referenceExpression.IsConditional)
        {
            throw new DistributedApplicationException("Vercel endpoint references do not support conditional reference expressions. Configure a concrete Vercel project environment variable instead.");
        }

        var arguments = new object?[referenceExpression.ValueProviders.Count];
        for (int i = 0; i < referenceExpression.ValueProviders.Count; i++)
        {
            IValueProvider valueProvider = referenceExpression.ValueProviders[i];
            arguments[i] = valueProvider switch
            {
                EndpointReference endpointReference when IsCrossResourceEndpointReference(resource, endpointReference) => GetEndpointPropertyValue(resource, options, entriesByResourceName, endpointReference.Property(EndpointProperty.Url)),
                EndpointReferenceExpression endpointReferenceExpression when IsCrossResourceEndpointReference(resource, endpointReferenceExpression.Endpoint) => GetEndpointPropertyValue(resource, options, entriesByResourceName, endpointReferenceExpression),
                // Mixed expressions can hide provider-specific ordering or secret semantics.
                // Keep this path to deterministic endpoint-only production URLs.
                _ => throw new DistributedApplicationException("Vercel endpoint reference expressions cannot be combined with parameters, secrets, or other value providers. Configure a concrete Vercel project environment variable instead.")
            };

            if (referenceExpression.StringFormats[i] is "uri" && arguments[i] is string stringValue)
            {
                arguments[i] = Uri.EscapeDataString(stringValue);
            }
        }

        return string.Format(CultureInfo.InvariantCulture, referenceExpression.Format, arguments);
    }

    private static string GetEndpointPropertyValue(
        IResource resource,
        VercelEnvironmentOptionsAnnotation options,
        IReadOnlyDictionary<string, VercelDeploymentEntry> entriesByResourceName,
        EndpointReferenceExpression endpointReferenceExpression)
    {
        // This is the endpoint-reference edge-case boundary: preview/custom URLs are
        // post-deploy outputs, internal endpoints have no public Vercel edge address, and
        // cross-environment references lack a stable same-deploy alias. Fail before values
        // are written to Vercel env vars.
        if (!options.Production)
        {
            throw new DistributedApplicationException(
                "Vercel endpoint references require production deployments because preview and custom target URLs are assigned by Vercel after deployment. Call WithVercelProductionDeployments on the Vercel environment, or remove the reference.");
        }

        var endpointReference = endpointReferenceExpression.Endpoint;
        var endpoint = endpointReference.EndpointAnnotation;
        if (!endpoint.IsExternal)
        {
            throw new DistributedApplicationException(
                $"Resource '{resource.Name}' references endpoint '{endpoint.Name}' on resource '{endpointReference.Resource.Name}', but Vercel endpoint references can only target external HTTP or HTTPS endpoints. Configure an external endpoint or remove the reference.");
        }

        if (!VercelDeploymentModel.IsHttpEndpoint(endpoint))
        {
            throw new DistributedApplicationException(
                $"Resource '{resource.Name}' references endpoint '{endpoint.Name}' on resource '{endpointReference.Resource.Name}' with scheme '{endpoint.UriScheme}', but Vercel endpoint references support only HTTP or HTTPS endpoints.");
        }

        if (!entriesByResourceName.TryGetValue(endpointReference.Resource.Name, out var referencedEntry))
        {
            throw new DistributedApplicationException(
                $"Resource '{resource.Name}' references endpoint '{endpoint.Name}' on resource '{endpointReference.Resource.Name}', but the referenced resource does not target this Vercel environment. Vercel endpoint references can only target workloads deployed to the same Vercel environment.");
        }

        string host = $"{VercelProjectNameResolver.GetProjectName(referencedEntry)}.vercel.app";
        const int port = 443;

        return endpointReferenceExpression.Property switch
        {
            // Production aliases are deterministic before deploy; preview/custom URLs are not.
            // Keep endpoint references on the stable caller-visible Vercel HTTPS surface.
            EndpointProperty.Url => $"https://{host}",
            EndpointProperty.Host or EndpointProperty.IPV4Host => host,
            EndpointProperty.Port => port.ToString(CultureInfo.InvariantCulture),
            EndpointProperty.TargetPort => endpoint.TargetPort is int targetPort
                ? targetPort.ToString(CultureInfo.InvariantCulture)
                : throw new DistributedApplicationException(
                    // Azure publishers can carry ContainerPortReference placeholders in
                    // Bicep/Helm. Vercel deploy receives concrete CLI env values, so an
                    // unresolved TargetPort would become a bogus string rather than a
                    // target-native reference.
                    $"Resource '{resource.Name}' references endpoint property '{EndpointProperty.TargetPort}' for endpoint '{endpoint.Name}' on resource '{endpointReference.Resource.Name}', but the endpoint does not define an explicit target port. Configure a target port or avoid passing TargetPort to Vercel."),
            EndpointProperty.Scheme => "https",
            EndpointProperty.HostAndPort => $"{host}:{port.ToString(CultureInfo.InvariantCulture)}",
            EndpointProperty.TlsEnabled => bool.TrueString,
            _ => throw new DistributedApplicationException($"The endpoint property '{endpointReferenceExpression.Property}' is not supported for Vercel endpoint references.")
        };
    }

    private static void ValidateEnvironmentVariableName(IResource resource, string name)
    {
        // Do not remap names to satisfy Vercel's env var shape. A lossy rename would break
        // the consuming workload's contract, so invalid names fail with the original key.
        if (string.IsNullOrWhiteSpace(name)
            || (!char.IsAsciiLetter(name[0]) && name[0] != '_')
            || name.Any(static character => !char.IsAsciiLetterOrDigit(character) && character != '_'))
        {
            throw new DistributedApplicationException(
                $"Resource '{resource.Name}' configures invalid Vercel environment variable name '{name}'. Use letters, digits, and underscores, and start with a letter or underscore.");
        }
    }

    private static bool ContainsSecretReference(object? value)
    {
        // Connection strings are treated as secret-bearing even when the underlying provider
        // does not mark each segment secret; Vercel should receive them through project env.
        return value switch
        {
            null => false,
            string => false,
            ParameterResource parameter => parameter.Secret,
            IResourceBuilder<ParameterResource> parameterBuilder => parameterBuilder.Resource.Secret,
            IResourceWithConnectionString => true,
            IResourceBuilder<IResourceWithConnectionString> => true,
            IValueWithReferences valueWithReferences => valueWithReferences.References.Any(ContainsSecretReference),
            _ => false
        };
    }

    private static bool ContainsCrossResourceEndpointReference(IResource resource, object? value)
    {
        return value switch
        {
            null => false,
            EndpointReference endpointReference => IsCrossResourceEndpointReference(resource, endpointReference),
            EndpointReferenceExpression endpointReferenceExpression => IsCrossResourceEndpointReference(resource, endpointReferenceExpression.Endpoint),
            IValueWithReferences valueWithReferences => valueWithReferences.References.Any(reference => ContainsCrossResourceEndpointReference(resource, reference)),
            _ => false
        };
    }

    private static bool IsCrossResourceEndpointReference(IResource resource, EndpointReference endpointReference)
        => !IsSameResource(resource, endpointReference.Resource);

    private static bool ContainsUnsupportedResourceReference(IResource resource, object? value)
    {
        // Vercel only knows how to turn endpoint references into deterministic production
        // aliases. Other resource references can represent connection strings, parameters,
        // or custom values that need a target-native mechanism this preview does not have.
        return value switch
        {
            null => false,
            string => false,
            ParameterResource => false,
            IResourceBuilder<ParameterResource> => false,
            EndpointReference => false,
            EndpointReferenceExpression => false,
            IResource referencedResource => !IsSameResource(resource, referencedResource),
            IValueWithReferences valueWithReferences => valueWithReferences.References.Any(reference => ContainsUnsupportedResourceReference(resource, reference)),
            IResourceBuilder<IResource> resourceBuilder => !IsSameResource(resource, resourceBuilder.Resource),
            _ => false
        };
    }

    private static bool IsSameResource(IResource resource, IResource otherResource)
        // Compare by Aspire resource name rather than object identity. Polyglot/ATS flows
        // can recreate resource references across an RPC boundary, but resource name is the
        // app-model identity used by deployment target maps.
        => string.Equals(resource.Name, otherResource.Name, StringComparison.Ordinal);
}
