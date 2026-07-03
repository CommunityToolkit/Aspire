namespace CommunityToolkit.Aspire.Hosting.Vercel;

internal static class VercelProjectEnvironment
{
    public static string GetName(VercelEnvironmentOptionsAnnotation options)
    {
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
