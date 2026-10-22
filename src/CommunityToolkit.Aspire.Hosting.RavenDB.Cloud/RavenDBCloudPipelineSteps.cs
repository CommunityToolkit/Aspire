using Aspire.Hosting;
using Aspire.Hosting.ApplicationModel;
using Aspire.Hosting.Pipelines;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace CommunityToolkit.Aspire.Hosting.RavenDB.Cloud;

/// <summary>
/// The deploy and destroy steps of a server published to RavenDB Cloud. Their names are part of the public contract:
/// <c>aspire do ravendb-cloud-plan-&lt;name&gt;</c> shows the plan without changing anything.
/// </summary>
internal static class RavenDBCloudPipelineSteps
{
    private const string StepPrefix = "ravendb-cloud-";

    /// <summary>The step that computes and shows the plan for <paramref name="server"/>.</summary>
    public static string PlanStepName(RavenDBServerResource server) => $"{StepPrefix}plan-{server.Name}";

    /// <summary>The step that creates or changes the product of <paramref name="server"/>.</summary>
    public static string ProvisionStepName(RavenDBServerResource server) => $"{StepPrefix}provision-{server.Name}";

    /// <summary>The step that creates the databases <paramref name="server"/> declares.</summary>
    public static string DatabasesStepName(RavenDBServerResource server) => $"{StepPrefix}databases-{server.Name}";

    /// <summary>The step that issues the applications of <paramref name="server"/> their certificates.</summary>
    public static string CertificatesStepName(RavenDBServerResource server) => $"{StepPrefix}certificates-{server.Name}";

    /// <summary>The step that revokes the certificates the applications of <paramref name="server"/> no longer use.</summary>
    public static string CleanupStepName(RavenDBServerResource server) => $"{StepPrefix}cleanup-{server.Name}";

    /// <summary>The step that revokes the certificates of <paramref name="server"/> and, when asked, terminates its product.</summary>
    public static string DestroyStepName(RavenDBServerResource server) => $"{StepPrefix}destroy-{server.Name}";

    /// <summary>The pipeline steps that deploy and destroy <paramref name="deployment"/>.</summary>
    public static IEnumerable<PipelineStep> Create(RavenDBCloudDeployment deployment)
    {
        var server = deployment.Server;

        var plan = new PipelineStep
        {
            Name = PlanStepName(server),
            Description = $"Compares RavenDB Cloud product '{deployment.ProductName}' with the AppHost and decides what the deployment does",
            Resource = server,
            Action = context => PlanAsync(deployment, context),
        };

        // Before anything is built or changed.
        plan.RequiredBy(WellKnownPipelineSteps.DeployPrereq);

        var provision = new PipelineStep
        {
            Name = ProvisionStepName(server),
            Description = $"Creates RavenDB Cloud product '{deployment.ProductName}' or applies the approved changes, and waits until it is active",
            Resource = server,
            Tags = [WellKnownPipelineTags.ProvisionInfrastructure],
            Action = async context =>
            {
                using var client = await CreateClientAsync(deployment, context).ConfigureAwait(false);
                var currentPlan = deployment.Plan ?? await ComputePlanAsync(deployment, client, context).ConfigureAwait(false);

                await CreateProducts(client, context).ProvisionAsync(deployment, currentPlan, context.CancellationToken).ConfigureAwait(false);

                context.Summary.Add($"RavenDB Cloud ({server.Name})", deployment.Endpoint.Value!);
            },
        };
        provision.DependsOn(plan);
        provision.RequiredBy(WellKnownPipelineSteps.Deploy);

        var databases = new PipelineStep
        {
            Name = DatabasesStepName(server),
            Description = $"Creates the databases of '{server.Name}' declared with ensureCreated",
            Resource = server,
            Action = context => WithServerAsync(deployment, context, async (_, administration) =>
            {
                foreach (var database in server.DatabasesToCreate)
                {
                    if (await administration.DatabaseExistsAsync(database, context.CancellationToken).ConfigureAwait(false))
                    {
                        continue;
                    }

                    if (await administration.CreateDatabaseAsync(database, deployment.NodeCount, context.CancellationToken).ConfigureAwait(false))
                    {
                        context.Logger.LogInformation("Created database '{Database}' on {Nodes} node(s).", database, deployment.NodeCount);
                    }
                }
            }),
        };
        databases.DependsOn(provision);
        databases.RequiredBy(WellKnownPipelineSteps.Deploy);

        var certificates = new PipelineStep
        {
            Name = CertificatesStepName(server),
            Description = $"Issues a new client certificate to each application that uses '{server.Name}'",
            Resource = server,
            Action = context => WithServerAsync(deployment, context, (_, administration) =>
                new RavenDBCloudCertificates(context.Logger).IssueAsync(
                    administration,
                    deployment,
                    RavenDBConsumers.Find(context.Model, server),
                    context.CancellationToken)),
        };
        certificates.DependsOn(databases);
        certificates.RequiredBy(WellKnownPipelineSteps.Deploy);

        var cleanup = new PipelineStep
        {
            Name = CleanupStepName(server),
            Description = $"Revokes the client certificates of '{server.Name}' that this deployment replaced",
            Resource = server,
            Action = context => WithServerAsync(deployment, context, (_, administration) =>
                new RavenDBCloudCertificates(context.Logger).RevokeReplacedAsync(administration, deployment, context.CancellationToken)),
        };
        cleanup.DependsOn(certificates);
        cleanup.RequiredBy(WellKnownPipelineSteps.Deploy);

        var destroy = new PipelineStep
        {
            Name = DestroyStepName(server),
            Description = $"Revokes the client certificates of '{server.Name}' and terminates RavenDB Cloud product '{deployment.ProductName}' if TerminateOnDestroy is set",
            Resource = server,
            Action = async context =>
            {
                using var client = await CreateClientAsync(deployment, context).ConfigureAwait(false);
                var products = CreateProducts(client, context);

                if (Endpoint(context) is var endpoint && endpoint != DefaultEndpoint)
                {
                    context.Logger.LogWarning("The RavenDB Cloud API at {Endpoint}, from {Variable}, not the production one.", endpoint, EndpointVariable);
                }

                if (await products.DestroyAsync(deployment, context.CancellationToken).ConfigureAwait(false) is null)
                {
                    return;
                }

                using var administration = await ConnectAsync(deployment, client, context).ConfigureAwait(false);

                await new RavenDBCloudCertificates(context.Logger).RevokeAllAsync(administration, deployment, context.CancellationToken).ConfigureAwait(false);
            },
        };
        destroy.DependsOn(WellKnownPipelineSteps.DestroyPrereq);
        destroy.RequiredBy(WellKnownPipelineSteps.Destroy);

        return [plan, provision, databases, certificates, cleanup, destroy];
    }

