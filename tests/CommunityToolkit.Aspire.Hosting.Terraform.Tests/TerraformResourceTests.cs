// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#pragma warning disable ASPIREPIPELINES001

using Aspire.Hosting;
using Aspire.Hosting.Pipelines;
using Aspire.Hosting.Utils;

namespace CommunityToolkit.Aspire.Hosting.Terraform.Tests;

public class TerraformResourceTests
{
    [Fact]
    public void EnvironmentCreatesCommandsOnceAndWiresDependencies()
    {
        using var builder = TestDistributedApplicationBuilder.Create();
        var environment = builder.AddTerraformEnvironment("infra", "./module")
            .WithApply()
            .WithInit()
            .WithPlan()
            .WithPlan(configure: plan => plan.WithArgs("-lock-timeout=60s"))
            .WithDestroy();

        var commands = environment.Resource.Annotations.OfType<TerraformCommandAnnotation>().ToDictionary(a => a.Command, a => a.Resource);
        Assert.Equal(4, commands.Count);
        Assert.Equal(Path.GetFullPath("./module", builder.AppHostDirectory), environment.Resource.WorkingDirectory);
        Assert.Contains(commands[TerraformCommand.Plan].Annotations.OfType<WaitAnnotation>(), a => a.Resource == commands[TerraformCommand.Init]);
        Assert.Contains(commands[TerraformCommand.Apply].Annotations.OfType<WaitAnnotation>(), a => a.Resource == commands[TerraformCommand.Plan]);
        Assert.Contains(commands[TerraformCommand.Destroy].Annotations.OfType<ExplicitStartupAnnotation>(), _ => true);
        Assert.Equal(Path.Combine(environment.Resource.WorkingDirectory, "infra.tfplan"), environment.Resource.GetPlanPath());
    }

    [Fact]
    public void PipelineStepsAreIndependentByDefaultAndAttachToDeployAndDestroy()
    {
        using var builder = TestDistributedApplicationBuilder.Create(DistributedApplicationOperation.Publish);
        var env = builder.AddTerraformEnvironment("infra", "./module").WithInit().WithPlan().WithApply().WithDestroy();
        var steps = TerraformPipelineExtensions.CreateSteps(env.Resource).ToDictionary(s => s.Name);

        Assert.Equal(4, steps.Count);
        Assert.All(steps.Values, step => Assert.Same(env.Resource, step.Resource));
        Assert.All(steps.Values, step => Assert.Empty(step.DependsOnSteps));
        Assert.Contains(WellKnownPipelineSteps.Deploy, steps["terraform-apply-infra"].RequiredBySteps);
        Assert.Contains(WellKnownPipelineSteps.Destroy, steps["terraform-destroy-infra"].RequiredBySteps);
        Assert.Contains(env.Resource.Annotations, a => a is PipelineStepAnnotation);
    }

    [Fact]
    public void PipelineChainingUsesDeclaredCommands()
    {
        using var builder = TestDistributedApplicationBuilder.Create(DistributedApplicationOperation.Publish);
        var env = builder.AddTerraformEnvironment("infra", "./module").WithApply().WithInit().WithPlan().WithDestroy()
            .WithPipelineOptions(options =>
            {
                options.ChainSteps = true;
                options.RequiredByDeploy = false;
                options.RequiredByDestroy = false;
            });
        var steps = TerraformPipelineExtensions.CreateSteps(env.Resource).ToDictionary(s => s.Name);

        Assert.Contains("terraform-init-infra", steps["terraform-plan-infra"].DependsOnSteps);
        Assert.Contains("terraform-plan-infra", steps["terraform-apply-infra"].DependsOnSteps);
        Assert.Contains("terraform-init-infra", steps["terraform-destroy-infra"].DependsOnSteps);
        Assert.DoesNotContain(WellKnownPipelineSteps.Deploy, steps["terraform-apply-infra"].RequiredBySteps);
        Assert.DoesNotContain(WellKnownPipelineSteps.Destroy, steps["terraform-destroy-infra"].RequiredBySteps);
    }

    [Fact]
    public void StandaloneCommandSelectionReplacesPreviousCommand()
    {
        using var builder = TestDistributedApplicationBuilder.Create();
        var cli = builder.AddTerraformCli("cli", "./module").WithInit("-upgrade").WithPlan("-out=review.tfplan");

        Assert.Equal(["plan", "-out=review.tfplan"], cli.Resource.SelectedArguments);
        Assert.Equal(Path.GetFullPath("./module", builder.AppHostDirectory), cli.Resource.WorkingDirectory);
    }
}
