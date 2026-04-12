using Aspire.Hosting;
using Aspire.Hosting.ApplicationModel;
using CommunityToolkit.Aspire.Hosting.Compose.Parsing.Contracts;

namespace CommunityToolkit.Aspire.Hosting.Compose.Mapping;

/// <summary>
/// Maps compose depends_on to Aspire WaitFor/WaitForCompletion.
/// </summary>
internal static class DependsOnMapper
{
    public static void Apply(ComposeFile composeFile, Dictionary<string, IResourceBuilder<ContainerResource>> resources)
    {
        foreach ((string serviceName, ComposeService service) in composeFile.Services)
        {
            if (service.DependsOn is not { } dependsOn || !resources.TryGetValue(serviceName, out IResourceBuilder<ContainerResource>? resource))
                continue;

            foreach ((string depName, string condition) in Parse(dependsOn))
            {
                if (!resources.TryGetValue(depName, out IResourceBuilder<ContainerResource>? dependency))
                    throw new InvalidOperationException($"Service '{serviceName}' depends on '{depName}', but '{depName}' is not defined in the compose file.");

                _ = condition switch
                {
                    ComposeConstants.Condition.ServiceCompletedSuccessfully => resource.WaitForCompletion(dependency),
                    _ => resource.WaitFor(dependency)
                };
            }
        }
    }

    internal static Dictionary<string, string> Parse(object dependsOn)
    {
        Dictionary<string, string> result = new(StringComparer.OrdinalIgnoreCase);

        switch (dependsOn)
        {
            case List<object> list:
                foreach (object item in list)
                    result[item.ToString()!] = ComposeConstants.Condition.ServiceStarted;
                break;

            case Dictionary<object, object> dict:
                foreach ((object key, object value) in dict)
                {
                    result[key.ToString()!] = value is Dictionary<object, object> condDict
                        && condDict.TryGetValue(ComposeConstants.Condition.Key, out object? condObj)
                            ? condObj.ToString()!
                            : ComposeConstants.Condition.ServiceStarted;
                }
                break;
        }

        return result;
    }
}
