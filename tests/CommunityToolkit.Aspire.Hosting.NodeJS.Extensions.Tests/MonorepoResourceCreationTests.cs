using Aspire.Hosting;

namespace CommunityToolkit.Aspire.Hosting.NodeJS.Extensions.Tests;

public class MonorepoResourceCreationTests
{
    [Fact]
    public void AddNxApp_CreatesCorrectResourceType()
    {
        var builder = DistributedApplication.CreateBuilder();

        var nx = builder.AddNxApp("test-nx", workingDirectory: "../test-nx");

        using var app = builder.Build();

        var appModel = app.Services.GetRequiredService<DistributedApplicationModel>();
        var nxResource = Assert.Single(appModel.Resources.OfType<NxResource>());

        Assert.Equal("test-nx", nxResource.Name);
        Assert.NotEmpty(nxResource.WorkingDirectory);
    }

    [Fact]
    public void AddTurborepoApp_CreatesCorrectResourceType()
    {
        var builder = DistributedApplication.CreateBuilder();

        var turbo = builder.AddTurborepoApp("test-turbo", workingDirectory: "../test-turbo");

        using var app = builder.Build();

        var appModel = app.Services.GetRequiredService<DistributedApplicationModel>();
        var turborepoResource = Assert.Single(appModel.Resources.OfType<TurborepoResource>());

        Assert.Equal("test-turbo", turborepoResource.Name);
        Assert.NotEmpty(turborepoResource.WorkingDirectory);
    }

    [Fact]
    public void NxResource_DefaultWorkingDirectory()
    {
        var builder = DistributedApplication.CreateBuilder();

        var nx = builder.AddNxApp("my-nx");

        using var app = builder.Build();

        var appModel = app.Services.GetRequiredService<DistributedApplicationModel>();
        var nxResource = Assert.Single(appModel.Resources.OfType<NxResource>());

        Assert.Contains("my-nx", nxResource.WorkingDirectory);
    }

    [Fact]
    public void TurborepoResource_DefaultWorkingDirectory()
    {
        var builder = DistributedApplication.CreateBuilder();

        var turbo = builder.AddTurborepoApp("my-turbo");

        using var app = builder.Build();

        var appModel = app.Services.GetRequiredService<DistributedApplicationModel>();
        var turborepoResource = Assert.Single(appModel.Resources.OfType<TurborepoResource>());

        Assert.Contains("my-turbo", turborepoResource.WorkingDirectory);
    }

    [Fact]
    public void NxResource_IsRunningState()
    {
        var builder = DistributedApplication.CreateBuilder();

        var nx = builder.AddNxApp("nx-running");

        using var app = builder.Build();

        var appModel = app.Services.GetRequiredService<DistributedApplicationModel>();
        var nxResource = Assert.Single(appModel.Resources.OfType<NxResource>());

        Assert.True(nxResource.TryGetAnnotationsOfType<ResourceSnapshotAnnotation>(out var annotations));
        var annotation = Assert.Single(annotations);

        Assert.Equal(KnownResourceStates.Running, annotation.InitialSnapshot.State?.Text);
    }

    [Fact]
    public void TurborepoResource_IsRunningState()
    {
        var builder = DistributedApplication.CreateBuilder();

        var turbo = builder.AddTurborepoApp("turbo-running");

        using var app = builder.Build();

        var appModel = app.Services.GetRequiredService<DistributedApplicationModel>();
        var turboResource = Assert.Single(appModel.Resources.OfType<TurborepoResource>());

        Assert.True(turboResource.TryGetAnnotationsOfType<ResourceSnapshotAnnotation>(out var annotations));
        var annotation = Assert.Single(annotations);

        Assert.Equal(KnownResourceStates.Running, annotation.InitialSnapshot.State?.Text);
    }

