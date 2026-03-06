using Microsoft.DotNet.XUnitExtensions;

namespace CommunityToolkit.Aspire.Hosting.Neon.Tests;

internal sealed record NeonIntegrationTestSettings(
    string ApiKey,
    string ProjectName,
    string EphemeralPrefix,
    string ExistingBranchName)
{
    public static NeonIntegrationTestSettings Require()
    {
        if (!TryCreate(out NeonIntegrationTestSettings? settings, out string? reason))
        {
            throw new SkipTestException(reason ?? "Neon integration tests are not configured.");
        }

        return settings!;
    }

    public static bool TryCreate(out NeonIntegrationTestSettings? settings, out string? reason)
    {
        settings = null;

        bool enabled = ReadBool("RUN_NEON_INTEGRATION_TESTS");
        if (!enabled)
        {
            reason = "Neon integration tests are disabled. Set RUN_NEON_INTEGRATION_TESTS=1 to enable.";
            return false;
        }

        string? apiKey = ReadRequired("NEON_INTEGRATION_API_KEY");
        if (string.IsNullOrWhiteSpace(apiKey))
        {
            reason = "Missing NEON_INTEGRATION_API_KEY. Provide a Neon API key to run integration tests.";
            return false;
        }

        string projectName = ReadOptional("NEON_INTEGRATION_PROJECT_NAME") ?? "aspire-neon-integration";
        string ephemeralPrefix = ReadOptional("NEON_INTEGRATION_EPHEMERAL_PREFIX") ?? "aspire-it-";
        string existingBranchName = ReadOptional("NEON_INTEGRATION_EXISTING_BRANCH_NAME") ?? "main";

        settings = new NeonIntegrationTestSettings(
            apiKey,
            projectName,
            ephemeralPrefix,
            existingBranchName);

        reason = null;
        return true;
    }

    private static string? ReadOptional(string name) => Environment.GetEnvironmentVariable(name);

    private static string? ReadRequired(string name)
    {
        string? value = ReadOptional(name);
        return string.IsNullOrWhiteSpace(value) ? null : value;
    }

    private static bool ReadBool(string name)
    {
        string? value = ReadOptional(name);
        return value is not null &&
            (string.Equals(value, "1", StringComparison.OrdinalIgnoreCase)
            || string.Equals(value, "true", StringComparison.OrdinalIgnoreCase)
            || string.Equals(value, "yes", StringComparison.OrdinalIgnoreCase));
    }
}
