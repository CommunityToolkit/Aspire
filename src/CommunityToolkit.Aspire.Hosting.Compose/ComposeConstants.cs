namespace CommunityToolkit.Aspire.Hosting.Compose;

/// <summary>
/// Compose specification constants.
/// </summary>
internal static class ComposeConstants
{
    internal static class TopLevel
    {
        public const string Services = "services";
        public const string Volumes = "volumes";
        public const string Networks = "networks";
        public const string Version = "version";
        public const string Configs = "configs";
        public const string Secrets = "secrets";
        public const string Extensions = "extensions";
        public const string Name = "name";
    }

    internal static class Service
    {
        public const string Image = "image";
        public const string Build = "build";
        public const string ContainerName = "container_name";
        public const string Hostname = "hostname";
        public const string Ports = "ports";
        public const string Environment = "environment";
        public const string DependsOn = "depends_on";
        public const string Command = "command";
        public const string Entrypoint = "entrypoint";
        public const string Healthcheck = "healthcheck";
        public const string Restart = "restart";
    }

    internal static class Health
    {
        public const string Test = "test";
        public const string Interval = "interval";
        public const string Timeout = "timeout";
        public const string Retries = "retries";
        public const string StartPeriod = "start_period";
        public const string Disable = "disable";
    }

    internal static class Resource
    {
        public const string Driver = "driver";
        public const string External = "external";
    }

    internal static class Condition
    {
        public const string Key = "condition";
        public const string ServiceStarted = "service_started";
        public const string ServiceHealthy = "service_healthy";
        public const string ServiceCompletedSuccessfully = "service_completed_successfully";
    }

    internal static class Protocol
    {
        public const string Tcp = "tcp";
        public const string Udp = "udp";
        public const string Http = "http";
        public const string Https = "https";
    }

    internal static class Volume
    {
        public const string ReadOnly = "ro";
    }

    internal static class Defaults
    {
        public const string ScratchImage = "scratch";
        public const string LatestTag = "latest";
    }
}