    [Fact]
    public async Task Nx_RunWithPackageManager_InfersFromInstallerWhenNotProvided()
    {
        var builder = DistributedApplication.CreateBuilder();
        // Attach a yarn installer annotation to the Nx resource, then call RunWithPackageManager with no arg
        var nxBuilder = builder.AddNxApp("nx-with-installer")
            .WithYarnPackageInstaller()
            .RunWithPackageManager(); // no package manager passed, should infer from installer

        // Add an app to the Nx workspace to verify app-level command/args
        var app1 = nxBuilder.AddApp("app1");

        using var app = builder.Build();

        var appModel = app.Services.GetRequiredService<DistributedApplicationModel>();
        var nxResource = Assert.Single(appModel.Resources.OfType<NxResource>());

        // The Nx resource should have a JavaScriptPackageManagerAnnotation matching yarn
        Assert.True(nxResource.TryGetLastAnnotation<JavaScriptPackageManagerAnnotation>(out var pm));
        Assert.Equal("yarn", pm.PackageManager);

        // Verify the created NxAppResource command and args
        var nxAppResource = Assert.Single(appModel.Resources.OfType<NxAppResource>());
        // For yarn package manager, AddApp uses 'yarn' as the command
        Assert.Equal("yarn", nxAppResource.Command);
        var nxAppArgs = await nxAppResource.GetArgumentValuesAsync();
        Assert.Collection(nxAppArgs,
                arg => Assert.Equal("nx", arg),
                arg => Assert.Equal("serve", arg),
                arg => Assert.Equal("app1", arg));
    }

    [Fact]
    public void Nx_RunWithPackageManager_ThrowsWhenNotConfigured()
    {
        var builder = DistributedApplication.CreateBuilder();

        // No installer and no packageManager argument should cause an exception
        Assert.Throws<InvalidOperationException>(() => builder.AddNxApp("nx-no-installer").RunWithPackageManager());
    }

    [Fact]
    public async Task Turborepo_RunWithPackageManager_InfersFromInstallerWhenNotProvided()
    {
        var builder = DistributedApplication.CreateBuilder();

        var turbo = builder.AddTurborepoApp("turbo-default")
            .WithPnpmPackageInstaller()
            .RunWithPackageManager(); // should default to pnpm

        // Add an app to the Turborepo workspace to verify app-level command/args
        var app1 = turbo.AddApp("app1");

        using var app = builder.Build();

        var appModel = app.Services.GetRequiredService<DistributedApplicationModel>();
        var turboResource = Assert.Single(appModel.Resources.OfType<TurborepoResource>());

        Assert.True(turboResource.TryGetLastAnnotation<JavaScriptPackageManagerAnnotation>(out var pm));
        Assert.Equal("pnpm", pm.PackageManager);

        // Verify Turborepo app command and args
        var turboApp = Assert.Single(appModel.Resources.OfType<TurborepoAppResource>());
        // For pnpm package manager, AddApp uses 'pnpm' as the command
        Assert.Equal("pnpm", turboApp.Command);
        var turboArgs = await turboApp.GetArgumentValuesAsync();
        Assert.Collection(turboArgs,
                arg => Assert.Equal("turbo", arg),
                arg => Assert.Equal("run", arg),
                arg => Assert.Equal("dev", arg),
                arg => Assert.Equal("--filter", arg),
                arg => Assert.Equal("app1", arg));
    }

    [Fact]
    public async Task Nx_RunWithPackageManager_WithPnpmAndYarn()
    {
        var builder = DistributedApplication.CreateBuilder();

        var nxPnpm = builder.AddNxApp("nx-pnpm").WithPnpmPackageInstaller().RunWithPackageManager();
        var nxYarn = builder.AddNxApp("nx-yarn").WithYarnPackageInstaller().RunWithPackageManager();

        // add apps to both (use unique app names)
        nxPnpm.AddApp("app1-pnpm");
        nxYarn.AddApp("app1-yarn");

        using var app = builder.Build();

        var appModel = app.Services.GetRequiredService<DistributedApplicationModel>();

        var nxPnpmResource = appModel.Resources.OfType<NxResource>().Single(r => r.Name == "nx-pnpm");
        Assert.True(nxPnpmResource.TryGetLastAnnotation<JavaScriptPackageManagerAnnotation>(out var pmPnpm));
        Assert.Equal("pnpm", pmPnpm.PackageManager);

        var nxPnpmApp = appModel.Resources.OfType<NxAppResource>().Single(r => r.Name == "app1-pnpm");
        Assert.Equal("pnpm", nxPnpmApp.Command);
        var pnpmArgs = await nxPnpmApp.GetArgumentValuesAsync();
        Assert.Collection(pnpmArgs,
            arg => Assert.Equal("nx", arg),
            arg => Assert.Equal("serve", arg),
            arg => Assert.Equal("app1-pnpm", arg));

        var nxYarnResource = appModel.Resources.OfType<NxResource>().Single(r => r.Name == "nx-yarn");
        Assert.True(nxYarnResource.TryGetLastAnnotation<JavaScriptPackageManagerAnnotation>(out var pmYarn));
        Assert.Equal("yarn", pmYarn.PackageManager);

        var nxYarnApp = appModel.Resources.OfType<NxAppResource>().Single(r => r.Name == "app1-yarn");
        Assert.Equal("yarn", nxYarnApp.Command);
        var yarnArgs = await nxYarnApp.GetArgumentValuesAsync();
        Assert.Collection(yarnArgs,
            arg => Assert.Equal("nx", arg),
            arg => Assert.Equal("serve", arg),
            arg => Assert.Equal("app1-yarn", arg));
    }

