using Aspire.Hosting.ApplicationModel;
using CommunityToolkit.Aspire.Hosting.Stripe;
using Microsoft.Extensions.DependencyInjection;

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
    /// <param name="apiKey">The parameter builder providing the Stripe API key.</param>
    /// <returns>A reference to the <see cref="IResourceBuilder{T}"/>.</returns>
    public static IResourceBuilder<StripeResource> AddStripe(
        this IDistributedApplicationBuilder builder,
        [ResourceName] string name,
        IResourceBuilder<ParameterResource> apiKey)
    {
        ArgumentNullException.ThrowIfNull(builder, nameof(builder));
        ArgumentException.ThrowIfNullOrEmpty(name, nameof(name));
        ArgumentNullException.ThrowIfNull(apiKey, nameof(apiKey));

        StripeResource resource = new(name);

        return builder.AddResource(resource)
            .WithImage(StripeContainerImageTags.Image)
            .WithImageTag(StripeContainerImageTags.Tag)
            .WithImageRegistry(StripeContainerImageTags.Registry)
            .WithEnvironment(context =>
            {
                context.EnvironmentVariables.Add("STRIPE_API_KEY", ReferenceExpression.Create($"{apiKey}"));
            })
            .ExcludeFromManifest();
    }

    /// <summary>
    /// Configures the Stripe CLI to listen for webhooks and forward them to the specified URL expression.
    /// </summary>
    /// <param name="builder">The resource builder.</param>
    /// <param name="forwardTo">The resource to forward webhooks to.</param>
    /// <param name="webhookPath">The path to the webhook endpoint.</param>
    /// <param name="events">Optional collection of specific webhook events to listen for (e.g., ["payment_intent.created", "charge.succeeded"]). If not specified, all events are forwarded.</param>
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
            context.Args.Add("--forward-to");
            context.Args.Add($"{endpoints.First().AllocatedEndpoint}{webhookPath}");
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
    /// <param name="events">Optional collection of specific webhook events to listen for (e.g., ["payment_intent.created", "charge.succeeded"]). If not specified, all events are forwarded.</param>
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
                string? url = await forwardTo.Resource.UrlParameter.GetValueAsync(context.CancellationToken).ConfigureAwait(false);
                if (!context.ExecutionContext.IsPublishMode)
                {
                    if (!UrlIsValidForExternalService(url, out var _, out var message))
                    {
                        throw new DistributedApplicationException($"The URL parameter '{forwardTo.Resource.UrlParameter.Name}' for the external service '{forwardTo.Resource.Name}' is invalid: {message}");
                    }
                }
                context.Args.Add($"--forward-to");
                context.Args.Add(ReferenceExpression.Create($"{url}{webhookPath}"));
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
        builder.OnBeforeResourceStarted((resource, @event, ct) =>
        {
            return Task.Run(async () =>
            {
                var notificationService = @event.Services.GetRequiredService<ResourceNotificationService>();
                var loggerService = @event.Services.GetRequiredService<ResourceLoggerService>();

                await foreach (var resourceEvent in notificationService.WatchAsync(ct).ConfigureAwait(false))
                {
                    if (!string.Equals(resource.Name, resourceEvent.Resource.Name, StringComparison.InvariantCultureIgnoreCase))
                    {
                        continue;
                    }

                    _ = WatchResourceLogsAsync(resourceEvent.ResourceId, loggerService, ct);
                    break;
                }
            }, ct);

            async Task WatchResourceLogsAsync(string resourceId, ResourceLoggerService loggerService, CancellationToken cancellationToken)
            {
                try
                {
                    await foreach (var logEvent in loggerService.WatchAsync(resourceId).WithCancellation(cancellationToken).ConfigureAwait(false))
                    {
                        foreach (var line in logEvent.Where(l => !string.IsNullOrWhiteSpace(l.Content)))
                        {
                            if (TryExtractSigningSecret(line.Content, out var signingSecret))
                            {
                                resource.WebhookSigningSecret = signingSecret;
                                return;
                            }
                        }
                    }
                }
                catch (TaskCanceledException) when (cancellationToken.IsCancellationRequested)
                {
                    // Expected when the resource is shutting down.
                }
                catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
                {
                    // Expected when the resource is shutting down.
                }
            }

            static bool TryExtractSigningSecret(string? content, out string? secret)
            {
                secret = null;

                if (string.IsNullOrWhiteSpace(content))
                {
                    return false;
                }

                const string Prefix = "whsec_";
                var startIndex = content.IndexOf(Prefix, StringComparison.OrdinalIgnoreCase);
                if (startIndex < 0)
                {
                    return false;
                }

                var endIndex = startIndex + Prefix.Length;
                while (endIndex < content.Length && IsSecretCharacter(content[endIndex]))
                {
                    endIndex++;
                }

                var candidate = content.Substring(startIndex, endIndex - startIndex).TrimEnd('.', ';', ',', ')', '"');

                if (candidate.Length <= Prefix.Length)
                {
                    return false;
                }

                secret = candidate;
                return true;

                static bool IsSecretCharacter(char value) => char.IsLetterOrDigit(value) || value is '_' or '-';
            }
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
