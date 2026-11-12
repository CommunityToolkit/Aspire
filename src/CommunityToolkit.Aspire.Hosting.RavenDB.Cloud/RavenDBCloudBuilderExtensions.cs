#pragma warning disable ASPIREATS001 // AspireExport is experimental

using System.ComponentModel;
using System.Text.RegularExpressions;
using Aspire.Hosting.ApplicationModel;
using Aspire.Hosting.Pipelines;
using CommunityToolkit.Aspire.Hosting.RavenDB;
using CommunityToolkit.Aspire.Hosting.RavenDB.Cloud;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Aspire.Hosting;

/// <summary>
/// Publishes a RavenDB server to RavenDB Cloud.
/// </summary>
public static partial class RavenDBCloudBuilderExtensions
{
    /// <summary>
    /// Publishes the server to a RavenDB Cloud product this AppHost owns: <c>aspire deploy</c> creates it from
    /// <paramref name="spec"/> when there is none named <paramref name="productName"/>, creates the databases declared
    /// with <c>ensureCreated</c>, and gives every application that references the server the product URL and a client
    /// certificate of its own. <c>aspire run</c> still starts the local container.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Every deployment starts with a plan: it compares the product with the AppHost and stops on any difference, unless
    /// the deployment is approved with the environment variable <c>RavenDB__Cloud__ApproveChanges=true</c>, in which case
    /// it applies the changes the Cloud API can make. The approval is read from the process environment only, never from
    /// configuration files, so it cannot be committed. <c>aspire do ravendb-cloud-plan-&lt;resource name&gt;</c> shows the plan only.
    /// </para>
    /// <para>
    /// The product name is the product's identity: a deployment from any machine, CI included, finds the product by it.
    /// Renaming the product in the AppHost creates a new product; the old one keeps running until it is terminated in the
    /// portal. <c>aspire publish</c> never calls the Cloud API.
    /// </para>
    /// <para>
    /// Each application gets a new certificate, with admin access to the databases it references only, on every
    /// deployment; the previous ones are revoked once the applications run with the new ones. The URL and the certificate
    /// reach the application as the client integration's settings, through the compute environment's own mechanism.
    /// </para>
    /// </remarks>
    /// <param name="builder">The resource builder for the RavenDB server.</param>
    /// <param name="apiKey">A secret parameter holding a RavenDB Cloud API key. The key has account-owner rights.</param>
    /// <param name="productName">The product's display name, used exactly as given; unique in the account.</param>
    /// <param name="spec">What the product is made of: <see cref="RavenDBCloudSpec.Development"/> or <see cref="RavenDBCloudSpec.Production"/>.</param>
    /// <param name="configure">Optional settings, such as the IP ranges allowed to reach the product.</param>
    /// <returns>The <see cref="IResourceBuilder{T}"/> for the RavenDB server resource.</returns>
    [AspireExportIgnore(Reason = "Polyglot AppHosts choose the spec with the overload that takes a RavenDBCloudSpecBuilder callback.")]
    public static IResourceBuilder<RavenDBServerResource> PublishAsRavenDBCloud(
        this IResourceBuilder<RavenDBServerResource> builder,
        IResourceBuilder<ParameterResource> apiKey,
        string productName,
        RavenDBCloudSpec spec,
        Action<RavenDBCloudOptions>? configure = null)
    {
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentNullException.ThrowIfNull(apiKey);
        ValidateProductName(productName);
        ArgumentNullException.ThrowIfNull(spec);

        var options = Configure(configure);

        if (options.Subdomain is { } subdomain && !RavenDBCloudDeployment.IsValidSubdomain(subdomain))
        {
            throw new ArgumentException(
                $"RavenDB Cloud does not accept the subdomain '{subdomain}': use up to {RavenDBCloudDeployment.MaxSubdomainLength} " +
                "letters, digits and dashes, not starting or ending with a dash.",
                nameof(configure));
        }

        return builder.ApplicationBuilder.ExecutionContext.IsPublishMode
            ? Publish(builder, new RavenDBCloudDeployment(builder.Resource, apiKey.Resource, RavenDBCloudMode.Managed, productName.Trim(), spec, null, options), nameof(PublishAsRavenDBCloud))
            : builder;
    }

