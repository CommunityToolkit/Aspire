using System.Text.Json;
using System.Text.Json.Serialization;
using Aspire.Hosting;

namespace CommunityToolkit.Aspire.Hosting.Vercel;

internal static class VercelOidcToken
{
    public static VercelOidcClaims DecodeUnvalidatedClaims(string token)
    {
        string[] parts = token.Split('.');
        if (parts.Length != 3)
        {
            throw new DistributedApplicationException("The Vercel OIDC token is not a valid compact JWT.");
        }

        try
        {
            // This is an unvalidated decode of the Vercel-issued token from `vercel pull`.
            // Docker/Vercel validate the token when it is used; here we only need routing
            // metadata such as owner_id/project to construct the VCR login and repository.
            // See https://vercel.com/docs/container-registry.
            byte[] payloadBytes = Convert.FromBase64String(PadBase64Url(parts[1]));
            var claims = JsonSerializer.Deserialize<VercelOidcTokenClaimsJson>(payloadBytes);

            return new(
                claims?.OwnerId,
                claims?.Owner,
                claims?.Project,
                claims?.ProjectId);
        }
        catch (Exception ex) when (ex is FormatException or JsonException)
        {
            throw new DistributedApplicationException("The Vercel OIDC token payload could not be decoded.", ex);
        }
    }

    private static string PadBase64Url(string value)
    {
        string padded = value.Replace('-', '+').Replace('_', '/');
        return padded.PadRight(padded.Length + (4 - padded.Length % 4) % 4, '=');
    }

    private sealed class VercelOidcTokenClaimsJson
    {
        [JsonPropertyName("owner_id")]
        public string? OwnerId { get; init; }

        [JsonPropertyName("owner")]
        public string? Owner { get; init; }

        [JsonPropertyName("project")]
        public string? Project { get; init; }

        [JsonPropertyName("project_id")]
        public string? ProjectId { get; init; }
    }
}
