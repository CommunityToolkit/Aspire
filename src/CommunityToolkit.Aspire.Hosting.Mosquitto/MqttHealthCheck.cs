using Aspire.Hosting.ApplicationModel;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using MQTTnet;

namespace CommunityToolkit.Aspire.Hosting.Mosquitto;

/// <summary>
/// A protocol-level readiness probe that connects to the Mosquitto broker as a real MQTT client would,
/// so the resource only reports healthy once the broker actually accepts MQTT connections.
/// </summary>
internal sealed class MqttHealthCheck : IHealthCheck
{
    private readonly MqttClientFactory _mqttFactory = new();
    private readonly MqttClientOptions _options;

    public MqttHealthCheck(string connectionString)
    {
        Uri uri = new(connectionString);
        var host = uri.Host;
        var port = uri.Port > 0 ? uri.Port : MosquittoServerResource.DefaultPort;

        _options = new MqttClientOptionsBuilder()
            .WithTcpServer(host, port)
            .Build();
    }

    public async Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default)
    {
        try
        {
            using IMqttClient mqttClient = _mqttFactory.CreateMqttClient();
            MqttClientConnectResult result = await mqttClient.ConnectAsync(_options, cancellationToken).ConfigureAwait(false);

            return result.ResultCode == MqttClientConnectResultCode.Success
                ? HealthCheckResult.Healthy()
                : HealthCheckResult.Unhealthy($"Failed to connect to the MQTT broker. Connect result code: {result.ResultCode}.");
        }
        catch (Exception ex)
        {
            return HealthCheckResult.Unhealthy("Failed to connect to the MQTT broker.", ex);
        }
    }
}