    /// <summary>Orders the steps against the ones Aspire and the compute environments add.</summary>
    public static void Configure(RavenDBCloudDeployment deployment, PipelineConfigurationContext context)
    {
        var server = deployment.Server;
        var plan = AspireConventions.Required(context, PlanStepName(server));
        var provision = AspireConventions.Required(context, ProvisionStepName(server));
        var certificates = AspireConventions.Required(context, CertificatesStepName(server));
        var cleanup = AspireConventions.Required(context, CleanupStepName(server));
        var destroy = AspireConventions.Required(context, DestroyStepName(server));

        // The API key is a parameter, resolved (or prompted for) by process-parameters.
        if (context.Steps.Any(s => s.Name == WellKnownPipelineSteps.ProcessParameters))
        {
            plan.DependsOn(WellKnownPipelineSteps.ProcessParameters);
            destroy.DependsOn(WellKnownPipelineSteps.ProcessParameters);
        }

        // The environments resolve the applications' values (the product URL, the certificates) when they prepare
        // their artifacts: the values have to be set by then.
        foreach (var environment in context.Model.Resources.OfType<IComputeEnvironmentResource>())
        {
            if (context.Steps.FirstOrDefault(s => s.Name == AspireConventions.Prepare(environment)) is { } prepare)
            {
                prepare.DependsOn(provision);
                prepare.DependsOn(certificates);
            }
        }

        // The previous certificates are revoked only once the applications run with the new ones, and the product only
        // goes away once the applications are stopped.
        // The steps of every RavenDB Cloud server are left out, so two servers do not wait for each other.
        foreach (var step in context.Steps.Where(s => !s.Name.StartsWith(StepPrefix, StringComparison.Ordinal)))
        {
            if (step.RequiredBySteps.Contains(WellKnownPipelineSteps.Deploy))
            {
                cleanup.DependsOn(step);
            }

            if (step.RequiredBySteps.Contains(WellKnownPipelineSteps.Destroy))
            {
                destroy.DependsOn(step);
            }
        }
    }

