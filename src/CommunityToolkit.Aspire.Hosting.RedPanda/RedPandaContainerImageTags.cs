namespace CommunityToolkit.Aspire.Hosting.RedPanda;

internal static class RedPandaContainerImageTags
{
    /// <remarks>docker.redpanda.com</remarks>
    internal const string Registry = "docker.redpanda.com";

    /// <remarks>redpandadata/redpanda</remarks>
    internal const string Image = "redpandadata/redpanda";

    /// <remarks>v26.1.10</remarks>
    internal const string Tag = "v26.1.10";

    /// <remarks>redpandadata/console</remarks>
    internal const string ConsoleImage = "redpandadata/console";

    /// <remarks>v3.7.4</remarks>
    internal const string ConsoleTag = "v3.7.4";

    /// <remarks>docker.io</remarks>
    internal const string KafkaUiRegistry = "docker.io";

    /// <remarks>kafbat/kafka-ui</remarks>
    internal const string KafkaUiImage = "kafbat/kafka-ui";

    /// <remarks>v1.5.0</remarks>
    internal const string KafkaUiTag = "v1.5.0";
}
