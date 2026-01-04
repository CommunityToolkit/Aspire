using Aspire.Hosting;
using Aspire.Hosting.JavaScript;

namespace CommunityToolkit.Aspire.Hosting.JavaScript.Extensions.Tests;

public class YarnWorkspaceResourceCreationTests
{
    [Fact]
    public void AddYarnWorkspaceApp_CreatesCorrectResourceType()
    {
        var builder = DistributedApplication.CreateBuilder();

        var yarn = builder.AddYarnWorkspaceApp("yarn-workspace", workingDirectory: "../yarn-workspace");

        using var app = builder.Build();

        var appModel = app.Services.GetRequiredService<DistributedApplicationModel>();
        var resource = Assert.Single(appModel.Resources.OfType<YarnWorkspaceResource>());

        Assert.Equal("yarn-workspace", resource.Name);
        Assert.NotEmpty(resource.WorkingDirectory);
    }

    [Fact]
    public void YarnWorkspaceResource_DefaultWorkingDirectory()
    {
        var builder = DistributedApplication.CreateBuilder();

        builder.AddYarnWorkspaceApp("yarn-default");

        using var app = builder.Build();

        var appModel = app.Services.GetRequiredService<DistributedApplicationModel>();
        var resource = Assert.Single(appModel.Resources.OfType<YarnWorkspaceResource>());

        Assert.Contains("yarn-default", resource.WorkingDirectory);
    }

    [Fact]
    public void YarnWorkspaceResource_IsRunningState()
    {
        var builder = DistributedApplication.CreateBuilder();

        builder.AddYarnWorkspaceApp("yarn-running");

        using var app = builder.Build();

        var appModel = app.Services.GetRequiredService<DistributedApplicationModel>();
        var resource = Assert.Single(appModel.Resources.OfType<YarnWorkspaceResource>());

        Assert.True(resource.TryGetAnnotationsOfType<ResourceSnapshotAnnotation>(out var annotations));
        var annotation = Assert.Single(annotations);

        Assert.Equal(KnownResourceStates.Running, annotation.InitialSnapshot.State?.Text);
    }

    [Fact]
    public async Task YarnWorkspaceApp_DefaultsToYarnCommandAndArgs()
    {
        var builder = DistributedApplication.CreateBuilder();

        var yarn = builder.AddYarnWorkspaceApp("yarn-app");
        yarn.AddApp("web");

        using var app = builder.Build();
        var appModel = app.Services.GetRequiredService<DistributedApplicationModel>();

        var workspace = Assert.Single(appModel.Resources.OfType<YarnWorkspaceResource>());
        Assert.True(workspace.TryGetLastAnnotation<JavaScriptPackageManagerAnnotation>(out var execution));
        Assert.Equal("yarn", execution.ExecutableName);

        var yarnApp = Assert.Single(appModel.Resources.OfType<YarnWorkspaceAppResource>());
        Assert.Equal("yarn", yarnApp.Command);
        var args = await yarnApp.GetArgumentValuesAsync();
        Assert.Collection(args,
            arg => Assert.Equal("workspace", arg),
            arg => Assert.Equal("web", arg),
            arg => Assert.Equal("run", arg),
            arg => Assert.Equal("dev", arg));
    }

    [Fact]
    public void YarnWorkspace_WithInstallFalse_DoesNotCreateInstaller()
    {
        var builder = DistributedApplication.CreateBuilder();

        builder.AddYarnWorkspaceApp("yarn-no-install", install: false);

        using var app = builder.Build();
        var appModel = app.Services.GetRequiredService<DistributedApplicationModel>();

        var workspace = Assert.Single(appModel.Resources.OfType<YarnWorkspaceResource>());
        Assert.True(workspace.TryGetLastAnnotation<JavaScriptPackageManagerConfiguredAnnotation>(out var configured));
        Assert.Equal("yarn", configured.PackageManager);
        Assert.True(workspace.TryGetLastAnnotation<JavaScriptPackageManagerAnnotation>(out var execution));
        Assert.Equal("yarn", execution.ExecutableName);

        Assert.False(workspace.TryGetLastAnnotation<JavaScriptPackageInstallerAnnotation>(out _));
        Assert.Empty(appModel.Resources.OfType<JavaScriptInstallerResource>());
    }

    [Fact]
    public void YarnWorkspace_WithInstallTrue_AppsWaitForInstaller()
    {
        var builder = DistributedApplication.CreateBuilder();

        var yarn = builder.AddYarnWorkspaceApp("yarn-install", install: true);
        yarn.AddApp("web");

        using var app = builder.Build();
        var appModel = app.Services.GetRequiredService<DistributedApplicationModel>();

        var workspace = Assert.Single(appModel.Resources.OfType<YarnWorkspaceResource>());
        Assert.True(workspace.TryGetLastAnnotation<JavaScriptPackageInstallerAnnotation>(out var installerAnnotation));

        var installer = installerAnnotation.Resource;
        Assert.NotNull(installer);
        Assert.Equal("yarn-install-installer", installer.Name);

        var yarnApp = Assert.Single(appModel.Resources.OfType<YarnWorkspaceAppResource>());
        Assert.True(yarnApp.TryGetAnnotationsOfType<WaitAnnotation>(out var waitAnnotations));
        var waitAnnotation = Assert.Single(waitAnnotations);
        Assert.Equal(installer, waitAnnotation.Resource);
    }
}
