using Aspire.Hosting;
using Aspire.Hosting.JavaScript;
using CommunityToolkit.Aspire.Testing;

namespace CommunityToolkit.Aspire.Hosting.JavaScript.Extensions.Tests;

public class VitePlusResourceCreationTests
{
    [Fact]
    public void AddVitePlusApp_CreatesCorrectResourceType()
    {
        var builder = DistributedApplication.CreateBuilder();

        var vp = builder.AddVitePlusApp("test-vp", workingDirectory: "../test-vp");

        using var app = builder.Build();

        var appModel = app.Services.GetRequiredService<DistributedApplicationModel>();
        var vitePlusResource = Assert.Single(appModel.Resources.OfType<VitePlusResource>());

        Assert.Equal("test-vp", vitePlusResource.Name);
        Assert.NotEmpty(vitePlusResource.WorkingDirectory);
    }

    [Fact]
    public void VitePlusResource_DefaultWorkingDirectory()
    {
        var builder = DistributedApplication.CreateBuilder();

        var vp = builder.AddVitePlusApp("my-vp");

        using var app = builder.Build();

        var appModel = app.Services.GetRequiredService<DistributedApplicationModel>();
        var vitePlusResource = Assert.Single(appModel.Resources.OfType<VitePlusResource>());

        Assert.Contains("my-vp", vitePlusResource.WorkingDirectory);
    }

    [Fact]
    public void VitePlusResource_IsRunningState()
    {
        var builder = DistributedApplication.CreateBuilder();

        var vp = builder.AddVitePlusApp("vp-running");

        using var app = builder.Build();

        var appModel = app.Services.GetRequiredService<DistributedApplicationModel>();
        var vpResource = Assert.Single(appModel.Resources.OfType<VitePlusResource>());

        Assert.True(vpResource.TryGetAnnotationsOfType<ResourceSnapshotAnnotation>(out var annotations));
        var annotation = Assert.Single(annotations);

        Assert.Equal(KnownResourceStates.Running, annotation.InitialSnapshot.State?.Text);
    }

    [Fact]
    public async Task VitePlus_AddApp_DefaultDirectoryIsAppsName()
    {
        var builder = DistributedApplication.CreateBuilder();

        var vp = builder.AddVitePlusApp("vp-default-dir");
        vp.AddApp("app1");

        using var app = builder.Build();

        var appModel = app.Services.GetRequiredService<DistributedApplicationModel>();
        var vpApp = Assert.Single(appModel.Resources.OfType<VitePlusAppResource>());

        Assert.Equal(Path.Combine("apps", "app1"), vpApp.Directory);
        Assert.Equal("vp", vpApp.Command);
        var args = await vpApp.GetArgumentListAsync();
        Assert.Collection(args,
            arg => Assert.Equal("-C", arg),
            arg => Assert.Equal(Path.Combine("apps", "app1"), arg),
            arg => Assert.Equal("dev", arg));
    }

    [Fact]
    public async Task VitePlus_AddApp_ExplicitDirectoryIsPassedThrough()
    {
        var builder = DistributedApplication.CreateBuilder();

        var vp = builder.AddVitePlusApp("vp-explicit-dir");
        vp.AddApp("docs", directory: "packages/docs");

        using var app = builder.Build();

        var appModel = app.Services.GetRequiredService<DistributedApplicationModel>();
        var vpApp = Assert.Single(appModel.Resources.OfType<VitePlusAppResource>());

        Assert.Equal("packages/docs", vpApp.Directory);
        Assert.Equal("vp", vpApp.Command);
        var args = await vpApp.GetArgumentListAsync();
        Assert.Collection(args,
            arg => Assert.Equal("-C", arg),
            arg => Assert.Equal("packages/docs", arg),
            arg => Assert.Equal("dev", arg));
    }