    private static async Task PlanAsync(RavenDBCloudDeployment deployment, PipelineStepContext context)
    {
        using var client = await CreateClientAsync(deployment, context).ConfigureAwait(false);
        var plan = await ComputePlanAsync(deployment, client, context).ConfigureAwait(false);

        if (Endpoint(context) is var endpoint && endpoint != DefaultEndpoint)
        {
            plan.Add($"endpoint: the RavenDB Cloud API at {endpoint}, from {EndpointVariable}, not the production one");
        }

        // The certificates other clients of an existing product use: one may be an earlier name of this AppHost's.
        if (plan.Product is { Status: ProductStatus.Active } product && product.Dns?.Any() == true && !plan.Blocks)
        {
            await AddOtherClientsAsync(deployment, client, context, plan).ConfigureAwait(false);
        }

        var report = $"Plan for RavenDB Cloud product '{deployment.ProductName}':{Environment.NewLine}{plan}";

        // One task per line: the CLI shows tasks, so the plan is what the person who deploys reads first.
        foreach (var line in plan.Lines)
        {
            var task = await context.ReportingStep.CreateTaskAsync(line.Text, context.CancellationToken).ConfigureAwait(false);
            await task.CompleteAsync(line.Text, line.Blocks ? CompletionState.CompletedWithError : CompletionState.Completed, context.CancellationToken).ConfigureAwait(false);
        }

        if (plan.Blocks)
        {
            context.Logger.LogError("{Plan}", report);
            throw new InvalidOperationException(report);
        }

        context.Logger.LogInformation("{Plan}", report);
        deployment.Plan = plan;
    }

    private static async Task<RavenDBCloudPlan> ComputePlanAsync(RavenDBCloudDeployment deployment, RavenDBCloudApiClient client, PipelineStepContext context)
    {
        var environment = context.Services.GetRequiredService<IRavenDBCloudEnvironment>();
        var approved = bool.TryParse(environment.Get(RavenDBCloudDeploymentApproval.Variable), out var value) && value;

        return await RavenDBCloudPlanner.PlanAsync(
            client,
            deployment,
            RavenDBConsumers.Find(context.Model, deployment.Server),
            approved,
            context.CancellationToken).ConfigureAwait(false);
    }

    private static async Task AddOtherClientsAsync(RavenDBCloudDeployment deployment, RavenDBCloudApiClient client, PipelineStepContext context, RavenDBCloudPlan plan)
    {
        var urls = RavenDBCloudProducts.GetNodeUrls(plan.Product!);

        deployment.ProductId = plan.ProductId;
        var certificate = await CreateProducts(client, context).GetAdminCertificateAsync(deployment, context.CancellationToken).ConfigureAwait(false);
        using var administration = context.Services.GetRequiredService<IRavenDBServerAdministrationFactory>().Create(urls, certificate);

        IReadOnlyList<string> others;

        try
        {
            await administration.PingAsync(context.CancellationToken).ConfigureAwait(false);
            others = await RavenDBCloudCertificates.GetOtherClientsAsync(administration, deployment, context.CancellationToken).ConfigureAwait(false);
        }
        catch (Exception exception) when (RavenDBServerAdministration.IsUnreachable(exception))
        {
            // Every later step talks to the server: stop before anything is built or changed.
            context.Logger.LogDebug(exception, "RavenDB Cloud product '{Product}' did not answer.", deployment.ProductName);
            plan.Add(
                $"unreachable: '{deployment.ProductName}' at {string.Join(", ", urls)} does not answer this machine. Add its address to the product's " +
                "allowed IPs in the RavenDB Cloud portal and deploy again.",
                blocks: true);
            return;
        }

        if (others.Count > 0)
        {
            plan.Add(
                $"other certificates: {string.Join(", ", others)} on '{deployment.ProductName}' are not ones this AppHost issues for " +
                "its current databases, so they are left alone. If one is from an earlier name of this AppHost or a database it no " +
                $"longer declares, revoke it in the Studio: {urls[0]}/studio/index.html#admin/settings/certificates");
        }
    }

    /// <summary>Runs <paramref name="action"/> against the product's server, with its client admin certificate.</summary>
    private static async Task WithServerAsync(
        RavenDBCloudDeployment deployment,
        PipelineStepContext context,
        Func<RavenDBCloudApiClient, IRavenDBServerAdministration, Task> action)
    {
        using var client = await CreateClientAsync(deployment, context).ConfigureAwait(false);
        using var administration = await ConnectAsync(deployment, client, context).ConfigureAwait(false);

        await action(client, administration).ConfigureAwait(false);
    }

