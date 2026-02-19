namespace CommunityToolkit.Aspire.Chroma
{
    public sealed partial class ChromaClientSettings
    {
        public bool DisableHealthChecks { get { throw null; } set { } }

        public System.Uri? Endpoint { get { throw null; } set { } }

        public int? HealthCheckTimeout { get { throw null; } set { } }
    }
}

namespace Microsoft.Extensions.Hosting
{
    public static partial class AspireChromaExtensions
    {
        public static void AddChromaClient(this IHostApplicationBuilder builder, string connectionName, System.Action<CommunityToolkit.Aspire.Chroma.ChromaClientSettings>? configureSettings = null) { }

        public static void AddKeyedChromaClient(this IHostApplicationBuilder builder, string name, System.Action<CommunityToolkit.Aspire.Chroma.ChromaClientSettings>? configureSettings = null) { }
    }
}