    [Fact]
    public async Task Turborepo_RunWithPackageManager_WithPnpmAndYarn()
    {
        var builder = DistributedApplication.CreateBuilder();

        var turboPnpm = builder.AddTurborepoApp("turbo-pnpm").WithPnpmPackageInstaller().RunWithPackageManager();
        var turboYarn = builder.AddTurborepoApp("turbo-yarn").WithYarnPackageInstaller().RunWithPackageManager();

        turboPnpm.AddApp("app1-pnpm");
        turboYarn.AddApp("app1-yarn");

        using var app = builder.Build();

        var appModel = app.Services.GetRequiredService<DistributedApplicationModel>();

        var turboPnpmResource = appModel.Resources.OfType<TurborepoResource>().Single(r => r.Name == "turbo-pnpm");
        Assert.True(turboPnpmResource.TryGetLastAnnotation<JavaScriptPackageManagerAnnotation>(out var tpmPnpm));
        Assert.Equal("pnpm", tpmPnpm.PackageManager);

        var turboPnpmApp = appModel.Resources.OfType<TurborepoAppResource>().Single(r => r.Name == "app1-pnpm");
        Assert.Equal("pnpm", turboPnpmApp.Command);
        var tpnpmArgs = await turboPnpmApp.GetArgumentValuesAsync();
        Assert.Collection(tpnpmArgs,
            arg => Assert.Equal("turbo", arg),
            arg => Assert.Equal("run", arg),
            arg => Assert.Equal("dev", arg),
            arg => Assert.Equal("--filter", arg),
            arg => Assert.Equal("app1-pnpm", arg));

        var turboYarnResource = appModel.Resources.OfType<TurborepoResource>().Single(r => r.Name == "turbo-yarn");
        Assert.True(turboYarnResource.TryGetLastAnnotation<JavaScriptPackageManagerAnnotation>(out var tpmYarn));
        Assert.Equal("yarn", tpmYarn.PackageManager);

        var turboYarnApp = appModel.Resources.OfType<TurborepoAppResource>().Single(r => r.Name == "app1-yarn");
        Assert.Equal("yarn", turboYarnApp.Command);
        var tyarnArgs = await turboYarnApp.GetArgumentValuesAsync();
        Assert.Collection(tyarnArgs,
            arg => Assert.Equal("turbo", arg),
            arg => Assert.Equal("run", arg),
            arg => Assert.Equal("dev", arg),
            arg => Assert.Equal("--filter", arg),
            arg => Assert.Equal("app1-yarn", arg));
    }

    [Fact]
    public async Task NoRunWithPackageManager_Defaults_AppCommandsIncludeNpxOrDefault()
    {
        var builder = DistributedApplication.CreateBuilder();

        // Nx without RunWithPackageManager or installer
        var nx = builder.AddNxApp("nx-default");
        nx.AddApp("app-nx-default");

        // Turborepo without RunWithPackageManager or installer
        var turbo = builder.AddTurborepoApp("turbo-default");
        turbo.AddApp("app-turbo-default");

        using var app = builder.Build();

        var appModel = app.Services.GetRequiredService<DistributedApplicationModel>();

        var nxApp = appModel.Resources.OfType<NxAppResource>().Single(r => r.Name == "app-nx-default");
        // Command should be 'nx' (default) and args should include npx prefix because no package manager annotation
        Assert.Equal("nx", nxApp.Command);
        var nxArgs = await nxApp.GetArgumentValuesAsync();
        Assert.Collection(nxArgs,
            arg => Assert.Equal("serve", arg),
            arg => Assert.Equal("app-nx-default", arg));

        var turboApp = appModel.Resources.OfType<TurborepoAppResource>().Single(r => r.Name == "app-turbo-default");
        // Command should be 'turbo' (default) and args should include npx prefix
        Assert.Equal("turbo", turboApp.Command);
        var turboArgs = await turboApp.GetArgumentValuesAsync();
        Assert.Collection(turboArgs,
            arg => Assert.Equal("run", arg),
            arg => Assert.Equal("dev", arg),
            arg => Assert.Equal("--filter", arg),
            arg => Assert.Equal("app-turbo-default", arg));
    }
}