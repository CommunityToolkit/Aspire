using System.Text;
using System.Text.Json;
using Aspire.Quartz;

namespace Aspire.Quartz;

internal sealed class JobSerializer
{
    private const int MaxParameterSize = 1024 * 1024; // 1MB

    private readonly JsonSerializerOptions _options = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = false
    };

    public byte[] Serialize(object? parameters, JobOptions? options)
    {
        var data = new JobData
        {
            Parameters = parameters,
            Options = options
        };

        var json = JsonSerializer.Serialize(data, _options);

        if (json.Length > MaxParameterSize)
        {
            throw new ArgumentException(
                $"Job parameters exceed maximum size of {MaxParameterSize} bytes. " +
                $"Consider reducing parameter size or storing large data separately.",
                nameof(parameters));
        }

        return Encoding.UTF8.GetBytes(json);
    }

    public (object? Parameters, JobOptions? Options) Deserialize(byte[] data)
    {
        var json = Encoding.UTF8.GetString(data);
        var jobData = JsonSerializer.Deserialize<JobData>(json, _options);

        return (jobData?.Parameters, jobData?.Options);
    }

    private sealed class JobData
    {
        public object? Parameters { get; set; }
        public JobOptions? Options { get; set; }
    }
}