    /// <summary>
    /// Publishes the server to a RavenDB Cloud product this AppHost owns, with the product's spec chosen by
    /// <paramref name="spec"/>; for polyglot AppHosts, which cannot call the <see cref="RavenDBCloudSpec"/> factories.
    /// See <see cref="PublishAsRavenDBCloud(IResourceBuilder{RavenDBServerResource}, IResourceBuilder{ParameterResource}, string, RavenDBCloudSpec, Action{RavenDBCloudOptions}?)"/>.
    /// </summary>
    /// <param name="builder">The resource builder for the RavenDB server.</param>
    /// <param name="apiKey">A secret parameter holding a RavenDB Cloud API key. The key has account-owner rights.</param>
    /// <param name="productName">The product's display name, used exactly as given; unique in the account.</param>
    /// <param name="spec">Calls exactly one of <c>development</c> or <c>production</c>.</param>
    /// <param name="configure">Optional settings, such as the IP ranges allowed to reach the product.</param>
    /// <returns>The <see cref="IResourceBuilder{T}"/> for the RavenDB server resource.</returns>
    [AspireExport(RunSyncOnBackgroundThread = true)]
    [EditorBrowsable(EditorBrowsableState.Never)]
    public static IResourceBuilder<RavenDBServerResource> PublishAsRavenDBCloud(
        this IResourceBuilder<RavenDBServerResource> builder,
        IResourceBuilder<ParameterResource> apiKey,
        string productName,
        Action<RavenDBCloudSpecBuilder> spec,
        Action<RavenDBCloudOptions>? configure = null)
    {
        ArgumentNullException.ThrowIfNull(spec);

        var chosen = new RavenDBCloudSpecBuilder();
        spec(chosen);

        return builder.PublishAsRavenDBCloud(
            apiKey,
            productName,
            chosen.Spec ?? throw new ArgumentException(
                "Choose the RavenDB Cloud product's spec: call development or production.",
                nameof(spec)),
            configure);
    }

    /// <summary>
    /// Publishes the server to an existing RavenDB Cloud product, which other AppHosts may use too: <c>aspire deploy</c>
    /// never creates, changes or terminates it. It creates the databases declared with <c>ensureCreated</c> and gives
    /// every application that references the server the product URL and a client certificate of its own, named after
    /// <paramref name="clientName"/>. <c>aspire run</c> still starts the local container.
    /// </summary>
    /// <remarks>
    /// The client name keeps this AppHost's certificates apart from those of the product's other clients: the
    /// deployment only revokes certificates of its own client. Give each AppHost and environment that uses the product
    /// its own client name. The product's owner allows the deploying machine and the applications' outbound addresses
    /// in its AllowedIps.
    /// </remarks>
    /// <param name="builder">The resource builder for the RavenDB server.</param>
    /// <param name="apiKey">A secret parameter holding a RavenDB Cloud API key of the account that holds the product.</param>
    /// <param name="productName">The product's display name.</param>
    /// <param name="clientName">The name of this AppHost among the product's clients: letters, digits and dashes.</param>
    /// <param name="configure">Optional settings.</param>
    /// <returns>The <see cref="IResourceBuilder{T}"/> for the RavenDB server resource.</returns>
    [AspireExport(RunSyncOnBackgroundThread = true)]
    public static IResourceBuilder<RavenDBServerResource> PublishAsExistingRavenDBCloud(
        this IResourceBuilder<RavenDBServerResource> builder,
        IResourceBuilder<ParameterResource> apiKey,
        string productName,
        string clientName,
        Action<RavenDBCloudOptions>? configure = null)
    {
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentNullException.ThrowIfNull(apiKey);
        ValidateProductName(productName);
        ArgumentException.ThrowIfNullOrWhiteSpace(clientName);

        if (!ClientNamePattern().IsMatch(clientName))
        {
            throw new ArgumentException(
                $"'{clientName}' is not a valid client name: use letters, digits and dashes, starting with a letter or a digit.",
                nameof(clientName));
        }

        var options = Configure(configure);

        if (options.TerminateOnDestroy)
        {
            throw new ArgumentException(
                "PublishAsExistingRavenDBCloud never terminates the product, so TerminateOnDestroy cannot be set.",
                nameof(configure));
        }

        return builder.ApplicationBuilder.ExecutionContext.IsPublishMode
            ? Publish(builder, new RavenDBCloudDeployment(builder.Resource, apiKey.Resource, RavenDBCloudMode.Attached, productName.Trim(), null, clientName, options), nameof(PublishAsExistingRavenDBCloud))
            : builder;
    }

