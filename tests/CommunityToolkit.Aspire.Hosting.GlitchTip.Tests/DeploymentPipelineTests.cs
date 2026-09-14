// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#pragma warning disable ASPIREPIPELINES001

using Aspire.Hosting;
using Aspire.Hosting.Docker;
using Aspire.Hosting.Pipelines;
using Aspire.Hosting.Utils;
using CommunityToolkit.Aspire.Hosting.GlitchTip.Deployment;
using Microsoft.Extensions.Logging.Abstractions;

namespace CommunityToolkit.Aspire.Hosting.GlitchTip.Tests;

public class DeploymentPipelineTests
{
    [Fact]
    public void ComposePublishesWithoutManagementAndResolvesBeforeRollout()
    {
        var resource = new ContainerResource("glitchtip");
        var compose = new DockerComposeEnvironmentResource("compose");
        var calls = 0;
        var steps = GlitchTipDeploymentPipeline.CreateSteps(resource, _ => { calls++; return Task.CompletedTask; }, _ => { calls++; return Task.CompletedTask; }).ToList();
        steps.AddRange([
            new PipelineStep { Name = "publish", Action = _ => Task.CompletedTask },
            new PipelineStep { Name = "build", Action = _ => Task.CompletedTask },
            new PipelineStep { Name = "prepare-compose", DependsOnSteps = ["publish", "build"], Action = _ => Task.CompletedTask },
            new PipelineStep { Name = "docker-compose-up-compose", DependsOnSteps = ["prepare-compose"], Action = _ => Task.CompletedTask }
        ]);
        using var services = new ServiceCollection().BuildServiceProvider();
        GlitchTipDeploymentPipeline.ConfigureDependencies(resource, new PipelineConfigurationContext
        {
            Services = services,
            Model = new DistributedApplicationModel((IEnumerable<IResource>)[resource, compose]),
            Steps = steps
        });

        Assert.Equal(0, calls);
        Assert.Empty(steps.Single(step => step.Name == "publish").DependsOnSteps);
        Assert.Contains("glitchtip-provision-glitchtip", steps.Single(step => step.Name == "prepare-compose").DependsOnSteps);
        var synchronize = steps.Single(step => step.Name == "glitchtip-synchronize-glitchtip");
        Assert.Contains("prepare-compose", synchronize.DependsOnSteps);
        Assert.Contains("build", synchronize.DependsOnSteps);
        Assert.Contains(synchronize.Name, steps.Single(step => step.Name == "docker-compose-up-compose").DependsOnSteps);
        Assert.All(steps.Take(2), step => Assert.DoesNotContain("publish", step.RequiredBySteps));
    }

    [Fact]
    public async Task ManagementOnlyAppHostRunsWithoutAComputeEnvironment()
    {
        using var builder = TestDistributedApplicationBuilder.Create(DistributedApplicationOperation.Publish);
        var resource = builder.AddResource(new ContainerResource("glitchtip"));
        var calls = new List<string>();
        var steps = GlitchTipDeploymentPipeline.CreateSteps(resource.Resource,
            _ => { calls.Add("provision"); return Task.CompletedTask; },
            _ => { calls.Add("synchronize"); return Task.CompletedTask; });
        using var services = new ServiceCollection().BuildServiceProvider();
        var model = new DistributedApplicationModel((IEnumerable<IResource>)[resource.Resource]);
        GlitchTipDeploymentPipeline.ConfigureDependencies(resource.Resource, new PipelineConfigurationContext
        {
            Services = services,
            Model = model,
            Steps = steps
        });
        var context = new PipelineStepContext
        {
            PipelineContext = new PipelineContext(model, builder.ExecutionContext, services, NullLogger.Instance, default),
            ReportingStep = null!
        };

        Assert.Equal(["glitchtip-provision-glitchtip", "build"], steps[1].DependsOnSteps);
        Assert.DoesNotContain(steps, step => step.Name.Contains("compose", StringComparison.Ordinal));
        await steps[0].Action(context);
        await steps[1].Action(context);
        Assert.Equal(["provision", "synchronize"], calls);
    }
    [Fact]
    public async Task UnsupportedComputeFailsBeforeManagementCalls()
    {
        using var builder = TestDistributedApplicationBuilder.Create(DistributedApplicationOperation.Publish);
        var resource = new ContainerResource("glitchtip");
        var calls = 0;
        var steps = GlitchTipDeploymentPipeline.CreateSteps(resource,
            _ => { calls++; return Task.CompletedTask; }, _ => Task.CompletedTask);
        using var services = new ServiceCollection().BuildServiceProvider();
        var model = new DistributedApplicationModel((IEnumerable<IResource>)[resource, new UnsupportedCompute("unsupported")]);
        var context = new PipelineStepContext
        {
            PipelineContext = new PipelineContext(model, builder.ExecutionContext, services, NullLogger.Instance, default),
            ReportingStep = null!
        };

        var error = await Assert.ThrowsAsync<DistributedApplicationException>(() => steps[0].Action(context));
        Assert.Contains("Docker Compose", error.Message);
        Assert.Equal(0, calls);
    }

