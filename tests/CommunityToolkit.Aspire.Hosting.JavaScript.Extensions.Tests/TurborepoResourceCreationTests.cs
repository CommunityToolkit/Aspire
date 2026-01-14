using Aspire.Hosting;
using Aspire.Hosting.JavaScript;
using CommunityToolkit.Aspire.Testing;

namespace CommunityToolkit.Aspire.Hosting.JavaScript.Extensions.Tests;

public class TurborepoResourceCreationTests
{
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

    [Theory]
    [InlineData("npm")]
    [InlineData("yarn")]
    [InlineData("pnpm")]
    public async Task Turborepo_WithPackageManagerLaunch_InfersFromInstallerWhenNotProvided(string packageManager)
    {
        var builder = DistributedApplication.CreateBuilder();

        var turbo = builder.AddTurborepoApp("turbo-default");

        turbo = (packageManager switch
        {
            "npm" => turbo.WithNpm(),
            "yarn" => turbo.WithYarn(),
            "pnpm" => turbo.WithPnpm(),
            _ => throw new ArgumentOutOfRangeException(nameof(packageManager), $"Unsupported package manager: {packageManager}"),
        }).WithPackageManagerLaunch();

        // Add an app to the Turborepo workspace to verify app-level command/args
        var app1 = turbo.AddApp("app1");

        using var app = builder.Build();

        var appModel = app.Services.GetRequiredService<DistributedApplicationModel>();
        var turboResource = Assert.Single(appModel.Resources.OfType<TurborepoResource>());

        Assert.True(turboResource.TryGetLastAnnotation<JavaScriptPackageManagerAnnotation>(out var pm));
        Assert.Equal(packageManager, pm.ExecutableName);

        // Verify Turborepo app command and args
        var turboApp = Assert.Single(appModel.Resources.OfType<TurborepoAppResource>());
        // For the inferred package manager, AddApp uses the corresponding command
        Assert.Equal(packageManager switch
        {
            "npm" => "npx",
            "yarn" => "yarn",
            "pnpm" => "pnpx",
            _ => packageManager
        }, turboApp.Command);
        var turboArgs = await turboApp.GetArgumentListAsync();
        Assert.Collection(turboArgs,
            arg => Assert.Equal("turbo", arg),
            arg => Assert.Equal("run", arg),
            arg => Assert.Equal("dev", arg),
            arg => Assert.Equal("--filter", arg),
            arg => Assert.Equal("app1", arg));
    }

    [Fact]
    public async Task Turborepo_WithPackageManagerLaunch_WithPnpmAndYarn()
    {
        var builder = DistributedApplication.CreateBuilder();

        var turboPnpm = builder.AddTurborepoApp("turbo-pnpm").WithPnpm().WithPackageManagerLaunch();
        var turboYarn = builder.AddTurborepoApp("turbo-yarn").WithYarn().WithPackageManagerLaunch();

        turboPnpm.AddApp("app1-pnpm");
        turboYarn.AddApp("app1-yarn");

        using var app = builder.Build();

        var appModel = app.Services.GetRequiredService<DistributedApplicationModel>();

        var turboPnpmResource = appModel.Resources.OfType<TurborepoResource>().Single(r => r.Name == "turbo-pnpm");
        Assert.True(turboPnpmResource.TryGetLastAnnotation<JavaScriptPackageManagerAnnotation>(out var tpmPnpm));
        Assert.Equal("pnpm", tpmPnpm.ExecutableName);

        var turboPnpmApp = appModel.Resources.OfType<TurborepoAppResource>().Single(r => r.Name == "app1-pnpm");
        Assert.Equal("pnpx", turboPnpmApp.Command);
        var tpnpmArgs = await turboPnpmApp.GetArgumentListAsync();
        Assert.Collection(tpnpmArgs,
            arg => Assert.Equal("turbo", arg),
            arg => Assert.Equal("run", arg),
            arg => Assert.Equal("dev", arg),
            arg => Assert.Equal("--filter", arg),
            arg => Assert.Equal("app1-pnpm", arg));

        var turboYarnResource = appModel.Resources.OfType<TurborepoResource>().Single(r => r.Name == "turbo-yarn");
        Assert.True(turboYarnResource.TryGetLastAnnotation<JavaScriptPackageManagerAnnotation>(out var tpmYarn));
        Assert.Equal("yarn", tpmYarn.ExecutableName);

        var turboYarnApp = appModel.Resources.OfType<TurborepoAppResource>().Single(r => r.Name == "app1-yarn");
        Assert.Equal("yarn", turboYarnApp.Command);
        var tyarnArgs = await turboYarnApp.GetArgumentListAsync();
        Assert.Collection(tyarnArgs,
            arg => Assert.Equal("turbo", arg),
            arg => Assert.Equal("run", arg),
            arg => Assert.Equal("dev", arg),
            arg => Assert.Equal("--filter", arg),
            arg => Assert.Equal("app1-yarn", arg));
    }