    [Theory]
    [InlineData("npm")]
    [InlineData("yarn")]
    [InlineData("pnpm")]
    [InlineData("bun")]
    public async Task VitePlus_WithPackageManagerLaunch_InfersFromInstallerWhenNotProvided(string packageManager)
    {
        var builder = DistributedApplication.CreateBuilder();

        var vp = builder.AddVitePlusApp("vp-default");

        vp = (packageManager switch
        {
            "npm" => vp.WithNpm(),
            "yarn" => vp.WithYarn(),
            "pnpm" => vp.WithPnpm(),
            "bun" => vp.WithBun(),
            _ => throw new ArgumentOutOfRangeException(nameof(packageManager), $"Unsupported package manager: {packageManager}"),
        }).WithPackageManagerLaunch();

        // Add an app to the Vite+ workspace to verify app-level command/args
        var app1 = vp.AddApp("app1");

        using var app = builder.Build();

        var appModel = app.Services.GetRequiredService<DistributedApplicationModel>();
        var vpResource = Assert.Single(appModel.Resources.OfType<VitePlusResource>());

        Assert.True(vpResource.TryGetLastAnnotation<JavaScriptPackageManagerAnnotation>(out var pm));
        Assert.Equal(packageManager, pm.ExecutableName);

        // Verify Vite+ app command and args
        var vpApp = Assert.Single(appModel.Resources.OfType<VitePlusAppResource>());
        // For the inferred package manager, AddApp uses the corresponding command
        Assert.Equal(packageManager switch
        {
            "npm" => "npx",
            "yarn" => "yarn",
            "pnpm" => "pnpx",
            "bun" => "bunx",
            _ => packageManager
        }, vpApp.Command);
        var vpArgs = await vpApp.GetArgumentListAsync();
        Assert.Collection(vpArgs,
            arg => Assert.Equal("vp", arg),
            arg => Assert.Equal("-C", arg),
            arg => Assert.Equal(Path.Combine("apps", "app1"), arg),
            arg => Assert.Equal("dev", arg));
    }

    [Fact]
    public async Task VitePlus_WithPackageManagerLaunch_WithPnpmAndYarn()
    {
        var builder = DistributedApplication.CreateBuilder();

        var vpPnpm = builder.AddVitePlusApp("vp-pnpm").WithPnpm().WithPackageManagerLaunch();
        var vpYarn = builder.AddVitePlusApp("vp-yarn").WithYarn().WithPackageManagerLaunch();

        vpPnpm.AddApp("app1-pnpm");
        vpYarn.AddApp("app1-yarn");

        using var app = builder.Build();

        var appModel = app.Services.GetRequiredService<DistributedApplicationModel>();

        var vpPnpmResource = appModel.Resources.OfType<VitePlusResource>().Single(r => r.Name == "vp-pnpm");
        Assert.True(vpPnpmResource.TryGetLastAnnotation<JavaScriptPackageManagerAnnotation>(out var vpmPnpm));
        Assert.Equal("pnpm", vpmPnpm.ExecutableName);

        var vpPnpmApp = appModel.Resources.OfType<VitePlusAppResource>().Single(r => r.Name == "app1-pnpm");
        Assert.Equal("pnpx", vpPnpmApp.Command);
        var vppnpmArgs = await vpPnpmApp.GetArgumentListAsync();
        Assert.Collection(vppnpmArgs,
            arg => Assert.Equal("vp", arg),
            arg => Assert.Equal("-C", arg),
            arg => Assert.Equal(Path.Combine("apps", "app1-pnpm"), arg),
            arg => Assert.Equal("dev", arg));

        var vpYarnResource = appModel.Resources.OfType<VitePlusResource>().Single(r => r.Name == "vp-yarn");
        Assert.True(vpYarnResource.TryGetLastAnnotation<JavaScriptPackageManagerAnnotation>(out var vpmYarn));
        Assert.Equal("yarn", vpmYarn.ExecutableName);

        var vpYarnApp = appModel.Resources.OfType<VitePlusAppResource>().Single(r => r.Name == "app1-yarn");
        Assert.Equal("yarn", vpYarnApp.Command);
        var vpyarnArgs = await vpYarnApp.GetArgumentListAsync();
        Assert.Collection(vpyarnArgs,
            arg => Assert.Equal("vp", arg),
            arg => Assert.Equal("-C", arg),
            arg => Assert.Equal(Path.Combine("apps", "app1-yarn"), arg),
            arg => Assert.Equal("dev", arg));
    }

    [Fact]
    public async Task VitePlus_NoWithPackageManagerLaunch_Defaults_AppCommandsIncludeNpxOrDefault()
    {
        var builder = DistributedApplication.CreateBuilder();

        // Vite+ without WithPackageManagerLaunch or installer
        var vp = builder.AddVitePlusApp("vp-default");
        vp.AddApp("app-vp-default");

        using var app = builder.Build();

        var appModel = app.Services.GetRequiredService<DistributedApplicationModel>();

        var vpApp = appModel.Resources.OfType<VitePlusAppResource>().Single(r => r.Name == "app-vp-default");
        // Command should be 'vp' (default) and args should target the app directory via -C
        Assert.Equal("vp", vpApp.Command);
        var vpArgs = await vpApp.GetArgumentListAsync();
        Assert.Collection(vpArgs,
            arg => Assert.Equal("-C", arg),
            arg => Assert.Equal(Path.Combine("apps", "app-vp-default"), arg),
            arg => Assert.Equal("dev", arg));
    }

