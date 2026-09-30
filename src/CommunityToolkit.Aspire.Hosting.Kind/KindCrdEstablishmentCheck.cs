// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Net;
using System.Net.Http;
using Aspire.Hosting;
using Aspire.Hosting.ApplicationModel;
using k8s;
using k8s.Autorest;
using k8s.Models;
using Microsoft.Extensions.Logging;

namespace CommunityToolkit.Aspire.Hosting.Kind;

internal sealed class KindCrdEstablishmentCheck(
    ResourceLoggerService loggerService,
    Func<string, IKubernetes>? kubernetesFactory = null) : IKindPostApplyCheck
{
    private static readonly TimeSpan PollInterval = TimeSpan.FromMilliseconds(250);
    private readonly Func<string, IKubernetes> _kubernetesFactory = kubernetesFactory
        ?? (static kubeconfigPath => new Kubernetes(
            KubernetesClientConfiguration.BuildConfigFromConfigFile(kubeconfigPath)));

    public async Task CheckAsync(KindDeployedResource resource, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var crdNames = resource.TryGetLastAnnotation<KindDeploymentOutcomeAnnotation>(out var annotation)
            ? annotation.CrdNames
            : null;
        if (crdNames is null)
        {
            throw new InvalidOperationException($"Deployment '{resource.Name}' has not completed applying.");
        }

        if (crdNames.Count == 0)
        {
            return;
        }

        List<string> remainingNames = crdNames.Select(GetDefinitionName).Distinct(StringComparer.OrdinalIgnoreCase).ToList();
        resource.TryGetLastAnnotation<KindCrdWaitPolicyAnnotation>(out var waitPolicy);
        var timeout = KubectlTimeouts.Normalize(
            waitPolicy?.Options.Timeout ?? KubectlTimeouts.DefaultCrdWaitTimeout,
            nameof(CrdWaitOptions.Timeout));
        var failureBehavior = waitPolicy?.Options.FailureBehavior ?? CrdWaitBehavior.Fail;
        ILogger logger = loggerService.GetLogger(resource);
        logger.LogInformation(
            "Waiting for {CrdCount} custom resource definition(s) to become Established...",
            remainingNames.Count);

        IKubernetes client;
        try
        {
            client = _kubernetesFactory(resource.Parent.KubeconfigPath);
        }
        catch (Exception exception) when (failureBehavior == CrdWaitBehavior.BestEffort
            && !cancellationToken.IsCancellationRequested && exception is not OperationCanceledException)
        {
            logger.LogWarning(exception,
                "CRD readiness is unverified for deployment '{DeploymentName}' (BestEffort).",
                resource.Name);
            return;
        }

        using var waitCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        waitCts.CancelAfter(timeout);
        Task<(string Name, V1CustomResourceDefinition? Definition)>[]? activeReads = null;

        try
        {
            while (true)
            {
                activeReads = remainingNames.Select(async name =>
                    (Name: name, Definition: await ReadDefinitionAsync(client, name, waitCts.Token).ConfigureAwait(false)))
                    .ToArray();
                List<Task<(string Name, V1CustomResourceDefinition? Definition)>> pendingReads = [.. activeReads];
                List<string> nextRemaining = new(remainingNames.Count);
                while (pendingReads.Count > 0)
                {
                    var completed = await Task.WhenAny(pendingReads).WaitAsync(waitCts.Token).ConfigureAwait(false);
                    waitCts.Token.ThrowIfCancellationRequested();
                    pendingReads.Remove(completed);
                    (string name, V1CustomResourceDefinition? definition) result;
                    try
                    {
                        result = await completed.ConfigureAwait(false);
                    }
                    finally
                    {
                        waitCts.Token.ThrowIfCancellationRequested();
                    }
                    var (name, definition) = result;

                    if (definition?.Status?.Conditions?.Any(condition =>
                        condition.Type == "NamesAccepted" && condition.Status == "False") == true)
                    {
                        waitCts.Cancel();
                        throw new InvalidOperationException(
                            $"Kubernetes rejected the names for custom resource definition '{name}'.");
                    }

                    if (definition?.Status?.Conditions?.Any(condition =>
                        condition.Type == "Established" && condition.Status == "True") != true)
                    {
                        nextRemaining.Add(name);
                    }
                }
                activeReads = null;

                if (nextRemaining.Count == 0)
                {
                    return;
                }

                remainingNames = nextRemaining;
                await Task.Delay(PollInterval, waitCts.Token).ConfigureAwait(false);
            }
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            if (failureBehavior != CrdWaitBehavior.BestEffort)
            {
                throw new TimeoutException(
                    $"Timed out waiting for custom resource definition(s) to become Established after {timeout}.");
            }

            logger.LogWarning(
                "Timed out waiting for custom resource definition(s) to become Established after {Timeout}.",
                timeout);
            logger.LogWarning(
                "CRD readiness is unverified for deployment '{DeploymentName}' (BestEffort).",
                resource.Name);
        }
        catch (HttpOperationException exception) when (failureBehavior == CrdWaitBehavior.BestEffort && !cancellationToken.IsCancellationRequested)
        {
            logger.LogWarning(exception,
                "CRD readiness is unverified for deployment '{DeploymentName}' (BestEffort).",
                resource.Name);
        }
        catch (HttpRequestException exception) when (failureBehavior == CrdWaitBehavior.BestEffort && !cancellationToken.IsCancellationRequested)
        {
            logger.LogWarning(exception,
                "CRD readiness is unverified for deployment '{DeploymentName}' (BestEffort).",
                resource.Name);
        }
        catch (InvalidOperationException exception) when (failureBehavior == CrdWaitBehavior.BestEffort && !cancellationToken.IsCancellationRequested)
        {
            logger.LogWarning(exception,
                "CRD readiness is unverified for deployment '{DeploymentName}' (BestEffort).",
                resource.Name);
        }
        finally
        {
            if (activeReads is null)
            {
                client.Dispose();
            }
            else
            {
                waitCts.Cancel();
                // A read may ignore cancellation; keep its client alive until it completes.
                _ = Task.WhenAll(activeReads).ContinueWith(task =>
                {
                    if (task.Exception is not null)
                    {
                        logger.LogDebug(task.Exception, "Additional CRD reads failed after the post-apply check ended.");
                    }
                    client.Dispose();
                }, CancellationToken.None, TaskContinuationOptions.ExecuteSynchronously, TaskScheduler.Default);
            }
        }

        cancellationToken.ThrowIfCancellationRequested();
    }

    private static async Task<V1CustomResourceDefinition?> ReadDefinitionAsync(
        IKubernetes client,
        string name,
        CancellationToken cancellationToken)
    {
        try
        {
            return await client.ApiextensionsV1.ReadCustomResourceDefinitionAsync(
                name, cancellationToken: cancellationToken).ConfigureAwait(false);
        }
        catch (HttpOperationException exception) when (exception.Response?.StatusCode == HttpStatusCode.NotFound)
        {
            return null;
        }
    }

    private static string GetDefinitionName(string resourceName)
    {
        var separator = resourceName.IndexOf('/');
        if (separator < 0 || separator == resourceName.Length - 1)
        {
            throw new InvalidOperationException($"Invalid custom resource definition name '{resourceName}'.");
        }

        return resourceName[(separator + 1)..];
    }
}