    [Fact]
    public async Task Turborepo_NoWithPackageManagerLaunch_Defaults_AppCommandsIncludeNpxOrDefault()
    {
        var builder = DistributedApplication.CreateBuilder();

        // Turborepo without WithPackageManagerLaunch or installer
        var turbo = builder.AddTurborepoApp("turbo-default");
        turbo.AddApp("app-turbo-default");

        using var app = builder.Build();

        var appModel = app.Services.GetRequiredService<DistributedApplicationModel>();

        var turboApp = appModel.Resources.OfType<TurborepoAppResource>().Single(r => r.Name == "app-turbo-default");
        // Command should be 'turbo' (default) and args should include npx prefix
        Assert.Equal("turbo", turboApp.Command);
        var turboArgs = await turboApp.GetArgumentListAsync();
        Assert.Collection(turboArgs,
            arg => Assert.Equal("run", arg),
            arg => Assert.Equal("dev", arg),
            arg => Assert.Equal("--filter", arg),
            arg => Assert.Equal("app-turbo-default", arg));
    }

    [Theory]
    [InlineData("npm")]
    [InlineData("yarn")]
    [InlineData("pnpm")]
    public async Task Turborepo_WithPackageManager_WithoutRunWith_DoesNotAffectAppExecution(string packageManager)
    {
        var builder = DistributedApplication.CreateBuilder();

        var turboBuilder = builder.AddTurborepoApp("turbo-configured-only");
        turboBuilder = packageManager switch
        {
            "npm" => turboBuilder.WithNpm(),
            "yarn" => turboBuilder.WithYarn(),
            "pnpm" => turboBuilder.WithPnpm(),
            _ => throw new ArgumentOutOfRangeException(nameof(packageManager))
        };

        turboBuilder.AddApp("test-app");

        using var app = builder.Build();
        var appModel = app.Services.GetRequiredService<DistributedApplicationModel>();

        // Workspace should have configured annotation but not execution annotation
        var turboResource = Assert.Single(appModel.Resources.OfType<TurborepoResource>());
        Assert.True(turboResource.TryGetLastAnnotation<JavaScriptPackageManagerConfiguredAnnotation>(out var configured));
        Assert.Equal(packageManager, configured.PackageManager);
        Assert.False(turboResource.TryGetLastAnnotation<JavaScriptPackageManagerAnnotation>(out _));

        // App should use "turbo" directly, not wrapped via package manager
        var turboApp = Assert.Single(appModel.Resources.OfType<TurborepoAppResource>());
        Assert.Equal("turbo", turboApp.Command);
        var args = await turboApp.GetArgumentListAsync();
        Assert.Collection(args,
            arg => Assert.Equal("run", arg),
            arg => Assert.Equal("dev", arg),
            arg => Assert.Equal("--filter", arg),
            arg => Assert.Equal("test-app", arg));
    }

    [Fact]
    public void Turborepo_WithPackageManagerLaunch_CalledTwiceWithDifferent_Throws()
    {
        var builder = DistributedApplication.CreateBuilder();

        var turbo = builder.AddTurborepoApp("turbo-conflict")
            .WithPnpm()
            .WithPackageManagerLaunch();

        var ex = Assert.Throws<InvalidOperationException>(() => turbo.WithPackageManagerLaunch("npm"));
        Assert.Contains("already configured to run with the 'pnpm' package manager", ex.Message);
    }

    [Fact]
    public void Turborepo_WithPackageManagerLaunch_ThrowsWhenNotConfigured()
    {
        var builder = DistributedApplication.CreateBuilder();

        var ex = Assert.Throws<InvalidOperationException>(() =>
            builder.AddTurborepoApp("turbo-no-config").WithPackageManagerLaunch());

        Assert.Contains("not configured with a package manager", ex.Message);
    }

    [Fact]
    public void Turborepo_WithPackageManagerLaunch_ExplicitOverride_UsesExplicitValue()
    {
        var builder = DistributedApplication.CreateBuilder();

        // Configure with npm but explicitly run with yarn
        var turbo = builder.AddTurborepoApp("turbo-override")
            .WithNpm()
            .WithPackageManagerLaunch("yarn");

        using var app = builder.Build();
        var appModel = app.Services.GetRequiredService<DistributedApplicationModel>();

        var turboResource = Assert.Single(appModel.Resources.OfType<TurborepoResource>());

        // Should have configured annotation for npm
        Assert.True(turboResource.TryGetLastAnnotation<JavaScriptPackageManagerConfiguredAnnotation>(out var configured));
        Assert.Equal("npm", configured.PackageManager);

        // But execution annotation should be yarn (explicit override)
        Assert.True(turboResource.TryGetLastAnnotation<JavaScriptPackageManagerAnnotation>(out var execution));
        Assert.Equal("yarn", execution.ExecutableName);
    }

    [Fact]
    public void Turborepo_WithPackageManagerLaunch_CalledTwiceWithSame_DoesNotThrow()
    {
        var builder = DistributedApplication.CreateBuilder();

        var turbo = builder.AddTurborepoApp("turbo-idempotent")
            .WithYarn()
            .WithPackageManagerLaunch()
            .WithPackageManagerLaunch(); // Second call with same inferred value

        using var app = builder.Build();
        var appModel = app.Services.GetRequiredService<DistributedApplicationModel>();

        var turboResource = Assert.Single(appModel.Resources.OfType<TurborepoResource>());
        Assert.True(turboResource.TryGetLastAnnotation<JavaScriptPackageManagerAnnotation>(out var execution));
        Assert.Equal("yarn", execution.ExecutableName);
    }