    [Theory]
    [InlineData("npm")]
    [InlineData("yarn")]
    [InlineData("pnpm")]
    [InlineData("bun")]
    public async Task VitePlus_WithPackageManager_WithoutRunWith_DoesNotAffectAppExecution(string packageManager)
    {
        var builder = DistributedApplication.CreateBuilder();

        var vpBuilder = builder.AddVitePlusApp("vp-configured-only");
        vpBuilder = packageManager switch
        {
            "npm" => vpBuilder.WithNpm(),
            "yarn" => vpBuilder.WithYarn(),
            "pnpm" => vpBuilder.WithPnpm(),
            "bun" => vpBuilder.WithBun(),
            _ => throw new ArgumentOutOfRangeException(nameof(packageManager))
        };

        vpBuilder.AddApp("test-app");

        using var app = builder.Build();
        var appModel = app.Services.GetRequiredService<DistributedApplicationModel>();

        // Workspace should have configured annotation but not execution annotation
        var vpResource = Assert.Single(appModel.Resources.OfType<VitePlusResource>());
        Assert.True(vpResource.TryGetLastAnnotation<JavaScriptPackageManagerConfiguredAnnotation>(out var configured));
        Assert.Equal(packageManager, configured.PackageManager);
        Assert.False(vpResource.TryGetLastAnnotation<JavaScriptPackageManagerAnnotation>(out _));

        // App should use "vp" directly, not wrapped via package manager
        var vpApp = Assert.Single(appModel.Resources.OfType<VitePlusAppResource>());
        Assert.Equal("vp", vpApp.Command);
        var args = await vpApp.GetArgumentListAsync();
        Assert.Collection(args,
            arg => Assert.Equal("-C", arg),
            arg => Assert.Equal(Path.Combine("apps", "test-app"), arg),
            arg => Assert.Equal("dev", arg));
    }

    [Fact]
    public void VitePlus_WithPackageManagerLaunch_CalledTwiceWithDifferent_Throws()
    {
        var builder = DistributedApplication.CreateBuilder();

        var vp = builder.AddVitePlusApp("vp-conflict")
            .WithPnpm()
            .WithPackageManagerLaunch();

        var ex = Assert.Throws<InvalidOperationException>(() => vp.WithPackageManagerLaunch("npm"));
        Assert.Contains("already configured to run with the 'pnpm' package manager", ex.Message);
    }

    [Fact]
    public void VitePlus_WithPackageManagerLaunch_ThrowsWhenNotConfigured()
    {
        var builder = DistributedApplication.CreateBuilder();

        var ex = Assert.Throws<InvalidOperationException>(() =>
            builder.AddVitePlusApp("vp-no-config").WithPackageManagerLaunch());

        Assert.Contains("not configured with a package manager", ex.Message);
    }

    [Fact]
    public void VitePlus_WithPackageManagerLaunch_ExplicitOverride_UsesExplicitValue()
    {
        var builder = DistributedApplication.CreateBuilder();

        // Configure with npm but explicitly run with yarn
        var vp = builder.AddVitePlusApp("vp-override")
            .WithNpm()
            .WithPackageManagerLaunch("yarn");

        using var app = builder.Build();
        var appModel = app.Services.GetRequiredService<DistributedApplicationModel>();

        var vpResource = Assert.Single(appModel.Resources.OfType<VitePlusResource>());

        // Should have configured annotation for npm
        Assert.True(vpResource.TryGetLastAnnotation<JavaScriptPackageManagerConfiguredAnnotation>(out var configured));
        Assert.Equal("npm", configured.PackageManager);

        // But execution annotation should be yarn (explicit override)
        Assert.True(vpResource.TryGetLastAnnotation<JavaScriptPackageManagerAnnotation>(out var execution));
        Assert.Equal("yarn", execution.ExecutableName);
    }

    [Fact]
    public void VitePlus_WithPackageManagerLaunch_CalledTwiceWithSame_DoesNotThrow()
    {
        var builder = DistributedApplication.CreateBuilder();

        var vp = builder.AddVitePlusApp("vp-idempotent")
            .WithYarn()
            .WithPackageManagerLaunch()
            .WithPackageManagerLaunch(); // Second call with same inferred value

        using var app = builder.Build();
        var appModel = app.Services.GetRequiredService<DistributedApplicationModel>();

        var vpResource = Assert.Single(appModel.Resources.OfType<VitePlusResource>());
        Assert.True(vpResource.TryGetLastAnnotation<JavaScriptPackageManagerAnnotation>(out var execution));
        Assert.Equal("yarn", execution.ExecutableName);
    }