    /// <summary>
    /// Connects to the product's server with its admin certificate, once the server accepts connections: right after a
    /// change the product reports Active a little before its node answers.
    /// </summary>
    private static async Task<IRavenDBServerAdministration> ConnectAsync(RavenDBCloudDeployment deployment, RavenDBCloudApiClient client, PipelineStepContext context)
    {
        var certificate = await CreateProducts(client, context).GetAdminCertificateAsync(deployment, context.CancellationToken).ConfigureAwait(false);
        var administration = context.Services.GetRequiredService<IRavenDBServerAdministrationFactory>()
            .Create(deployment.NodeUrls, certificate);

        try
        {
            var deadline = DateTimeOffset.UtcNow + deployment.ReachableTimeout;
            var waiting = false;

            while (true)
            {
                try
                {
                    await administration.PingAsync(context.CancellationToken).ConfigureAwait(false);
                    return administration;
                }
                catch (Exception exception) when (RavenDBServerAdministration.IsUnreachable(exception))
                {
                    if (DateTimeOffset.UtcNow >= deadline)
                    {
                        throw new InvalidOperationException(
                            $"RavenDB Cloud product '{deployment.ProductName}' at {deployment.Endpoint.Value} did not answer for " +
                            $"{deployment.ReachableTimeout}. Check that the address of this machine is among the product's allowed IPs.",
                            exception);
                    }

                    if (!waiting)
                    {
                        context.Logger.LogInformation(
                            "Waiting for RavenDB Cloud product '{Product}' at {Url} to accept connections.",
                            deployment.ProductName,
                            deployment.Endpoint.Value);
                        waiting = true;
                    }
                }

                await Task.Delay(deployment.PollInterval, context.CancellationToken).ConfigureAwait(false);
            }
        }
        catch
        {
            administration.Dispose();
            throw;
        }
    }

    private static async Task<RavenDBCloudApiClient> CreateClientAsync(RavenDBCloudDeployment deployment, PipelineStepContext context)
    {
        var apiKey = await deployment.ApiKey.GetValueAsync(context.CancellationToken).ConfigureAwait(false);

        if (string.IsNullOrWhiteSpace(apiKey))
        {
            throw new InvalidOperationException(
                $"RavenDB Cloud needs an API key for '{deployment.Server.Name}'. Set the '{deployment.ApiKey.Name}' parameter.");
        }

        return context.Services.GetRequiredService<IRavenDBCloudApiClientFactory>().Create(Endpoint(context), apiKey);
    }

    /// <summary>
    /// The Cloud API: the production one, or another from <see cref="EndpointVariable"/> in the process environment.
    /// </summary>
    private static string Endpoint(PipelineStepContext context)
    {
        var endpoint = context.Services.GetRequiredService<IRavenDBCloudEnvironment>().Get(EndpointVariable) is { Length: > 0 } other
            ? other
            : DefaultEndpoint;

        // The API key has account-owner rights; plain HTTP is only for a local stand-in of the API.
        if (!Uri.TryCreate(endpoint, UriKind.Absolute, out var uri) || (uri.Scheme != Uri.UriSchemeHttps && !uri.IsLoopback))
        {
            throw new InvalidOperationException(
                $"The RavenDB Cloud API endpoint '{endpoint}' in {EndpointVariable} must be an https URL: the API key sent to it has account-owner rights.");
        }

        return endpoint;
    }

    /// <summary>The environment variable with the Cloud API endpoint.</summary>
    internal const string EndpointVariable = "RAVENDB_CLOUD_ENDPOINT";

    private const string DefaultEndpoint = "https://api.cloud.ravendb.net";

    private static RavenDBCloudProducts CreateProducts(RavenDBCloudApiClient client, PipelineStepContext context) =>
        new(client, context.Logger);
}

/// <summary>
/// The Aspire steps the RavenDB Cloud steps are ordered against. Aspire names them by convention only, so the names
/// live here: a rename in Aspire shows up in one place.
/// </summary>
internal static class AspireConventions
{
    /// <summary>The step in which a compute environment resolves its values into its artifacts.</summary>
    public static string Prepare(IResource environment) => $"prepare-{environment.Name}";

    /// <summary>A step of this integration, which must exist: a missing one is a bug, never something to skip.</summary>
    public static PipelineStep Required(PipelineConfigurationContext context, string name) =>
        context.Steps.FirstOrDefault(s => s.Name == name)
        ?? throw new DistributedApplicationException(
            $"The pipeline has no step '{name}', which the RavenDB Cloud integration orders its own steps against. This " +
            "version of the integration does not support the Aspire version in use.");
}
