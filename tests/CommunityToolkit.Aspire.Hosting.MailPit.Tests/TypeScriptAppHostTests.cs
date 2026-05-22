using Aspire.Components.Common.Tests;
using CommunityToolkit.Aspire.Testing;

namespace CommunityToolkit.Aspire.Hosting.MailPit.Tests;

[RequiresDocker]
public class TypeScriptAppHostTests
{
    [Fact]
    public async Task TypeScriptAppHostCompilesAndStarts()
    {
        await TypeScriptAppHostTest.Run(
            appHostProject: "CommunityToolkit.Aspire.Hosting.MailPit.AppHost.TypeScript",
            packageName: "CommunityToolkit.Aspire.Hosting.MailPit",
            exampleName: "mailpit",
            waitForResources: ["mailpit", "mailpit-default"],
            cancellationToken: TestContext.Current.CancellationToken);
    }
}
