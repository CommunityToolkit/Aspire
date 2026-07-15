using CommunityToolkit.Aspire.Testing;

namespace CommunityToolkit.Aspire.Hosting.SeaweedFS.Tests;

[RequiresDocker]
public class TypeScriptAppHostTests
{
    [Fact]
    public async Task TypeScriptAppHostCompilesAndStarts()
    {
        
        Environment.SetEnvironmentVariable("ASPIRE_ALLOW_UNSECURED_TRANSPORT", "true");

        try
        {
            // This will automatically generate the Node SDK, run the apphost.mts, 
            // and wait for the resources to report a healthy state.
            await TypeScriptAppHostTest.Run(
                appHostProject: "SeaweedFS.AppHost.TypeScript",
                packageName: "CommunityToolkit.Aspire.Hosting.SeaweedFS",
                exampleName: "seaweedfs",
                waitForResources: ["typescript-seaweedfs-s3", "typescript-seaweedfs-filer"],
                cancellationToken: TestContext.Current.CancellationToken);
        }
        finally
        {
            Environment.SetEnvironmentVariable("ASPIRE_ALLOW_UNSECURED_TRANSPORT", null);
        }
    }
}