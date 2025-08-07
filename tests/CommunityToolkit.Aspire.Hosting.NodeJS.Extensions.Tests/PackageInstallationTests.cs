using Aspire.Hosting;

namespace CommunityToolkit.Aspire.Hosting.NodeJS.Extensions.Tests;

public class PackageInstallationTests
{
    /// <summary>
    /// This test validates that the WithNpmPackageInstallation method creates
    /// installer resources with proper arguments and relationships.
    /// </summary>
    [Fact]
    public async Task WithNpmPackageInstallation_CanBeConfiguredWithInstallAndCIOptions()
    {
        var builder = DistributedApplication.CreateBuilder();

        var nodeApp = builder.AddNpmApp("test-app", "./test-app");
        var nodeApp2 = builder.AddNpmApp("test-app-ci", "./test-app-ci");

        // Test that both configurations can be set up without errors
        nodeApp.WithNpmPackageInstallation(useCI: false); // Uses npm install
        nodeApp2.WithNpmPackageInstallation(useCI: true);  // Uses npm ci

        using var app = builder.Build();

        var appModel = app.Services.GetRequiredService<DistributedApplicationModel>();
        var nodeResources = appModel.Resources.OfType<NodeAppResource>().ToList();
        var installerResources = appModel.Resources.OfType<NpmInstallerResource>().ToList();

        Assert.Equal(2, nodeResources.Count);
        Assert.Equal(2, installerResources.Count);
        Assert.All(nodeResources, resource => Assert.Equal("npm", resource.Command));

        // Verify install vs ci commands
        var installResource = installerResources.Single(r => r.Name == "test-app-npm-install");
        var ciResource = installerResources.Single(r => r.Name == "test-app-ci-npm-install");

        Assert.Equal("npm", installResource.Command);
        var args = await installResource.GetArgumentValuesAsync();
        Assert.Single(args);
        Assert.Equal("install", args[0]);

        Assert.Equal("npm", ciResource.Command);
        args = await ciResource.GetArgumentValuesAsync();
        Assert.Single(args);
        Assert.Equal("ci", args[0]);
    }

    [Fact]
    public void WithNpmPackageInstallation_ExcludedFromPublishMode()
    {
        var builder = DistributedApplication.CreateBuilder(["Publishing:Publisher=manifest", "Publishing:OutputPath=./publish"]);

        var nodeApp = builder.AddNpmApp("test-app", "./test-app");
        nodeApp.WithNpmPackageInstallation(useCI: false);

        using var app = builder.Build();

        var appModel = app.Services.GetRequiredService<DistributedApplicationModel>();

        // Verify the NodeApp resource exists
        var nodeResource = Assert.Single(appModel.Resources.OfType<NodeAppResource>());
        Assert.Equal("npm", nodeResource.Command);

        // Verify NO installer resource was created in publish mode
        var installerResources = appModel.Resources.OfType<NpmInstallerResource>().ToList();
        Assert.Empty(installerResources);

        // Verify no wait annotations were added
        Assert.False(nodeResource.TryGetAnnotationsOfType<WaitAnnotation>(out _));
    }

    [Fact]
    public async Task WithNpmPackageInstallation_CanAcceptAdditionalArgs()
    {
        var builder = DistributedApplication.CreateBuilder();

        var nodeApp = builder.AddNpmApp("test-app", "./test-app");
        var nodeAppWithArgs = builder.AddNpmApp("test-app-args", "./test-app-args");

        // Test npm install with additional args
        nodeApp.WithNpmPackageInstallation(useCI: false, configureInstaller: installerBuilder =>
        {
            installerBuilder.WithArgs("--legacy-peer-deps");
        });
        nodeAppWithArgs.WithNpmPackageInstallation(useCI: true, configureInstaller: installerBuilder =>
        {
            installerBuilder.WithArgs("--verbose", "--no-optional");
        });

        using var app = builder.Build();

        var appModel = app.Services.GetRequiredService<DistributedApplicationModel>();
        var installerResources = appModel.Resources.OfType<NpmInstallerResource>().ToList();

        Assert.Equal(2, installerResources.Count);

        var installResource = installerResources.Single(r => r.Name == "test-app-npm-install");
        var ciResource = installerResources.Single(r => r.Name == "test-app-args-npm-install");

        // Verify install command with additional args
        var installArgs = await installResource.GetArgumentValuesAsync();
        Assert.Collection(
            installArgs,
            arg => Assert.Equal("install", arg),
            arg => Assert.Equal("--legacy-peer-deps", arg)
        );

        // Verify ci command with additional args
        var ciArgs = await ciResource.GetArgumentValuesAsync();
        Assert.Collection(
            ciArgs,
            arg => Assert.Equal("ci", arg),
            arg => Assert.Equal("--verbose", arg),
            arg => Assert.Equal("--no-optional", arg)
        );
    }

