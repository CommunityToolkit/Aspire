using CommunityToolkit.Aspire.Testing;

namespace CommunityToolkit.Aspire.Hosting.Minio.Tests;

[RequiresDocker]
public class TypeScriptAppHostTests
{
    [Fact]
    public async Task TypeScriptAppHostCompilesAndStarts()
    {
        await TypeScriptAppHostTest.Run(
            appHostProject: "CommunityToolkit.Aspire.Hosting.Minio.AppHost.TypeScript",
            packageName: "CommunityToolkit.Aspire.Hosting.Minio",
            exampleName: "minio",
            waitForResources: ["minio", "minio-defaults", "minio-bind-mount"],
            cancellationToken: TestContext.Current.CancellationToken);
    }
}