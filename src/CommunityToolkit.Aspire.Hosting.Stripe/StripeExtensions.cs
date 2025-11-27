using Aspire.Hosting.ApplicationModel;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;

namespace Aspire.Hosting;

/// <summary>
/// Extension methods for adding Stripe CLI to a <see cref="IDistributedApplicationBuilder"/>.
/// </summary>
public static class StripeExtensions
{
    /// <summary>
    /// Adds the Stripe CLI to the application model for local webhook forwarding.
    /// </summary>
    /// <param name="builder">The <see cref="IDistributedApplicationBuilder"/> to add the resource to.</param>
    /// <param name="name">The name of the resource.</param>
    /// <returns>A reference to the <see cref="IResourceBuilder{T}"/>.</returns>
    public static IResourceBuilder<StripeResource> AddStripe(
        this IDistributedApplicationBuilder builder,
        [ResourceName] string name)
    {
        ArgumentNullException.ThrowIfNull(builder, nameof(builder));
        ArgumentException.ThrowIfNullOrEmpty(name, nameof(name));

        var resource = new StripeResource(name);

        return builder.AddResource(resource)
            .ExcludeFromManifest();
    }

    /// <summary>
    /// Configures the Stripe CLI to listen for webhooks and forward them to the specified URL expression.
    /// </summary>
    /// <param name="builder">The resource builder.</param>
    /// <param name="forwardTo">The resource to forward webhooks to.</param>
    /// <param name="webhookPath">The path to the webhook endpoint.</param>
    /// <param name="events">Optional comma-separated list of specific webhook events to listen for (e.g., "payment_intent.created,charge.succeeded"). If not specified, all events are forwarded.</param>
    /// <returns>A reference to the <see cref="IResourceBuilder{T}"/>.</returns>
    public static IResourceBuilder<StripeResource> WithListen(
        this IResourceBuilder<StripeResource> builder,
        IResourceBuilder<IResourceWithEndpoints> forwardTo,
        string webhookPath = "/webhooks/stripe",
        IEnumerable<string>? events = null)
    {
        ArgumentNullException.ThrowIfNull(builder, nameof(builder));
        ArgumentNullException.ThrowIfNull(forwardTo, nameof(forwardTo));

        builder.WithArgs("listen");
        builder.WithArgs(context =>
        {
            if (!forwardTo.Resource.TryGetEndpoints(out var endpoints) || !endpoints.Any())
            {
                throw new InvalidOperationException($"The resource '{forwardTo.Resource.Name}' does not have any endpoints defined.");
            }
            context.Args.Add($"--forward-to={endpoints.First().AllocatedEndpoint}{webhookPath}");
        });

        if (events is not null && events.Any())
        {
            builder.WithArgs("--events", string.Join(",", events));
        }

        return builder.ResolveSecret();
    }

    /// <summary>
    /// Configures the Stripe CLI to listen for webhooks and forward them to the specified URL expression.
    /// </summary>
    /// <param name="builder">The resource builder.</param>
    /// <param name="forwardTo">The resource to forward webhooks to.</param>
    /// <param name="webhookPath">The path to the webhook endpoint.</param>
    /// <param name="events">Optional comma-separated list of specific webhook events to listen for (e.g., "payment_intent.created,charge.succeeded"). If not specified, all events are forwarded.</param>
    /// <returns>A reference to the <see cref="IResourceBuilder{T}"/>.</returns>
    public static IResourceBuilder<StripeResource> WithListen(
        this IResourceBuilder<StripeResource> builder,
        IResourceBuilder<ExternalServiceResource> forwardTo,
        string webhookPath = "/webhooks/stripe",
        IEnumerable<string>? events = null)
    {
        ArgumentNullException.ThrowIfNull(builder, nameof(builder));
        ArgumentNullException.ThrowIfNull(forwardTo, nameof(forwardTo));

        builder.WithArgs("listen");

        if (forwardTo.Resource.Uri is not null)
        {
            builder.WithArgs($"--forward-to");
            builder.WithArgs(ReferenceExpression.Create($"{forwardTo.Resource.Uri.ToString()}{webhookPath}"));
        }
        else if (forwardTo.Resource.UrlParameter is not null)
        {
            builder.WithArgs(async context =>
            {
                if (!context.ExecutionContext.IsPublishMode)
                {
                    var url = await forwardTo.Resource.UrlParameter.GetValueAsync(context.CancellationToken).ConfigureAwait(false);
                    if (!UrlIsValidForExternalService(url, out var _, out var message))
                    {
                        throw new DistributedApplicationException($"The URL parameter '{forwardTo.Resource.UrlParameter.Name}' for the external service '{forwardTo.Resource.Name}' is invalid: {message}");
                    }
                }
                context.Args.Add($"--forward-to");
                context.Args.Add(ReferenceExpression.Create($"{forwardTo.Resource.UrlParameter}{webhookPath}"));
            });
        }
        else
        {
            throw new InvalidOperationException($"The external service resource '{forwardTo.Resource.Name}' does not have a defined URI.");
        }

        if (events is not null && events.Any())
        {
            builder.WithArgs("--events", string.Join(",", events));
        }

        return builder.ResolveSecret();
    }

