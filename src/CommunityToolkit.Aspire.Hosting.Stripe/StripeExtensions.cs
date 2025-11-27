using Aspire.Hosting.ApplicationModel;

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
    /// Configures the Stripe CLI to listen for webhooks and forward them to the specified endpoint.
    /// </summary>
    /// <param name="builder">The resource builder.</param>
    /// <param name="reference">A reference to an endpoint to forward webhooks to.</param>
    /// <param name="events">Optional comma-separated list of specific webhook events to listen for (e.g., "payment_intent.created,charge.succeeded"). If not specified, all events are forwarded.</param>
    /// <returns>A reference to the <see cref="IResourceBuilder{T}"/>.</returns>
    public static IResourceBuilder<StripeResource> WithListen(
        this IResourceBuilder<StripeResource> builder,
        EndpointReference reference,
        string? events = null)
    {
        ArgumentNullException.ThrowIfNull(builder, nameof(builder));
        ArgumentNullException.ThrowIfNull(reference, nameof(reference));

        return builder.WithListen(ReferenceExpression.Create($"{reference.Property(EndpointProperty.Url)}"), events);
    }

    /// <summary>
    /// Configures the Stripe CLI to listen for webhooks and forward them to the specified URL.
    /// </summary>
    /// <param name="builder">The resource builder.</param>
    /// <param name="forwardTo">The URL to forward webhooks to (e.g., "http://localhost:5000/webhooks/stripe").</param>
    /// <param name="events">Optional comma-separated list of specific webhook events to listen for (e.g., "payment_intent.created,charge.succeeded"). If not specified, all events are forwarded.</param>
    /// <returns>A reference to the <see cref="IResourceBuilder{T}"/>.</returns>
    public static IResourceBuilder<StripeResource> WithListen(
        this IResourceBuilder<StripeResource> builder,
        string forwardTo,
        string? events = null)
    {
        ArgumentNullException.ThrowIfNull(builder, nameof(builder));
        ArgumentException.ThrowIfNullOrEmpty(forwardTo, nameof(forwardTo));

        return builder.WithListen(ReferenceExpression.Create($"{forwardTo}"), events);
    }

    /// <summary>
    /// Configures the Stripe CLI to listen for webhooks and forward them to the specified URL expression.
    /// </summary>
    /// <param name="builder">The resource builder.</param>
    /// <param name="forwardTo">The URL expression to forward webhooks to.</param>
    /// <param name="events">Optional comma-separated list of specific webhook events to listen for (e.g., "payment_intent.created,charge.succeeded"). If not specified, all events are forwarded.</param>
    /// <returns>A reference to the <see cref="IResourceBuilder{T}"/>.</returns>
    public static IResourceBuilder<StripeResource> WithListen(
        this IResourceBuilder<StripeResource> builder,
        ReferenceExpression forwardTo,
        string? events = null)
    {
        ArgumentNullException.ThrowIfNull(builder, nameof(builder));
        ArgumentNullException.ThrowIfNull(forwardTo, nameof(forwardTo));

        builder.WithArgs("listen");
        builder.WithArgs(context => context.Args.Add($"--forward-to={forwardTo}"));

        if (!string.IsNullOrEmpty(events))
        {
            builder.WithArgs("--events", events);
        }

        return builder;
    }

    /// <summary>
    /// Configures the Stripe CLI to use a specific API key.
    /// </summary>
    /// <param name="builder">The resource builder.</param>
    /// <param name="apiKey">The Stripe API key to use.</param>
    /// <returns>A reference to the <see cref="IResourceBuilder{T}"/>.</returns>
    public static IResourceBuilder<StripeResource> WithApiKey(
        this IResourceBuilder<StripeResource> builder,
        string apiKey)
    {
        ArgumentNullException.ThrowIfNull(builder, nameof(builder));
        ArgumentException.ThrowIfNullOrEmpty(apiKey, nameof(apiKey));

        return builder.WithArgs("--api-key", apiKey);
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

        return builder.WithArgs(context => context.Args.Add($"--api-key={apiKey.Resource}"));
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

        return builder.WithEnvironment(context =>
        {
            context.EnvironmentVariables[webhookSigningSecretEnvVarName] = $"{source.Resource.Name}.outputs.webhookSigningSecret";
        });
    }
}