    [Fact]
    public async Task WithYarnPackageInstallation_CanAcceptAdditionalArgs()
    {
        var builder = DistributedApplication.CreateBuilder();

        var nodeApp = builder.AddYarnApp("test-yarn-app", "./test-yarn-app");
        nodeApp.WithYarnPackageInstallation(configureInstaller: installerBuilder =>
        {
            installerBuilder.WithArgs("--frozen-lockfile", "--verbose");
        });

        using var app = builder.Build();

        var appModel = app.Services.GetRequiredService<DistributedApplicationModel>();
        var installerResources = appModel.Resources.OfType<YarnInstallerResource>().ToList();

        var installerResource = Assert.Single(installerResources);
        Assert.Equal("test-yarn-app-yarn-install", installerResource.Name);

        var args = await installerResource.GetArgumentValuesAsync();
        Assert.Collection(
            args,
            arg => Assert.Equal("install", arg),
            arg => Assert.Equal("--frozen-lockfile", arg),
            arg => Assert.Equal("--verbose", arg)
        );
    }

    [Fact]
    public async Task WithPnpmPackageInstallation_CanAcceptAdditionalArgs()
    {
        var builder = DistributedApplication.CreateBuilder();

        var nodeApp = builder.AddPnpmApp("test-pnpm-app", "./test-pnpm-app");
        nodeApp.WithPnpmPackageInstallation(configureInstaller: installerBuilder =>
        {
            installerBuilder.WithArgs("--frozen-lockfile");
        });

        using var app = builder.Build();

        var appModel = app.Services.GetRequiredService<DistributedApplicationModel>();
        var installerResources = appModel.Resources.OfType<PnpmInstallerResource>().ToList();

        var installerResource = Assert.Single(installerResources);
        Assert.Equal("test-pnpm-app-pnpm-install", installerResource.Name);

        var args = await installerResource.GetArgumentValuesAsync();
        Assert.Collection(
            args,
            arg => Assert.Equal("install", arg),
            arg => Assert.Equal("--frozen-lockfile", arg)
        );
    }

    [Fact]
    public async Task AddNxApp_CreatesNxResourceAndAppsWithSharedInstaller()
    {
        var builder = DistributedApplication.CreateBuilder();

        var nx = builder.AddNxApp("nx", workingDirectory: "../frontend")
            .WithNpmPackageInstaller();

        var app1 = nx.AddApp("app1");
        var app2 = nx.AddApp("app2", appName: "app-2");

        using var app = builder.Build();

        var appModel = app.Services.GetRequiredService<DistributedApplicationModel>();

        // Verify NxResource exists
        var nxResource = Assert.Single(appModel.Resources.OfType<NxResource>());
        Assert.Equal("nx", nxResource.Name);

        // Verify NxAppResources exist
        var nxAppResources = appModel.Resources.OfType<NxAppResource>().ToList();
        Assert.Equal(2, nxAppResources.Count);

        var app1Resource = nxAppResources.Single(r => r.Name == "app1");
        var app2Resource = nxAppResources.Single(r => r.Name == "app2");

        Assert.Equal("app1", app1Resource.AppName);
        Assert.Equal("app-2", app2Resource.AppName);
        Assert.Equal("nx", app1Resource.Command);
        Assert.Equal("nx", app2Resource.Command);

        // Verify arguments
        var app1Args = await app1Resource.GetArgumentValuesAsync();
        Assert.Collection(app1Args,
            arg => Assert.Equal("serve", arg),
            arg => Assert.Equal("app1", arg));

        var app2Args = await app2Resource.GetArgumentValuesAsync();
        Assert.Collection(app2Args,
            arg => Assert.Equal("serve", arg),
            arg => Assert.Equal("app-2", arg));

        // Verify only one installer was created
        var installerResources = appModel.Resources.OfType<NpmInstallerResource>().ToList();
        var installerResource = Assert.Single(installerResources);
        Assert.Equal("nx-npm-install", installerResource.Name);

        // Verify installer arguments
        var installerArgs = await installerResource.GetArgumentValuesAsync();
        Assert.Single(installerArgs);
        Assert.Equal("install", installerArgs[0]);
    }