    /// <summary>
    /// Configures the Stripe CLI to use a specific API key from a parameter.
    /// </summary>
    /// <param name="builder">The resource builder.</param>
    /// <param name="apiKey">The parameter containing the Stripe API key to use.</param>
    /// <returns>A reference to the <see cref="IResourceBuilder{T}"/>.</returns>
    public static IResourceBuilder<StripeResource> WithApiKey(
        this IResourceBuilder<StripeResource> builder,
        IResourceBuilder<ParameterResource> apiKey)
    {
        ArgumentNullException.ThrowIfNull(builder, nameof(builder));
        ArgumentNullException.ThrowIfNull(apiKey, nameof(apiKey));

        return builder.WithArgs(context =>
        {
            context.Args.Add($"--api-key");
            context.Args.Add(apiKey);
        });
    }

    /// <summary>
    /// Adds a reference to a Stripe CLI resource for accessing its webhook signing secret.
    /// </summary>
    /// <param name="builder">The resource builder.</param>
    /// <param name="source">The Stripe CLI resource to reference.</param>
    /// <param name="webhookSigningSecretEnvVarName">Optional environment variable name to use for the webhook signing secret. Defaults to "STRIPE_WEBHOOK_SECRET".</param>
    /// <returns>A reference to the <see cref="IResourceBuilder{T}"/>.</returns>
    public static IResourceBuilder<TDestination> WithReference<TDestination>(
        this IResourceBuilder<TDestination> builder,
        IResourceBuilder<StripeResource> source,
        string webhookSigningSecretEnvVarName = "STRIPE_WEBHOOK_SECRET")
        where TDestination : IResourceWithEnvironment
    {
        ArgumentNullException.ThrowIfNull(builder, nameof(builder));
        ArgumentNullException.ThrowIfNull(source, nameof(source));
        ArgumentException.ThrowIfNullOrEmpty(webhookSigningSecretEnvVarName, nameof(webhookSigningSecretEnvVarName));

        if (builder is IResourceBuilder<IResourceWithWaitSupport> waitResource)
        {
            waitResource.WaitFor(source);
        }

        return builder.WithEnvironment(context =>
        {
            context.EnvironmentVariables.Add(webhookSigningSecretEnvVarName, ReferenceExpression.Create($"{source.Resource.WebhookSigningSecret}"));
        });
    }

    private static IResourceBuilder<StripeResource> ResolveSecret(this IResourceBuilder<StripeResource> builder)
    {
        builder.OnBeforeResourceStarted(async (resource, @event, ct) =>
        {
            var stdOut = new StringWriter();
            var stdErr = new StringWriter();

            using var process = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = resource.Command,
                    Arguments = "listen --print-secret",
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    CreateNoWindow = true,
                }
            };

            process.OutputDataReceived += (sender, e) =>
            {
                if (e.Data is not null)
                {
                    stdOut.WriteLine(e.Data);
                }
            };

            process.ErrorDataReceived += (sender, e) =>
            {
                if (e.Data is not null)
                {
                    stdErr.WriteLine(e.Data);
                }
            };

            if (!process.Start())
            {
                throw new InvalidOperationException("Failed to start Stripe CLI process to retrieve webhook signing secret.");
            }

            process.BeginOutputReadLine();
            process.BeginErrorReadLine();

            await process.WaitForExitAsync(ct).ConfigureAwait(false);

            if (process.ExitCode != 0)
            {
                throw new InvalidOperationException($"Stripe CLI process exited with code {process.ExitCode}. Error output: {stdErr}");
            }

            var secret = stdOut.ToString().Trim();
            if (string.IsNullOrEmpty(secret))
            {
                throw new InvalidOperationException("Failed to retrieve webhook signing secret from Stripe CLI output.");
            }

            resource.WebhookSigningSecret = secret;
        });

        return builder;
    }

    internal static bool UrlIsValidForExternalService(string? url, [NotNullWhen(true)] out Uri? uri, [NotNullWhen(false)] out string? message)
    {
        if (url is null || !Uri.TryCreate(url, UriKind.Absolute, out uri))
        {
            uri = null;
            message = "The URL for the external service must be an absolute URI.";
            return false;
        }

        if (GetUriValidationException(uri) is { } exception)
        {
            message = exception.Message;
            uri = null;
            return false;
        }

        message = null;

        return true;
    }

    private static ArgumentException? GetUriValidationException(Uri uri)
    {
        if (!uri.IsAbsoluteUri)
        {
            return new ArgumentException("The URI for the external service must be absolute.", nameof(uri));
        }
        if (uri.AbsolutePath != "/")
        {
            return new ArgumentException("The URI absolute path must be \"/\".", nameof(uri));
        }
        if (!string.IsNullOrEmpty(uri.Fragment))
        {
            return new ArgumentException("The URI cannot contain a fragment.", nameof(uri));
        }
        return null;
    }
}