    [Theory]
    [InlineData("npm")]
    [InlineData("yarn")]
    [InlineData("pnpm")]
    [InlineData("bun")]
    public async Task VitePlus_MultipleApps_AllInheritExecutionAnnotation(string packageManager)
    {
        var builder = DistributedApplication.CreateBuilder();

        var vp = builder.AddVitePlusApp("vp-multi");
        vp = packageManager switch
        {
            "npm" => vp.WithNpm(),
            "yarn" => vp.WithYarn(),
            "pnpm" => vp.WithPnpm(),
            "bun" => vp.WithBun(),
            _ => throw new ArgumentOutOfRangeException(nameof(packageManager))
        };
        vp = vp.WithPackageManagerLaunch();

        vp.AddApp("app1");
        vp.AddApp("app2");
        vp.AddApp("app3");

        using var app = builder.Build();
        var appModel = app.Services.GetRequiredService<DistributedApplicationModel>();

        var apps = appModel.Resources.OfType<VitePlusAppResource>().ToList();
        Assert.Equal(3, apps.Count);

        foreach (var vpApp in apps)
        {
            var launcherName = packageManager switch
            {
                "npm" => "npx",
                "yarn" => "yarn",
                "pnpm" => "pnpx",
                "bun" => "bunx",
                _ => throw new ArgumentOutOfRangeException(nameof(packageManager))
            };

            Assert.Equal(launcherName, vpApp.Command);
            var args = await vpApp.GetArgumentListAsync();
            Assert.Collection(args,
                arg => Assert.Equal("vp", arg),
                arg => Assert.Equal("-C", arg),
                arg => Assert.Equal(Path.Combine("apps", vpApp.Name), arg),
                arg => Assert.Equal("dev", arg));
        }
    }

    [Fact]
    public void VitePlus_WithNpm_WithoutInstallFlag_DoesNotCreateInstallerResource()
    {
        var builder = DistributedApplication.CreateBuilder();

        var vp = builder.AddVitePlusApp("vp-no-installer")
            .WithNpm(); // install defaults to false

        using var app = builder.Build();
        var appModel = app.Services.GetRequiredService<DistributedApplicationModel>();

        // Should have configured annotation
        var vpResource = Assert.Single(appModel.Resources.OfType<VitePlusResource>());
        Assert.True(vpResource.TryGetLastAnnotation<JavaScriptPackageManagerConfiguredAnnotation>(out _));

        // But no installer annotation or resource
        Assert.False(vpResource.TryGetLastAnnotation<JavaScriptPackageInstallerAnnotation>(out _));
        Assert.Empty(appModel.Resources.OfType<JavaScriptInstallerResource>());
    }

    [Fact]
    public void VitePlus_ConfiguredAnnotation_AddedEvenWithInstallFalse()
    {
        var builder = DistributedApplication.CreateBuilder();

        var vp = builder.AddVitePlusApp("vp-configured")
            .WithPnpm(install: false);

        using var app = builder.Build();
        var appModel = app.Services.GetRequiredService<DistributedApplicationModel>();

        var vpResource = Assert.Single(appModel.Resources.OfType<VitePlusResource>());

        // Configured annotation should always be present
        Assert.True(vpResource.TryGetLastAnnotation<JavaScriptPackageManagerConfiguredAnnotation>(out var configured));
        Assert.Equal("pnpm", configured.PackageManager);

        // But no installer
        Assert.False(vpResource.TryGetLastAnnotation<JavaScriptPackageInstallerAnnotation>(out _));
    }

    [Fact]
    public void VitePlus_WithInstallTrue_AppsWaitForInstaller()
    {
        var builder = DistributedApplication.CreateBuilder();

        var vp = builder.AddVitePlusApp("vp-with-install")
            .WithYarn(install: true)
            .WithPackageManagerLaunch();

        vp.AddApp("app1");

        using var app = builder.Build();
        var appModel = app.Services.GetRequiredService<DistributedApplicationModel>();

        // Verify installer resource exists
        var vpResource = Assert.Single(appModel.Resources.OfType<VitePlusResource>());
        Assert.True(vpResource.TryGetLastAnnotation<JavaScriptPackageInstallerAnnotation>(out var installerAnnotation));

        var installer = installerAnnotation.Resource;
        Assert.NotNull(installer);
        Assert.Equal("vp-with-install-installer", installer.Name);

        // Verify app has WaitFor relationship
        var vpApp = Assert.Single(appModel.Resources.OfType<VitePlusAppResource>());
        Assert.True(vpApp.TryGetAnnotationsOfType<WaitAnnotation>(out var waitAnnotations));
        var waitAnnotation = Assert.Single(waitAnnotations);
        Assert.Equal(installer, waitAnnotation.Resource);
    }
}
