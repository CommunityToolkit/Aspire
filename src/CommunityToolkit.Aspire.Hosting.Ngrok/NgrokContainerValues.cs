namespace CommunityToolkit.Aspire.Hosting.Ngrok;

internal static class NgrokContainerValues
{
    /// <remarks>docker.io</remarks>
    public const string Registry = "docker.io";

    /// <remarks>ngrok/ngrok</remarks>
    public const string Image = "ngrok/ngrok";

    /// <remarks>3</remarks>
    public const string Tag = "3";

    /// <remarks>NGROK_AUTHTOKEN</remarks>
    public const string AuthTokenEnvName = "NGROK_AUTHTOKEN";
}