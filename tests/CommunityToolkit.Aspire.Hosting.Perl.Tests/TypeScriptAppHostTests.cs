using CommunityToolkit.Aspire.Testing;

namespace CommunityToolkit.Aspire.Hosting.Perl.Tests;

public class TypeScriptAppHostTests
{
    [Fact]
    public async Task TypeScriptAppHostCartonProjectDependencyScenario_FleetingEndpoint_ReturnsExpectedText()
    {
        await TypeScriptAppHostTest.Run(
            appHostProject: "CpanmApiIntegration.AppHost.TypeScript",
            packageName: "CommunityToolkit.Aspire.Hosting.Perl",
            exampleName: "perl/cpanm-api-integration",
            waitForResources: ["perl-api", "perl-driver"],
            requiredCommands: ["perl", "carton"],
            useConfiguredPackages: true,
            httpProbeResource: "perl-api",
            httpProbePath: "/fleeting",
            httpProbeExpectedText: "fragile",
            cancellationToken: TestContext.Current.CancellationToken);
    }
}