    [Theory]
    [InlineData("npm")]
    [InlineData("yarn")]
    [InlineData("pnpm")]
    public async Task Turborepo_MultipleApps_AllInheritExecutionAnnotation(string packageManager)
    {
        var builder = DistributedApplication.CreateBuilder();

        var turbo = builder.AddTurborepoApp("turbo-multi");
        turbo = packageManager switch
        {
            "npm" => turbo.WithNpm(),
            "yarn" => turbo.WithYarn(),
            "pnpm" => turbo.WithPnpm(),
            _ => throw new ArgumentOutOfRangeException(nameof(packageManager))
        };
        turbo = turbo.WithPackageManagerLaunch();

        turbo.AddApp("app1");
        turbo.AddApp("app2");
        turbo.AddApp("app3");

        using var app = builder.Build();
        var appModel = app.Services.GetRequiredService<DistributedApplicationModel>();

        var apps = appModel.Resources.OfType<TurborepoAppResource>().ToList();
        Assert.Equal(3, apps.Count);

        foreach (var turboApp in apps)
        {
            var launcherName = packageManager switch
            {
                "npm" => "npx",
                "yarn" => "yarn",
                "pnpm" => "pnpx",
                _ => throw new ArgumentOutOfRangeException(nameof(packageManager))
            };

            Assert.Equal(launcherName, turboApp.Command);
            var args = await turboApp.GetArgumentListAsync();
            Assert.Collection(args,
                arg => Assert.Equal("turbo", arg),
                arg => Assert.Equal("run", arg),
                arg => Assert.Equal("dev", arg),
                arg => Assert.Equal("--filter", arg),
                arg => Assert.Equal(turboApp.Name, arg));
        }
    }

    [Fact]
    public void Turborepo_WithNpm_WithoutInstallFlag_DoesNotCreateInstallerResource()
    {
        var builder = DistributedApplication.CreateBuilder();

        var turbo = builder.AddTurborepoApp("turbo-no-installer")
            .WithNpm(); // install defaults to false

        using var app = builder.Build();
        var appModel = app.Services.GetRequiredService<DistributedApplicationModel>();

        // Should have configured annotation
        var turboResource = Assert.Single(appModel.Resources.OfType<TurborepoResource>());
        Assert.True(turboResource.TryGetLastAnnotation<JavaScriptPackageManagerConfiguredAnnotation>(out _));

        // But no installer annotation or resource
        Assert.False(turboResource.TryGetLastAnnotation<JavaScriptPackageInstallerAnnotation>(out _));
        Assert.Empty(appModel.Resources.OfType<JavaScriptInstallerResource>());
    }

    [Fact]
    public void Turborepo_ConfiguredAnnotation_AddedEvenWithInstallFalse()
    {
        var builder = DistributedApplication.CreateBuilder();

        var turbo = builder.AddTurborepoApp("turbo-configured")
            .WithPnpm(install: false);

        using var app = builder.Build();
        var appModel = app.Services.GetRequiredService<DistributedApplicationModel>();

        var turboResource = Assert.Single(appModel.Resources.OfType<TurborepoResource>());

        // Configured annotation should always be present
        Assert.True(turboResource.TryGetLastAnnotation<JavaScriptPackageManagerConfiguredAnnotation>(out var configured));
        Assert.Equal("pnpm", configured.PackageManager);

        // But no installer
        Assert.False(turboResource.TryGetLastAnnotation<JavaScriptPackageInstallerAnnotation>(out _));
    }

    [Fact]
    public void Turborepo_WithInstallTrue_AppsWaitForInstaller()
    {
        var builder = DistributedApplication.CreateBuilder();

        var turbo = builder.AddTurborepoApp("turbo-with-install")
            .WithYarn(install: true)
            .WithPackageManagerLaunch();

        turbo.AddApp("app1");

        using var app = builder.Build();
        var appModel = app.Services.GetRequiredService<DistributedApplicationModel>();

        // Verify installer resource exists
        var turboResource = Assert.Single(appModel.Resources.OfType<TurborepoResource>());
        Assert.True(turboResource.TryGetLastAnnotation<JavaScriptPackageInstallerAnnotation>(out var installerAnnotation));

        var installer = installerAnnotation.Resource;
        Assert.NotNull(installer);
        Assert.Equal("turbo-with-install-installer", installer.Name);

        // Verify app has WaitFor relationship
        var turboApp = Assert.Single(appModel.Resources.OfType<TurborepoAppResource>());
        Assert.True(turboApp.TryGetAnnotationsOfType<WaitAnnotation>(out var waitAnnotations));
        var waitAnnotation = Assert.Single(waitAnnotations);
        Assert.Equal(installer, waitAnnotation.Resource);
    }
}
