namespace CommunityToolkit.Aspire.Hosting.Vercel;

/// <summary>
/// Translates Aspire's Vercel deployment options into the environment names expected by Vercel
/// project environment-variable commands and persisted state cleanup.
/// </summary>
internal static class VercelProjectEnvironment
{
    public static string GetName(VercelEnvironmentOptionsAnnotation options)
    {
        // Vercel CLI env commands use "production" and "preview" environment names, while
        // deploy uses --prod or --target. Keep this translation in one place so state cleanup
        // removes variables from the same Vercel environment deploy configured.
        // See https://vercel.com/docs/cli/env and https://vercel.com/docs/cli/deploy.
        if (options.Production)
        {
            return "production";
        }

        return string.IsNullOrWhiteSpace(options.Target) ? "preview" : options.Target;
    }

    public static string GetName(VercelDeploymentState state)
    {
        if (state.Production)
        {
            return "production";
        }

        return string.IsNullOrWhiteSpace(state.Target) ? "preview" : state.Target;
    }
}
