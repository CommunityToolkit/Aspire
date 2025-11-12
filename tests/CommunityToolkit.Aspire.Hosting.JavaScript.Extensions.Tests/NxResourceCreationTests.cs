using Aspire.Hosting;
using Aspire.Hosting.JavaScript;

namespace CommunityToolkit.Aspire.Hosting.JavaScript.Extensions.Tests;

public class NxResourceCreationTests
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

    [Theory]
    [InlineData("npm")]
    [InlineData("yarn")]
    [InlineData("pnpm")]
    public async Task Nx_WithPackageManagerLaunch_InfersFromInstallerWhenNotProvided(string packageManager)
    {
        var builder = DistributedApplication.CreateBuilder();
        // Attach a yarn installer annotation to the Nx resource, then call WithPackageManagerLaunch with no arg
        var nxBuilder = builder.AddNxApp("nx-with-installer");

        nxBuilder = (packageManager switch
        {
            "npm" => nxBuilder.WithNpm(),
            "yarn" => nxBuilder.WithYarn(),
            "pnpm" => nxBuilder.WithPnpm(),
            _ => throw new ArgumentOutOfRangeException(nameof(packageManager), $"Unsupported package manager: {packageManager}"),
        }).WithPackageManagerLaunch();

        // Add an app to the Nx workspace to verify app-level command/args
        var app1 = nxBuilder.AddApp("app1");

        using var app = builder.Build();

        var appModel = app.Services.GetRequiredService<DistributedApplicationModel>();
        var nxResource = Assert.Single(appModel.Resources.OfType<NxResource>());

        // The Nx resource should have a JavaScriptPackageManagerAnnotation matching the inferred package manager
        Assert.True(nxResource.TryGetLastAnnotation<JavaScriptPackageManagerAnnotation>(out var pm));
        Assert.Equal(packageManager, pm.ExecutableName);

        // Verify the created NxAppResource command and args
        var nxAppResource = Assert.Single(appModel.Resources.OfType<NxAppResource>());
        // For the inferred package manager, AddApp uses the corresponding command
        Assert.Equal(packageManager switch
        {
            "npm" => "npx",
            "yarn" => "yarn",
            "pnpm" => "pnpx",
            _ => packageManager
        }, nxAppResource.Command);
        var nxAppArgs = await nxAppResource.GetArgumentValuesAsync();
        Assert.Collection(nxAppArgs,
                arg => Assert.Equal("nx", arg),
                arg => Assert.Equal("serve", arg),
                arg => Assert.Equal("app1", arg));
    }

    [Fact]
    public void Nx_WithPackageManagerLaunch_ThrowsWhenNotConfigured()
    {
        var builder = DistributedApplication.CreateBuilder();

        // No installer and no packageManager argument should cause an exception
        Assert.Throws<InvalidOperationException>(() => builder.AddNxApp("nx-no-installer").WithPackageManagerLaunch());
    }

    [Fact]
    public async Task Nx_WithPackageManagerLaunch_WithPnpmAndYarn()
    {
        var builder = DistributedApplication.CreateBuilder();

        var nxPnpm = builder.AddNxApp("nx-pnpm").WithPnpm().WithPackageManagerLaunch();
        var nxYarn = builder.AddNxApp("nx-yarn").WithYarn().WithPackageManagerLaunch();

        // add apps to both (use unique app names)
        nxPnpm.AddApp("app1-pnpm");
        nxYarn.AddApp("app1-yarn");

        using var app = builder.Build();

        var appModel = app.Services.GetRequiredService<DistributedApplicationModel>();

        var nxPnpmResource = appModel.Resources.OfType<NxResource>().Single(r => r.Name == "nx-pnpm");
        Assert.True(nxPnpmResource.TryGetLastAnnotation<JavaScriptPackageManagerAnnotation>(out var pmPnpm));
        Assert.Equal("pnpm", pmPnpm.ExecutableName);

        var nxPnpmApp = appModel.Resources.OfType<NxAppResource>().Single(r => r.Name == "app1-pnpm");
        Assert.Equal("pnpx", nxPnpmApp.Command);
        var pnpmArgs = await nxPnpmApp.GetArgumentValuesAsync();
        Assert.Collection(pnpmArgs,
            arg => Assert.Equal("nx", arg),
            arg => Assert.Equal("serve", arg),
            arg => Assert.Equal("app1-pnpm", arg));

        var nxYarnResource = appModel.Resources.OfType<NxResource>().Single(r => r.Name == "nx-yarn");
        Assert.True(nxYarnResource.TryGetLastAnnotation<JavaScriptPackageManagerAnnotation>(out var pmYarn));
        Assert.Equal("yarn", pmYarn.ExecutableName);

        var nxYarnApp = appModel.Resources.OfType<NxAppResource>().Single(r => r.Name == "app1-yarn");
        Assert.Equal("yarn", nxYarnApp.Command);
        var yarnArgs = await nxYarnApp.GetArgumentValuesAsync();
        Assert.Collection(yarnArgs,
            arg => Assert.Equal("nx", arg),
            arg => Assert.Equal("serve", arg),
            arg => Assert.Equal("app1-yarn", arg));
    }

    [Fact]
    public async Task Nx_NoWithPackageManagerLaunch_Defaults_AppCommandsIncludeNpxOrDefault()
    {
        var builder = DistributedApplication.CreateBuilder();

        // Nx without WithPackageManagerLaunch or installer
        var nx = builder.AddNxApp("nx-default");
        nx.AddApp("app-nx-default");

        using var app = builder.Build();

        var appModel = app.Services.GetRequiredService<DistributedApplicationModel>();

        var nxApp = appModel.Resources.OfType<NxAppResource>().Single(r => r.Name == "app-nx-default");
        // Command should be 'nx' (default) and args should include npx prefix because no package manager annotation
        Assert.Equal("nx", nxApp.Command);
        var nxArgs = await nxApp.GetArgumentValuesAsync();
        Assert.Collection(nxArgs,
            arg => Assert.Equal("serve", arg),
            arg => Assert.Equal("app-nx-default", arg));
    }

    [Theory]
    [InlineData("npm")]
    [InlineData("yarn")]
    [InlineData("pnpm")]
    public async Task Nx_WithPackageManager_WithoutRunWith_DoesNotAffectAppExecution(string packageManager)
    {
        var builder = DistributedApplication.CreateBuilder();

        // Configure package manager but don't call WithPackageManagerLaunch
        var nxBuilder = builder.AddNxApp("nx-configured-only");
        nxBuilder = packageManager switch
        {
            "npm" => nxBuilder.WithNpm(),
            "yarn" => nxBuilder.WithYarn(),
            "pnpm" => nxBuilder.WithPnpm(),
            _ => throw new ArgumentOutOfRangeException(nameof(packageManager))
        };

        // Add app - should use default "nx" command, not the package manager
        nxBuilder.AddApp("test-app");

        using var app = builder.Build();
        var appModel = app.Services.GetRequiredService<DistributedApplicationModel>();

        // Workspace should have configured annotation but not execution annotation
        var nxResource = Assert.Single(appModel.Resources.OfType<NxResource>());
        Assert.True(nxResource.TryGetLastAnnotation<JavaScriptPackageManagerConfiguredAnnotation>(out var configured));
        Assert.Equal(packageManager, configured.PackageManager);
        Assert.False(nxResource.TryGetLastAnnotation<JavaScriptPackageManagerAnnotation>(out _));

        // App should use "nx" directly, not wrapped via package manager
        var nxApp = Assert.Single(appModel.Resources.OfType<NxAppResource>());
        Assert.Equal("nx", nxApp.Command);
        var args = await nxApp.GetArgumentValuesAsync();
        Assert.Collection(args,
            arg => Assert.Equal("serve", arg),
            arg => Assert.Equal("test-app", arg));
    }

    [Fact]
    public void Nx_WithPackageManagerLaunch_ExplicitOverride_UsesExplicitValue()
    {
        var builder = DistributedApplication.CreateBuilder();

        // Configure with npm but explicitly run with yarn
        var nx = builder.AddNxApp("nx-override")
            .WithNpm()
            .WithPackageManagerLaunch("yarn");

        using var app = builder.Build();
        var appModel = app.Services.GetRequiredService<DistributedApplicationModel>();

        var nxResource = Assert.Single(appModel.Resources.OfType<NxResource>());

        // Should have configured annotation for npm
        Assert.True(nxResource.TryGetLastAnnotation<JavaScriptPackageManagerConfiguredAnnotation>(out var configured));
        Assert.Equal("npm", configured.PackageManager);

        // But execution annotation should be yarn (explicit override)
        Assert.True(nxResource.TryGetLastAnnotation<JavaScriptPackageManagerAnnotation>(out var execution));
        Assert.Equal("yarn", execution.ExecutableName);
    }

    [Fact]
    public void Nx_WithPackageManagerLaunch_CalledTwiceWithSame_DoesNotThrow()
    {
        var builder = DistributedApplication.CreateBuilder();

        var nx = builder.AddNxApp("nx-idempotent")
            .WithYarn()
            .WithPackageManagerLaunch()
            .WithPackageManagerLaunch(); // Second call with same inferred value

        using var app = builder.Build();
        var appModel = app.Services.GetRequiredService<DistributedApplicationModel>();

        var nxResource = Assert.Single(appModel.Resources.OfType<NxResource>());
        Assert.True(nxResource.TryGetLastAnnotation<JavaScriptPackageManagerAnnotation>(out var execution));
        Assert.Equal("yarn", execution.ExecutableName);
    }

    [Fact]
    public void Nx_WithPackageManagerLaunch_CalledTwiceWithDifferent_Throws()
    {
        var builder = DistributedApplication.CreateBuilder();

        var nx = builder.AddNxApp("nx-conflict")
            .WithYarn()
            .WithPackageManagerLaunch(); // Sets yarn

        // Trying to change to pnpm should throw
        var ex = Assert.Throws<InvalidOperationException>(() => nx.WithPackageManagerLaunch("pnpm"));
        Assert.Contains("already configured to run with the 'yarn' package manager", ex.Message);
    }

    [Theory]
    [InlineData("npm")]
    [InlineData("yarn")]
    [InlineData("pnpm")]
    public async Task Nx_MultipleApps_AllInheritExecutionAnnotation(string packageManager)
    {
        var builder = DistributedApplication.CreateBuilder();

        var nx = builder.AddNxApp("nx-multi");
        nx = packageManager switch
        {
            "npm" => nx.WithNpm(),
            "yarn" => nx.WithYarn(),
            "pnpm" => nx.WithPnpm(),
            _ => throw new ArgumentOutOfRangeException(nameof(packageManager))
        };
        nx = nx.WithPackageManagerLaunch();

        nx.AddApp("app1");
        nx.AddApp("app2");
        nx.AddApp("app3");

        using var app = builder.Build();
        var appModel = app.Services.GetRequiredService<DistributedApplicationModel>();

        var apps = appModel.Resources.OfType<NxAppResource>().ToList();
        Assert.Equal(3, apps.Count);

        foreach (var nxApp in apps)
        {
            var launcherName = packageManager switch
            {
                "npm" => "npx",
                "yarn" => "yarn",
                "pnpm" => "pnpx",
                _ => throw new ArgumentOutOfRangeException(nameof(packageManager))
            };

            Assert.Equal(launcherName, nxApp.Command);
            var args = await nxApp.GetArgumentValuesAsync();
            Assert.Collection(args,
                arg => Assert.Equal("nx", arg),
                arg => Assert.Equal("serve", arg),
                arg => Assert.Equal(nxApp.Name, arg));
        }
    }

    [Fact]
    public void Nx_WithNpm_WithoutInstallFlag_DoesNotCreateInstallerResource()
    {
        var builder = DistributedApplication.CreateBuilder();

        var nx = builder.AddNxApp("nx-no-installer")
            .WithNpm(); // install defaults to false

        using var app = builder.Build();
        var appModel = app.Services.GetRequiredService<DistributedApplicationModel>();

        // Should have configured annotation
        var nxResource = Assert.Single(appModel.Resources.OfType<NxResource>());
        Assert.True(nxResource.TryGetLastAnnotation<JavaScriptPackageManagerConfiguredAnnotation>(out _));

        // But no installer annotation or resource
        Assert.False(nxResource.TryGetLastAnnotation<JavaScriptPackageInstallerAnnotation>(out _));
        Assert.Empty(appModel.Resources.OfType<JavaScriptInstallerResource>());
    }

    [Fact]
    public void Nx_ConfiguredAnnotation_AddedEvenWithInstallFalse()
    {
        var builder = DistributedApplication.CreateBuilder();

        var nx = builder.AddNxApp("nx-configured")
            .WithPnpm(install: false);

        using var app = builder.Build();
        var appModel = app.Services.GetRequiredService<DistributedApplicationModel>();

        var nxResource = Assert.Single(appModel.Resources.OfType<NxResource>());

        // Configured annotation should always be present
        Assert.True(nxResource.TryGetLastAnnotation<JavaScriptPackageManagerConfiguredAnnotation>(out var configured));
        Assert.Equal("pnpm", configured.PackageManager);

        // But no installer
        Assert.False(nxResource.TryGetLastAnnotation<JavaScriptPackageInstallerAnnotation>(out _));
    }

    [Fact]
    public void Nx_WithInstallTrue_AppsWaitForInstaller()
    {
        var builder = DistributedApplication.CreateBuilder();

        var nx = builder.AddNxApp("nx-with-install")
            .WithYarn(install: true)
            .WithPackageManagerLaunch();

        nx.AddApp("app1");

        using var app = builder.Build();
        var appModel = app.Services.GetRequiredService<DistributedApplicationModel>();

        // Verify installer resource exists
        var nxResource = Assert.Single(appModel.Resources.OfType<NxResource>());
        Assert.True(nxResource.TryGetLastAnnotation<JavaScriptPackageInstallerAnnotation>(out var installerAnnotation));

        var installer = installerAnnotation.Resource;
        Assert.NotNull(installer);
        Assert.Equal("nx-with-install-installer", installer.Name);

        // Verify app has WaitFor relationship
        var nxApp = Assert.Single(appModel.Resources.OfType<NxAppResource>());
        Assert.True(nxApp.TryGetAnnotationsOfType<WaitAnnotation>(out var waitAnnotations));
        var waitAnnotation = Assert.Single(waitAnnotations);
        Assert.Equal(installer, waitAnnotation.Resource);
    }
}
