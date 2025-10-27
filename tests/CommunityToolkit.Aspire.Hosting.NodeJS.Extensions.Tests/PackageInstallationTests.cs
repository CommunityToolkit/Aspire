using Aspire.Hosting;

namespace CommunityToolkit.Aspire.Hosting.NodeJS.Extensions.Tests;

public class PackageInstallationTests
{
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
    public async Task AddNxApp_CreatesNxResourceAndAppsWithYarnInstaller()
    {
        var builder = DistributedApplication.CreateBuilder();

        var nx = builder.AddNxApp("nx", workingDirectory: "../frontend")
            .WithYarnPackageInstaller();

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
        var installerResources = appModel.Resources.OfType<YarnInstallerResource>().ToList();
        var installerResource = Assert.Single(installerResources);
        Assert.Equal("nx-yarn-install", installerResource.Name);

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
            .WithYarnPackageInstaller();
        var turbo = builder.AddTurborepoApp("turbo", workingDirectory: "../frontend")
            .WithPnpmPackageInstaller();

        nx.AddApp("app1");
        turbo.AddApp("app2");

        using var app = builder.Build();

        var appModel = app.Services.GetRequiredService<DistributedApplicationModel>();

        // Verify no installer resources were created in publish mode
        var yarnInstallerResources = appModel.Resources.OfType<YarnInstallerResource>().ToList();
        var pnpmInstallerResources = appModel.Resources.OfType<PnpmInstallerResource>().ToList();

        Assert.Empty(yarnInstallerResources);
        Assert.Empty(pnpmInstallerResources);
    }

    [Fact]
    public void InstallerResourceAddedAsAnnotation_Turborepo()
    {
        var builder = DistributedApplication.CreateBuilder();

        var turbo = builder.AddTurborepoApp("turbo", workingDirectory: "../frontend")
            .WithYarnPackageInstaller();

        turbo.AddApp("app1");
        turbo.AddApp("app2");

        using var app = builder.Build();

        var appModel = app.Services.GetRequiredService<DistributedApplicationModel>();

        // Verify installer resource was added as annotation
        var installerResources = appModel.Resources.OfType<YarnInstallerResource>().ToList();
        Assert.Single(installerResources);
        Assert.Equal("turbo-yarn-install", installerResources[0].Name);
    }

    [Fact]
    public void InstallerResourceAddedAsAnnotation_Nx()
    {
        var builder = DistributedApplication.CreateBuilder();

        var nx = builder.AddNxApp("nx", workingDirectory: "../frontend")
            .WithPnpmPackageInstaller();

        nx.AddApp("app1");
        nx.AddApp("app2");

        using var app = builder.Build();

        var appModel = app.Services.GetRequiredService<DistributedApplicationModel>();

        // Verify installer resource was added as annotation
        var installerResources = appModel.Resources.OfType<PnpmInstallerResource>().ToList();
        Assert.Single(installerResources);
        Assert.Equal("nx-pnpm-install", installerResources[0].Name);
    }

    [Fact]
    public void InstallerResourceAddedAsAnnotation_Pnpm()
    {
        var builder = DistributedApplication.CreateBuilder();

        var pnpm = builder.AddPnpmApp("pnpm", "../frontend")
            .WithPnpmPackageInstallation();

        using var app = builder.Build();

        var appModel = app.Services.GetRequiredService<DistributedApplicationModel>();

        // Verify installer resource was added as annotation
        var installerResources = appModel.Resources.OfType<PnpmInstallerResource>().ToList();
        Assert.Single(installerResources);
        Assert.Equal("pnpm-pnpm-install", installerResources[0].Name);
    }

    [Fact]
    public void InstallerResourceAddedAsAnnotation_Yarn()
    {
        var builder = DistributedApplication.CreateBuilder();

        var yarn = builder.AddYarnApp("yarn", "../frontend")
            .WithYarnPackageInstallation();

        using var app = builder.Build();

        var appModel = app.Services.GetRequiredService<DistributedApplicationModel>();

        // Verify installer resource was added as annotation
        var installerResources = appModel.Resources.OfType<YarnInstallerResource>().ToList();
        Assert.Single(installerResources);
        Assert.Equal("yarn-yarn-install", installerResources[0].Name);
    }
}
