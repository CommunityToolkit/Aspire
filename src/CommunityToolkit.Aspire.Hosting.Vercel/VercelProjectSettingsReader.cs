using System.Text.Json;
using Aspire.Hosting;

namespace CommunityToolkit.Aspire.Hosting.Vercel;

internal static class VercelProjectSettingsReader
{
    public static VercelPulledProjectSettings Read(string projectJsonPath, string projectJsonContent)
    {
        try
        {
            // `vercel pull` writes `.vercel/project.json`. Only project identity fields are
            // needed: they select the linked provider project and are safe to persist in state.
            using var document = JsonDocument.Parse(projectJsonContent);
            var root = document.RootElement;

            string projectName = root.TryGetProperty("projectName", out var projectNameElement) && projectNameElement.ValueKind == JsonValueKind.String
                ? projectNameElement.GetString() ?? string.Empty
                : string.Empty;
            string? projectId = root.TryGetProperty("projectId", out var projectIdElement) && projectIdElement.ValueKind == JsonValueKind.String
                ? projectIdElement.GetString()
                : null;
            string? orgId = root.TryGetProperty("orgId", out var orgIdElement) && orgIdElement.ValueKind == JsonValueKind.String
                ? orgIdElement.GetString()
                : null;

            return new(projectName, projectId, orgId);
        }
        catch (JsonException ex)
        {
            throw new DistributedApplicationException($"Vercel project settings file '{projectJsonPath}' is invalid JSON.", ex);
        }
    }
}
