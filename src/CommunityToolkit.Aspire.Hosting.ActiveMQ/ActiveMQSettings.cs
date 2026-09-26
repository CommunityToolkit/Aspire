namespace CommunityToolkit.Aspire.Hosting.ActiveMQ;

internal static class ActiveMQSettings
{
    public static IActiveMQSettings ForClassic =>
        new ActiveMQSettingsImpl
        {
            Registry = ActiveMQClassicContainerImageSettings.Registry,
            Image = ActiveMQClassicContainerImageSettings.Image,
            Tag = ActiveMQClassicContainerImageSettings.Tag,
            EnvironmentVariableUsername = ActiveMQClassicContainerImageSettings.EnvironmentVariableUsername,
            EnvironmentVariablePassword = ActiveMQClassicContainerImageSettings.EnvironmentVariablePassword,
            JolokiaPath = ActiveMQClassicContainerImageSettings.JolokiaPath,
            DataPath = ActiveMQClassicContainerImageSettings.DataPath,
            ConfPath = ActiveMQClassicContainerImageSettings.ConfPath
        };

    public static IActiveMQSettings ForArtemis =>
        new ActiveMQSettingsImpl
        {
            Registry = ActiveMQArtemisContainerImageSettings.Registry,
            Image = ActiveMQArtemisContainerImageSettings.Image,
            Tag = ActiveMQArtemisContainerImageSettings.Tag,
            EnvironmentVariableUsername = ActiveMQArtemisContainerImageSettings.EnvironmentVariableUsername,
            EnvironmentVariablePassword = ActiveMQArtemisContainerImageSettings.EnvironmentVariablePassword,
            JolokiaPath = ActiveMQArtemisContainerImageSettings.JolokiaPath,
            DataPath = ActiveMQArtemisContainerImageSettings.DataPath,
            ConfPath = ActiveMQArtemisContainerImageSettings.ConfPath
        };
    private record ActiveMQSettingsImpl : IActiveMQSettings
    {
        public required string Registry { get; init; }
        public required string Image { get; init; }
        public required string Tag { get; init; }
        public required string EnvironmentVariableUsername { get; init; }
        public required string EnvironmentVariablePassword { get; init; }
        public required string JolokiaPath { get; init; }
        public required string DataPath { get; init; }
        public required string ConfPath { get; init; }
    }
}