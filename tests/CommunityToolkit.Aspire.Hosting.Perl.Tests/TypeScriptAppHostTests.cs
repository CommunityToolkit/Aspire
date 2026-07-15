using CommunityToolkit.Aspire.Testing;

namespace CommunityToolkit.Aspire.Hosting.Perl.Tests;

public class TypeScriptAppHostTests
{
    [Fact]
    public async Task TypeScriptAppHostCompilesAndStarts()
    {
        await TypeScriptAppHostTest.Run(
            appHostProject: "CpanmApiIntegration.AppHost.TypeScript",
            packageName: "CommunityToolkit.Aspire.Hosting.Perl",
            exampleName: "perl",
            waitForResources: ["perl-api"],
            waitStatus: "up",
            requiredCommands: ["perl", "cpanm"],
            cancellationToken: TestContext.Current.CancellationToken);
    }
}