    [Fact]
    public async Task AddTurborepoApp_CreatesResourcesWithSharedInstaller()
    {
        var builder = DistributedApplication.CreateBuilder();

        var turbo = builder.AddTurborepoApp("turbo", workingDirectory: "../frontend")
            .WithYarnPackageInstaller();

        var app1 = turbo.AddApp("app1");
        var app2 = turbo.AddApp("app2", filter: "custom-filter");

        using var app = builder.Build();

        var appModel = app.Services.GetRequiredService<DistributedApplicationModel>();

        // Verify TurborepoResource exists
        var turborepoResource = Assert.Single(appModel.Resources.OfType<TurborepoResource>());
        Assert.Equal("turbo", turborepoResource.Name);

        // Verify TurborepoAppResources exist
        var turborepoAppResources = appModel.Resources.OfType<TurborepoAppResource>().ToList();
        Assert.Equal(2, turborepoAppResources.Count);

        var app1Resource = turborepoAppResources.Single(r => r.Name == "app1");
        var app2Resource = turborepoAppResources.Single(r => r.Name == "app2");

        Assert.Equal("app1", app1Resource.Filter);
        Assert.Equal("custom-filter", app2Resource.Filter);
        Assert.Equal("turbo", app1Resource.Command);
        Assert.Equal("turbo", app2Resource.Command);

        // Verify arguments
        var app1Args = await app1Resource.GetArgumentValuesAsync();
        Assert.Collection(app1Args,
            arg => Assert.Equal("run", arg),
            arg => Assert.Equal("dev", arg),
            arg => Assert.Equal("--filter", arg),
            arg => Assert.Equal("app1", arg));

        var app2Args = await app2Resource.GetArgumentValuesAsync();
        Assert.Collection(app2Args,
            arg => Assert.Equal("run", arg),
            arg => Assert.Equal("dev", arg),
            arg => Assert.Equal("--filter", arg),
            arg => Assert.Equal("custom-filter", arg));

        // Verify only one installer was created
        var installerResources = appModel.Resources.OfType<YarnInstallerResource>().ToList();
        var installerResource = Assert.Single(installerResources);
        Assert.Equal("turbo-yarn-install", installerResource.Name);
    }

    [Fact]
    public void MonorepoPackageInstallersExcludedFromPublishMode()
    {
        var builder = DistributedApplication.CreateBuilder(["Publishing:Publisher=manifest", "Publishing:OutputPath=./publish"]);

        var nx = builder.AddNxApp("nx", workingDirectory: "../frontend")
            .WithNpmPackageInstaller();
        var turbo = builder.AddTurborepoApp("turbo", workingDirectory: "../frontend")
            .WithPnpmPackageInstaller();

        nx.AddApp("app1");
        turbo.AddApp("app2");

        using var app = builder.Build();

        var appModel = app.Services.GetRequiredService<DistributedApplicationModel>();

        // Verify no installer resources were created in publish mode
        var npmInstallerResources = appModel.Resources.OfType<NpmInstallerResource>().ToList();
        var pnpmInstallerResources = appModel.Resources.OfType<PnpmInstallerResource>().ToList();

        Assert.Empty(npmInstallerResources);
        Assert.Empty(pnpmInstallerResources);
    }
}