// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Aspire.Hosting;
using Aspire.Hosting.Utils;

namespace CommunityToolkit.Aspire.Hosting.GlitchTip.Tests;

public class DeploymentParameterTests
{
    [Fact]
    public async Task PublishDeclaresRequiredParametersWithoutResolvingThem()
    {
        using var builder = TestDistributedApplicationBuilder.Create(DistributedApplicationOperation.Publish);
        var project = AddProject(builder);
        var parameters = DeploymentParameters(project.Resource);

        Assert.Equal(["observability-url", "observability-organization", "observability-initial-team", "observability-api-token"], parameters.Select(parameter => parameter.Name));
        Assert.All(parameters, parameter => Assert.Contains(parameter, builder.Resources));
        Assert.All(parameters, parameter => Assert.Null(parameter.Default));
        Assert.Equal([false, false, false, true], parameters.Select(parameter => parameter.Secret));
        Assert.Null(project.Resource.LocalServer);
        Assert.Null(project.Resource.Management);
        Assert.False(project.Resource.Provisioned.Task.IsCompleted);

        foreach (var parameter in parameters)
        {
            var error = await Assert.ThrowsAsync<MissingParameterValueException>(() =>
                GlitchTipLifecycle.RequiredAsync(parameter, "deployment input", TestContext.Current.CancellationToken));
            Assert.Contains(parameter.Name, error.Message);
        }
    }

    [Fact]
    public void RunNeverAddsAutomaticDeploymentParameters()
    {
        using var builder = TestDistributedApplicationBuilder.Create(DistributedApplicationOperation.Run);
        var project = AddProject(builder);

        Assert.Empty(project.Resource.OwnedDeploymentParameters);
        Assert.Null(project.Resource.InstanceUrl);
        Assert.Null(project.Resource.ApiToken);
        Assert.Equal("observability-local-organization", project.Resource.Organization!.Name);
        Assert.Equal("observability-local-team", project.Resource.InitialTeam!.Name);
        Assert.DoesNotContain(builder.Resources, resource => resource.Name is "observability-url" or "observability-organization" or "observability-initial-team" or "observability-api-token");
    }

    [Theory]
    [InlineData("url")]
    [InlineData("organization")]
    [InlineData("initial-team")]
    [InlineData("api-token")]
    public void ReservedParameterCollisionIsRejectedBeforeAnyModelChanges(string suffix)
    {
        using var builder = TestDistributedApplicationBuilder.Create(DistributedApplicationOperation.Publish);
        var slug = builder.AddParameter("project-slug", "stack");
        var release = builder.AddParameter("release", "v1");
        var callerParameter = builder.AddParameter($"OBSERVABILITY-{suffix}", "caller-owned");
        var originalResources = builder.Resources.ToArray();

        Assert.Throws<ArgumentException>(() => builder.AddGlitchTip("observability", slug, release));

        Assert.Equal(originalResources, builder.Resources);
        Assert.Contains(callerParameter.Resource, builder.Resources);
        Assert.Empty(builder.Resources.OfType<GlitchTipResource>());
    }

    [Fact]
    public void CustomBindingsRemoveOnlyUnusedOwnedDefaultsAndRepeatedOverridesAreLastWins()
    {
        using var builder = TestDistributedApplicationBuilder.Create(DistributedApplicationOperation.Publish);
        var project = AddProject(builder);
        var defaults = DeploymentParameters(project.Resource);
        var retainedDefault = builder.CreateResourceBuilder(defaults[0]);
        var first = AddCustomParameters(builder, "first");

        Assert.Same(project, project.WithDeploymentParameters(retainedDefault, first[1], first[2], first[3]));
        Assert.Contains(defaults[0], builder.Resources);
        Assert.All(defaults.Skip(1), parameter => Assert.DoesNotContain(parameter, builder.Resources));
        Assert.All(first, parameter => Assert.Contains(parameter.Resource, builder.Resources));
        Assert.Equal([defaults[0], first[1].Resource, first[2].Resource, first[3].Resource], DeploymentParameters(project.Resource));

        var second = AddCustomParameters(builder, "second");
        Assert.Same(project, project.WithDeploymentParameters(second[0], second[1], second[2], second[3]));

        Assert.Empty(project.Resource.OwnedDeploymentParameters);
        Assert.All(defaults, parameter => Assert.DoesNotContain(parameter, builder.Resources));
        Assert.All(first.Concat(second), parameter => Assert.Contains(parameter.Resource, builder.Resources));
        Assert.Equal(second.Select(parameter => parameter.Resource), DeploymentParameters(project.Resource));
        Assert.Equal(10, builder.Resources.OfType<ParameterResource>().Count());
    }

    [Fact]
    public void ForeignBindingDoesNotRemoveOrReplaceDefaults()
    {
        using var builder = TestDistributedApplicationBuilder.Create(DistributedApplicationOperation.Publish);
        using var foreignBuilder = TestDistributedApplicationBuilder.Create(DistributedApplicationOperation.Publish);
        var project = AddProject(builder);
        var defaults = DeploymentParameters(project.Resource);
        var custom = AddCustomParameters(builder, "custom");
        var foreignToken = foreignBuilder.AddParameter("custom-token", "test-only", secret: true);
        var originalResources = builder.Resources.ToArray();

        Assert.Throws<ArgumentException>(() => project.WithDeploymentParameters(custom[0], custom[1], custom[2], foreignToken));

        Assert.Equal(defaults, DeploymentParameters(project.Resource));
        Assert.Equal(originalResources, builder.Resources);
    }

    [Fact]
    public void ForeignProjectBuilderCannotRewriteAnotherAppHostsBindings()
    {
        using var builder = TestDistributedApplicationBuilder.Create(DistributedApplicationOperation.Publish);
        using var foreignBuilder = TestDistributedApplicationBuilder.Create(DistributedApplicationOperation.Publish);
        var foreignProject = AddProject(foreignBuilder);
        var defaults = DeploymentParameters(foreignProject.Resource);
        var invalidBuilder = builder.CreateResourceBuilder(foreignProject.Resource);
        var custom = AddCustomParameters(builder, "custom");

        Assert.Throws<ArgumentException>(() => invalidBuilder.WithDeploymentParameters(custom[0], custom[1], custom[2], custom[3]));

        Assert.Equal(defaults, DeploymentParameters(foreignProject.Resource));
        Assert.All(defaults, parameter => Assert.Contains(parameter, foreignBuilder.Resources));
    }

    private static IResourceBuilder<GlitchTipResource> AddProject(IDistributedApplicationBuilder builder) =>
        builder.AddGlitchTip("observability", builder.AddParameter("project-slug", "stack"), builder.AddParameter("release", "v1"));

    private static ParameterResource[] DeploymentParameters(GlitchTipResource resource) =>
        [resource.InstanceUrl!, resource.Organization!, resource.InitialTeam!, resource.ApiToken!];

    private static IResourceBuilder<ParameterResource>[] AddCustomParameters(IDistributedApplicationBuilder builder, string prefix) =>
        [builder.AddParameter($"{prefix}-url", "https://unreachable.invalid/"), builder.AddParameter($"{prefix}-organization", "organization"), builder.AddParameter($"{prefix}-team", "team"), builder.AddParameter($"{prefix}-token", "test-only", secret: true)];
}