    private sealed class UnsupportedCompute(string name) : Resource(name), IComputeEnvironmentResource;
    [Theory]
    [InlineData(9090, "http://api:9090")]
    [InlineData(null, "http://api:8080")]
    public async Task ComposeMonitorReferenceUsesTargetPortWithoutLocalAllocation(int? targetPort, string expected)
    {
        using var builder = TestDistributedApplicationBuilder.Create(DistributedApplicationOperation.Publish);
        var resource = builder.AddResource(new ProjectResource("api"))
            .WithHttpEndpoint(port: 5215, targetPort: targetPort);
        var environment = new DockerComposeEnvironmentResource("compose");
        var service = new DockerComposeServiceResource("api-compose", resource.Resource, environment);
        resource.Resource.Annotations.Add(new DeploymentTargetAnnotation(service) { ComputeEnvironment = environment });
        using var services = new ServiceCollection().BuildServiceProvider();
        var model = new DistributedApplicationModel((IEnumerable<IResource>)[resource.Resource, environment]);
        var context = new PipelineStepContext
        {
            PipelineContext = new PipelineContext(model, builder.ExecutionContext, services, NullLogger.Instance, default),
            ReportingStep = null!
        };
        var endpoint = resource.GetEndpoint("http");
        Assert.False(endpoint.IsAllocated);
        var actual = await GlitchTipDeploymentPipeline.ResolveEndpointAsync(context, endpoint);
        Assert.Equal(expected, actual);
    }
    [Theory]
    [InlineData(true, "http://api:9090/health?token=fictional%2Fvalue%3F%26&literal={ok}")]
    [InlineData(false, "http://api:9090/ready?token=fictional%2Fvalue%3F%26&literal={ok}")]
    public async Task ComposeMonitorUrlExpressionTranslatesNestedEndpointPropertiesAndSecretParameters(bool wholeEndpoint, string expected)
    {
        using var builder = TestDistributedApplicationBuilder.Create(DistributedApplicationOperation.Publish);
        var api = builder.AddResource(new ProjectResource("api")).WithHttpEndpoint(port: 5215, targetPort: 9090);
        var unused = builder.AddResource(new ProjectResource("unused")).WithHttpEndpoint();
        var token = builder.AddParameter("token", () => "fictional/value?&", secret: true);
        var enabled = builder.AddParameter("enabled", () => "YES");
        var environment = new DockerComposeEnvironmentResource("compose");
        var service = new DockerComposeServiceResource("api-compose", api.Resource, environment);
        api.Resource.Annotations.Add(new DeploymentTargetAnnotation(service) { ComputeEnvironment = environment });
        using var services = new ServiceCollection().BuildServiceProvider();
        var model = new DistributedApplicationModel((IEnumerable<IResource>)[api.Resource, environment]);
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        var context = new PipelineStepContext
        {
            PipelineContext = new PipelineContext(model, builder.ExecutionContext, services, NullLogger.Instance, timeout.Token),
            ReportingStep = null!
        };
        var endpoint = api.GetEndpoint("http");
        var nested = wholeEndpoint
            ? ReferenceExpression.Create($"{endpoint}/health")
            : ReferenceExpression.Create($"{endpoint.Property(EndpointProperty.Scheme)}://{endpoint.Property(EndpointProperty.Host)}:{endpoint.Property(EndpointProperty.Port)}/ready");
        var chosen = ReferenceExpression.Create($"{nested}?token={token:uri}&literal={{ok}}");
        var expression = ReferenceExpression.CreateConditional(enabled.Resource, "yes", chosen,
            ReferenceExpression.Create($"{unused.GetEndpoint("http")}/never-evaluated"));

        Assert.False(endpoint.IsAllocated);
        Assert.Equal(expected, await GlitchTipDeploymentPipeline.ResolveExpressionAsync(context, expression));
    }

    [Fact]
    public void RunDoesNotRegisterDeploymentActions()
    {
        using var builder = TestDistributedApplicationBuilder.Create(DistributedApplicationOperation.Run);
        var resource = builder.AddResource(new ContainerResource("glitchtip"));
        GlitchTipDeploymentPipeline.Configure(resource, _ => throw new InvalidOperationException(), _ => throw new InvalidOperationException());
        Assert.Empty(resource.Resource.Annotations.OfType<PipelineStepAnnotation>());
        Assert.Empty(resource.Resource.Annotations.OfType<PipelineConfigurationAnnotation>());
    }

    [Fact]
    public void PublishRegistersDeferredActionsWithoutInvokingThem()
    {
        using var builder = TestDistributedApplicationBuilder.Create(DistributedApplicationOperation.Publish);
        var resource = builder.AddResource(new ContainerResource("glitchtip"));
        GlitchTipDeploymentPipeline.Configure(resource, _ => throw new InvalidOperationException(), _ => throw new InvalidOperationException());
        Assert.Single(resource.Resource.Annotations.OfType<PipelineStepAnnotation>());
        Assert.Single(resource.Resource.Annotations.OfType<PipelineConfigurationAnnotation>());
    }
}