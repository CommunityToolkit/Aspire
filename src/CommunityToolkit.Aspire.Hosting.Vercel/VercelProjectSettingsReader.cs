using System.Text.Json;
using System.Text.Json.Serialization;
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
            var settings = JsonSerializer.Deserialize<VercelProjectSettingsJson>(projectJsonContent);

            return new(settings?.ProjectName ?? string.Empty, settings?.ProjectId, settings?.OrgId);
        }
        catch (JsonException ex)
        {
            throw new DistributedApplicationException($"Vercel project settings file '{projectJsonPath}' is invalid JSON.", ex);
        }
    }

    private sealed class VercelProjectSettingsJson
    {
        [JsonPropertyName("projectName")]
        public string? ProjectName { get; init; }

        [JsonPropertyName("projectId")]
        public string? ProjectId { get; init; }

        [JsonPropertyName("orgId")]
        public string? OrgId { get; init; }
    }
}
