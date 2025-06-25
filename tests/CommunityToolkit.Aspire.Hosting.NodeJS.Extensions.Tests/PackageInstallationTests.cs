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
}