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

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task EnvironmentConfigurationAppliesToAllCommandsRegardlessOfDeclarationOrder(bool configureFirst)
    {
        using var builder = TestDistributedApplicationBuilder.Create();
        var environment = builder.AddTerraformEnvironment("infra", "./module");

        if (configureFirst)
        {
            ConfigureEnvironment();
        }

        environment.WithInit().WithPlan().WithApply().WithDestroy();

        if (!configureFirst)
        {
            ConfigureEnvironment();
        }

        foreach (var annotation in environment.Resource.Annotations.OfType<TerraformCommandAnnotation>())
        {
            var command = annotation.Resource;
            Assert.Equal("custom-terraform", command.Command);
            var values = new Dictionary<string, object>();
            var context = new EnvironmentCallbackContext(builder.ExecutionContext, command, values, TestContext.Current.CancellationToken);
            foreach (var callback in command.Annotations.OfType<EnvironmentCallbackAnnotation>())
            {
                await callback.Callback(context);
            }

            Assert.Equal("staging", Assert.IsType<string>(values["TF_WORKSPACE"]));
        }

        void ConfigureEnvironment() => environment.WithWorkspace("staging").WithTerraformExecutablePath("custom-terraform");
    }

    [Fact]
    public void PipelineStepsDependOnlyOnParameterProcessingByDefaultAndAttachToDeployAndDestroy()
    {
        using var builder = TestDistributedApplicationBuilder.Create(DistributedApplicationOperation.Publish);
        var env = builder.AddTerraformEnvironment("infra", "./module").WithInit().WithPlan().WithApply().WithDestroy();
        var steps = TerraformPipelineExtensions.CreateSteps(env.Resource).ToDictionary(s => s.Name);

        Assert.Equal(4, steps.Count);
        Assert.All(steps.Values, step => Assert.Same(env.Resource, step.Resource));
        Assert.All(steps.Values, step => Assert.Equal([WellKnownPipelineSteps.ProcessParameters], step.DependsOnSteps));
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

        Assert.All(steps.Values, step => Assert.Contains(WellKnownPipelineSteps.ProcessParameters, step.DependsOnSteps));
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

    [Fact]
    public async Task ReconfiguringPlanPreservesItsPathAndApplyAppendsItAfterOptions()
    {
        using var builder = TestDistributedApplicationBuilder.Create();
        var environment = builder.AddTerraformEnvironment("infra", "./module")
            .WithApply(apply => apply.WithArgs("-lock-timeout=60s"))
            .WithPlan("./artifacts/review.tfplan")
            .WithPlan(configure: plan => plan.WithArgs("-lock-timeout=30s"))
            .WithApply(apply => apply.WithArgs("-parallelism=2"));
        var path = Path.GetFullPath("./artifacts/review.tfplan", builder.AppHostDirectory);
        Assert.Equal(path, environment.Resource.GetPlanPath());

        var apply = environment.Resource.GetCommandResource(TerraformCommand.Apply)!;
        var context = new CommandLineArgsCallbackContext([]) { ExecutionContext = builder.ExecutionContext };
        foreach (var annotation in apply.Annotations.OfType<CommandLineArgsCallbackAnnotation>())
        {
            await annotation.Callback(context);
        }
        Assert.Equal(["apply", "-input=false", "-lock-timeout=60s", "-parallelism=2", path], context.Args);

        environment.WithPlan("./artifacts/replacement.tfplan");
        Assert.Equal(Path.GetFullPath("./artifacts/replacement.tfplan", builder.AppHostDirectory), environment.Resource.GetPlanPath());
    }

    [Theory]
    [InlineData(new[] { "review.tfplan" }, false)]
    [InlineData(new[] { "-lock-timeout=60s", "review.tfplan" }, false)]
    [InlineData(new[] { "-lock-timeout", "60s", "review.tfplan" }, false)]
    [InlineData(new[] { "-invoke", "action.aws_lambda_invoke.test", "review.tfplan" }, false)]
    [InlineData(new[] { "-invoke=action.aws_lambda_invoke.test", "review.tfplan" }, false)]
    [InlineData(new[] { "--", "review.tfplan" }, false)]
    [InlineData(new[] { "-lock-timeout", "60s" }, true)]
    [InlineData(new[] { "-var", "name=value" }, true)]
    [InlineData(new[] { "-invoke", "action.aws_lambda_invoke.test" }, true)]
    [InlineData(new[] { "-invoke=action.aws_lambda_invoke.test" }, true)]
    [InlineData(new[] { "-auto-approve" }, true)]
    public async Task StandaloneVariableFileIsOmittedOnlyForSavedPlan(string[] arguments, bool expectVariableFile)
    {
        using var builder = TestDistributedApplicationBuilder.Create();
        var cli = builder.AddTerraformCli("cli", "./module")
            .WithEnvironment("TF_CLI_ARGS_apply", "-compact-warnings")
            .WithTfVarsFile("./inputs.tfvars")
            .WithApply(args: arguments);
        var values = new Dictionary<string, object>();
        var context = new EnvironmentCallbackContext(builder.ExecutionContext, cli.Resource, values, TestContext.Current.CancellationToken);
        foreach (var annotation in cli.Resource.Annotations.OfType<EnvironmentCallbackAnnotation>())
        {
            await annotation.Callback(context);
        }

        var value = values["TF_CLI_ARGS_apply"];
        var resolved = value is IValueProvider provider
            ? await provider.GetValueAsync(TestContext.Current.CancellationToken)
            : (string)value;
        Assert.Contains("-compact-warnings", resolved);
        Assert.Equal(expectVariableFile, resolved!.Contains("-var-file=", StringComparison.Ordinal));
    }
}