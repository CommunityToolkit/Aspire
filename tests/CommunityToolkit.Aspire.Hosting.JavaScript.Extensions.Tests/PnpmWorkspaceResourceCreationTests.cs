using Aspire.Hosting;
using Aspire.Hosting.JavaScript;

namespace CommunityToolkit.Aspire.Hosting.JavaScript.Extensions.Tests;

public class PnpmWorkspaceResourceCreationTests
{
    [Fact]
    public void AddPnpmWorkspaceApp_CreatesCorrectResourceType()
    {
        var builder = DistributedApplication.CreateBuilder();

        var pnpm = builder.AddPnpmWorkspaceApp("pnpm-workspace", workingDirectory: "../pnpm-workspace");

        using var app = builder.Build();

        var appModel = app.Services.GetRequiredService<DistributedApplicationModel>();
        var resource = Assert.Single(appModel.Resources.OfType<PnpmWorkspaceResource>());

        Assert.Equal("pnpm-workspace", resource.Name);
        Assert.NotEmpty(resource.WorkingDirectory);
    }

    [Fact]
    public void PnpmWorkspaceResource_DefaultWorkingDirectory()
    {
        var builder = DistributedApplication.CreateBuilder();

        builder.AddPnpmWorkspaceApp("pnpm-default");

        using var app = builder.Build();

        var appModel = app.Services.GetRequiredService<DistributedApplicationModel>();
        var resource = Assert.Single(appModel.Resources.OfType<PnpmWorkspaceResource>());

        Assert.Contains("pnpm-default", resource.WorkingDirectory);
    }

    [Fact]
    public void PnpmWorkspaceResource_IsRunningState()
    {
        var builder = DistributedApplication.CreateBuilder();

        builder.AddPnpmWorkspaceApp("pnpm-running");

        using var app = builder.Build();

        var appModel = app.Services.GetRequiredService<DistributedApplicationModel>();
        var resource = Assert.Single(appModel.Resources.OfType<PnpmWorkspaceResource>());

        Assert.True(resource.TryGetAnnotationsOfType<ResourceSnapshotAnnotation>(out var annotations));
        var annotation = Assert.Single(annotations);

        Assert.Equal(KnownResourceStates.Running, annotation.InitialSnapshot.State?.Text);
    }

    [Fact]
    public async Task PnpmWorkspaceApp_DefaultsToPnpmCommandAndArgs()
    {
        var builder = DistributedApplication.CreateBuilder();

        var pnpm = builder.AddPnpmWorkspaceApp("pnpm-app");
        pnpm.AddApp("web");

        using var app = builder.Build();
        var appModel = app.Services.GetRequiredService<DistributedApplicationModel>();

        var workspace = Assert.Single(appModel.Resources.OfType<PnpmWorkspaceResource>());
        Assert.True(workspace.TryGetLastAnnotation<JavaScriptPackageManagerAnnotation>(out var execution));
        Assert.Equal("pnpm", execution.ExecutableName);

        var pnpmApp = Assert.Single(appModel.Resources.OfType<PnpmWorkspaceAppResource>());
        Assert.Equal("pnpm", pnpmApp.Command);
        var args = await pnpmApp.GetArgumentValuesAsync();
        Assert.Collection(args,
            arg => Assert.Equal("--filter", arg),
            arg => Assert.Equal("web", arg),
            arg => Assert.Equal("run", arg),
            arg => Assert.Equal("dev", arg));
    }

    [Fact]
    public void PnpmWorkspace_WithInstallFalse_DoesNotCreateInstaller()
    {
        var builder = DistributedApplication.CreateBuilder();

        builder.AddPnpmWorkspaceApp("pnpm-no-install", install: false);

        using var app = builder.Build();
        var appModel = app.Services.GetRequiredService<DistributedApplicationModel>();

        var workspace = Assert.Single(appModel.Resources.OfType<PnpmWorkspaceResource>());
        Assert.True(workspace.TryGetLastAnnotation<JavaScriptPackageManagerConfiguredAnnotation>(out var configured));
        Assert.Equal("pnpm", configured.PackageManager);
        Assert.True(workspace.TryGetLastAnnotation<JavaScriptPackageManagerAnnotation>(out var execution));
        Assert.Equal("pnpm", execution.ExecutableName);

        Assert.False(workspace.TryGetLastAnnotation<JavaScriptPackageInstallerAnnotation>(out _));
        Assert.Empty(appModel.Resources.OfType<JavaScriptInstallerResource>());
    }

    [Fact]
    public void PnpmWorkspace_WithInstallTrue_AppsWaitForInstaller()
    {
        var builder = DistributedApplication.CreateBuilder();

        var pnpm = builder.AddPnpmWorkspaceApp("pnpm-install", install: true);
        pnpm.AddApp("web");

        using var app = builder.Build();
        var appModel = app.Services.GetRequiredService<DistributedApplicationModel>();

        var workspace = Assert.Single(appModel.Resources.OfType<PnpmWorkspaceResource>());
        Assert.True(workspace.TryGetLastAnnotation<JavaScriptPackageInstallerAnnotation>(out var installerAnnotation));

        var installer = installerAnnotation.Resource;
        Assert.NotNull(installer);
        Assert.Equal("pnpm-install-installer", installer.Name);

        var pnpmApp = Assert.Single(appModel.Resources.OfType<PnpmWorkspaceAppResource>());
        Assert.True(pnpmApp.TryGetAnnotationsOfType<WaitAnnotation>(out var waitAnnotations));
        var waitAnnotation = Assert.Single(waitAnnotations);
        Assert.Equal(installer, waitAnnotation.Resource);
    }
}
