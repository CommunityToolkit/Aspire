using System.Text.Json;
using System.Text.Json.Nodes;

namespace CommunityToolkit.Aspire.Hosting.Vercel;

internal static class VercelBuildOutputWriter
{
    private const int VercelBuildOutputApiVersion = 3;
    private const string VercelDirectoryName = ".vercel";
    private const string VercelOutputDirectoryName = "output";
    private const string VercelProjectFileName = "project.json";

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = true
    };

    public static async Task WriteAsync(
        VercelDeploymentEntry entry,
        VercelPulledProject project,
        string imageReference,
        CancellationToken cancellationToken)
    {
        // Vercel Build Output API v3 expects the file-system contract documented at
        // https://vercel.com/docs/build-output-api and
        // https://vercel.com/docs/build-output-api/configuration:
        //   .vercel/project.json                          copied project identity from `vercel pull`
        //   .vercel/output/config.json                    routes and API version
        //   .vercel/output/functions/index.func/.vc-config.json
        //       { "runtime": "container", "handler": "<vcr image>@sha256:..." }
        // There is intentionally no user source copy here; Aspire's build/push pipeline has
        // already built the image, and Vercel deploy uploads only metadata that points at it.
        string vercelDirectory = Path.Combine(entry.DeployDirectory, VercelDirectoryName);
        string outputDirectory = Path.Combine(vercelDirectory, VercelOutputDirectoryName);
        string functionDirectory = Path.Combine(outputDirectory, "functions", "index.func");
        Directory.CreateDirectory(functionDirectory);

        await File.WriteAllTextAsync(Path.Combine(vercelDirectory, VercelProjectFileName), project.ProjectJsonContent, cancellationToken).ConfigureAwait(false);

        var outputConfig = new JsonObject
        {
            ["version"] = VercelBuildOutputApiVersion,
            ["routes"] = new JsonArray
            {
                new JsonObject
                {
                    ["handle"] = "filesystem"
                },
                new JsonObject
                {
                    ["src"] = "/(.*)",
                    ["dest"] = "/index"
                }
            }
        };

        var functionConfig = new JsonObject
        {
            ["handler"] = imageReference,
            ["runtime"] = "container",
            ["environment"] = new JsonObject()
        };

        await File.WriteAllTextAsync(Path.Combine(outputDirectory, "config.json"), outputConfig.ToJsonString(JsonOptions), cancellationToken).ConfigureAwait(false);
        await File.WriteAllTextAsync(Path.Combine(functionDirectory, ".vc-config.json"), functionConfig.ToJsonString(JsonOptions), cancellationToken).ConfigureAwait(false);
    }
}
