using System.Text.Json;
using System.Text.Json.Nodes;

namespace CommunityToolkit.Aspire.Hosting.Vercel;

/// <summary>
/// Writes the minimal Vercel Build Output API directory that points Vercel at an Aspire-built
/// container image instead of asking Vercel to build source code.
/// </summary>
internal static class VercelBuildOutputWriter
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = true
    };

    public static async Task WriteAsync(
        VercelDeploymentProjectGroup group,
        VercelPulledProject project,
        IReadOnlyList<VercelResolvedDeployment> resolvedDeployments,
        CancellationToken cancellationToken)
    {
        // Vercel Build Output API v3 expects the file-system contract documented at
        // https://vercel.com/docs/build-output-api and
        // https://vercel.com/docs/build-output-api/configuration:
        //   .vercel/project.json                          copied project identity from `vercel pull`
        //   .vercel/output/config.json                    routes, services, bindings, and API version
        //   .vercel/output/services/{service}/functions/index.func/.vc-config.json
        //       { "runtime": "container", "handler": "<vcr image>@sha256:..." }
        // There is intentionally no user source copy here; Aspire's build/push pipeline has
        // already built the image, and Vercel deploy uploads only metadata that points at it.
        string vercelDirectory = Path.Combine(group.RootEntry.EffectiveDeployDirectory, VercelConstants.DirectoryName);
        string outputDirectory = Path.Combine(vercelDirectory, VercelConstants.OutputDirectoryName);
        Directory.CreateDirectory(outputDirectory);

        await File.WriteAllTextAsync(Path.Combine(vercelDirectory, VercelConstants.ProjectFileName), project.ProjectJsonContent, cancellationToken).ConfigureAwait(false);

        var resolvedByResourceName = resolvedDeployments.ToDictionary(static resolved => resolved.PreparedDeployment.Entry.Resource.Name, StringComparer.Ordinal);
        JsonObject experimentalServices = [];
        JsonArray services = [];

        foreach (var service in group.Services)
        {
            var resolved = resolvedByResourceName[service.Entry.Resource.Name];
            string serviceOutputDirectory = Path.Combine(outputDirectory, "services", service.ServiceName);
            string functionDirectory = Path.Combine(serviceOutputDirectory, "functions", "index.func");
            Directory.CreateDirectory(functionDirectory);

            var serviceConfig = new JsonObject
            {
                ["version"] = VercelConstants.BuildOutputApiVersion,
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
                },
                ["crons"] = new JsonArray()
            };

            var functionConfig = new JsonObject
            {
                ["handler"] = resolved.Image.Reference,
                ["runtime"] = "container",
                ["environment"] = CreateEnvironmentObject(resolved.PreparedDeployment.EnvironmentConfiguration.DeploymentEnvironmentVariables)
            };

            await File.WriteAllTextAsync(Path.Combine(serviceOutputDirectory, "config.json"), serviceConfig.ToJsonString(JsonOptions), cancellationToken).ConfigureAwait(false);
            await File.WriteAllTextAsync(Path.Combine(functionDirectory, ".vc-config.json"), functionConfig.ToJsonString(JsonOptions), cancellationToken).ConfigureAwait(false);

            var serviceDefinition = new JsonObject
            {
                ["root"] = $"{service.ServiceName}/"
            };
            var serviceManifestEntry = new JsonObject
            {
                ["schema"] = "experimentalServicesV2",
                ["name"] = service.ServiceName,
                ["root"] = service.ServiceName
            };

            JsonArray bindings = CreateBindingsArray(resolved.PreparedDeployment.EnvironmentConfiguration.ServiceBindings);
            if (bindings.Count > 0)
            {
                serviceDefinition["bindings"] = bindings.DeepClone();
                serviceManifestEntry["bindings"] = bindings;
            }

            experimentalServices[service.ServiceName] = serviceDefinition;
            services.Add(serviceManifestEntry);
        }

        var outputConfig = new JsonObject
        {
            ["version"] = VercelConstants.BuildOutputApiVersion,
            ["routes"] = new JsonArray
            {
                new JsonObject
                {
                    ["handle"] = "filesystem"
                },
                new JsonObject
                {
                    ["src"] = "^(?:/(.*))$",
                    ["destination"] = new JsonObject
                    {
                        ["service"] = group.Root.ServiceName,
                        ["type"] = "service"
                    }
                }
            },
            ["crons"] = new JsonArray(),
            ["experimentalServicesV2"] = experimentalServices,
            ["services"] = services
        };

        await File.WriteAllTextAsync(Path.Combine(outputDirectory, "config.json"), outputConfig.ToJsonString(JsonOptions), cancellationToken).ConfigureAwait(false);
    }

    private static JsonObject CreateEnvironmentObject(IReadOnlyList<KeyValuePair<string, string>> environmentVariables)
    {
        JsonObject environment = [];
        foreach (var environmentVariable in environmentVariables)
        {
            environment[environmentVariable.Key] = environmentVariable.Value;
        }

        return environment;
    }

    private static JsonArray CreateBindingsArray(IReadOnlyList<VercelServiceBinding> serviceBindings)
    {
        JsonArray bindings = [];
        foreach (var binding in serviceBindings)
        {
            bindings.Add(new JsonObject
            {
                ["type"] = "service",
                ["service"] = binding.ServiceName,
                ["format"] = "url",
                ["env"] = binding.EnvironmentVariableName
            });
        }

        return bindings;
    }
}