    private static RavenDBCloudOptions Configure(Action<RavenDBCloudOptions>? configure)
    {
        var options = new RavenDBCloudOptions();
        configure?.Invoke(options);
        return options;
    }

    private static void ValidateProductName(string productName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(productName);

        if (productName.Trim().Length > RavenDBCloudDeployment.MaxProductNameLength)
        {
            throw new ArgumentException(
                $"RavenDB Cloud accepts product names of up to {RavenDBCloudDeployment.MaxProductNameLength} characters.",
                nameof(productName));
        }
    }

    private static IResourceBuilder<RavenDBServerResource> Publish(
        IResourceBuilder<RavenDBServerResource> builder,
        RavenDBCloudDeployment deployment,
        string method)
    {
        builder.ApplicationBuilder.Services.TryAddSingleton<IRavenDBCloudApiClientFactory, RavenDBCloudApiClientFactory>();
        builder.ApplicationBuilder.Services.TryAddSingleton<IRavenDBServerAdministrationFactory, RavenDBServerAdministrationFactory>();
        builder.ApplicationBuilder.Services.TryAddSingleton<IRavenDBCloudEnvironment, ProcessEnvironment>();

        builder.WithPublishStrategy(new RavenDBPublishStrategy(method, ReferenceExpression.Create($"{deployment.Endpoint}")));

        // Every application that uses the server gets a certificate value of its own; the deployment fills it in.
        builder.ApplicationBuilder.Eventing.Subscribe<BeforeStartEvent>((@event, _) =>
        {
            Validate(@event.Model, deployment);

            foreach (var consumer in RavenDBConsumers.Find(@event.Model, builder.Resource))
            {
                var certificate = new RavenDBCloudCertificate(builder.Resource, consumer.Resource);
                deployment.Certificates.Add(certificate);
                RavenDBClientConfiguration.DeliverCertificate(consumer, certificate);
            }

            return Task.CompletedTask;
        });

        return builder
            .WithAnnotation(deployment)
            .WithPipelineStepFactory(_ => RavenDBCloudPipelineSteps.Create(deployment))
            .WithPipelineConfiguration(context => RavenDBCloudPipelineSteps.Configure(deployment, context));
    }

    /// <summary>What can be checked before anything runs, so a mistake never gets as far as the Cloud API.</summary>
    private static void Validate(DistributedApplicationModel model, RavenDBCloudDeployment deployment)
    {
        var sameProduct = model.Resources
            .OfType<RavenDBServerResource>()
            .Where(s => s != deployment.Server && s.Annotations.OfType<RavenDBCloudDeployment>()
                .Any(d => string.Equals(d.ProductName, deployment.ProductName, StringComparison.OrdinalIgnoreCase)))
            .Select(s => s.Name)
            .ToList();

        if (sameProduct.Count > 0)
        {
            throw new DistributedApplicationException(
                $"RavenDB servers '{deployment.Server.Name}' and '{string.Join("', '", sameProduct)}' are published to the same " +
                $"RavenDB Cloud product '{deployment.ProductName}'. One product backs one server: give each its own product name.");
        }

        foreach (var database in deployment.Server.Databases.Values)
        {
            if (!DatabaseNamePattern().IsMatch(database))
            {
                throw new DistributedApplicationException(
                    $"'{database}' is not a valid RavenDB database name. Use letters, digits, '_', '-' or '.'.");
            }
        }
    }

    [GeneratedRegex("^[A-Za-z0-9][A-Za-z0-9-]*$")]
    private static partial Regex ClientNamePattern();

    [GeneratedRegex("^[A-Za-z0-9_.-]+$")]
    private static partial Regex DatabaseNamePattern();
}
