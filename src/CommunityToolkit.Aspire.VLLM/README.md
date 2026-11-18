# CommunityToolkit.Aspire.VLLM library

Registers a [`Microsoft.Extensions.AI`](https://learn.microsoft.com/dotnet/ai/microsoft-extensions-ai) `IChatClient` that targets a [vLLM](https://docs.vllm.ai) OpenAI-compatible inference server. It also enables the corresponding health check and telemetry.

The client applies vLLM-specific defaults:

- a **placeholder API key** when none is configured (vLLM doesn't require one, but the OpenAI client requires a non-empty credential),
- the **`/v1` base path**, appended automatically,
- the **served model name**, resolved from `AddChatClient(modelId)`, settings, or the model resource's connection string,
- a **`/health` health check** that reports readiness of the vLLM engine.

## Getting Started

### Install the package

Install the package in your client project:

```dotnetcli
dotnet add package CommunityToolkit.Aspire.VLLM
```

### Example usage

In the _Program.cs_ file of your project, call `AddVLLMClient` to register the vLLM client, then `AddChatClient` to register an `IChatClient`:

```csharp
builder.AddVLLMClient("qwen")
    .AddChatClient();
```

You can then inject `IChatClient` into your services:

```csharp
public class ChatService(IChatClient chatClient)
{
    public async Task<string> AskAsync(string question)
    {
        var response = await chatClient.GetResponseAsync(question);
        return response.Text;
    }
}
```

## Configuration

The vLLM client integration provides multiple options to configure the server connection.

### Use a connection string

When using a connection string from the `ConnectionStrings` configuration section, provide the connection name when calling `AddVLLMClient`:

```csharp
builder.AddVLLMClient("vllm");
```

The connection string is retrieved from the `ConnectionStrings` configuration section. It can be a bare URI or a set of key-value pairs. A model resource reference supplies both `Endpoint` and `Model`; a bare URI or direct server reference requires the model to be configured separately:

```json
{
  "ConnectionStrings": {
    "vllm": "Endpoint=http://localhost:8000;Model=Qwen/Qwen3-0.6B"
  }
}
```

### Use configuration providers

The vLLM client integration supports `Microsoft.Extensions.Configuration`. It loads `VLLMClientSettings` from the `Aspire:VLLM` section, then from `Aspire:VLLM:{connectionName}`:

```json
{
  "Aspire": {
    "VLLM": {
      "vllm": {
        "Model": "Qwen/Qwen3-0.6B",
        "DisableHealthChecks": false,
        "DisableTracing": false
      }
    }
  }
}
```

For the model name, a value passed to `AddChatClient(modelId)` takes precedence over inline settings, configuration, and the connection string, in that order. When the server uses `WithServedModelName`, use that alias as the model name.

| Setting | Description |
| --- | --- |
| `Endpoint` | Base endpoint of the vLLM server, for example `http://localhost:8000`. Usually provided by the connection string. |
| `Key` | API key. Optional; a placeholder is used when empty. |
| `Model` | Served model name (`--served-model-name`) to target. |
| `DisableHealthChecks` | Disables the `/health` health check. |
| `HealthCheckTimeout` | Health check timeout, in milliseconds. |
| `DisableTracing` | Disables OpenTelemetry tracing of chat calls. |
| `EnableSensitiveTelemetryData` | Includes prompts and completions in telemetry. Disabled by default. |

### Use inline delegates

You can also pass the `Action<VLLMClientSettings> configureSettings` delegate to set up some or all of the options inline:

```csharp
builder.AddVLLMClient("vllm", settings => settings.Model = "Qwen/Qwen3-0.6B")
    .AddChatClient();
```

### Keyed registration

To connect to more than one vLLM server, use the keyed overloads:

```csharp
builder.AddKeyedVLLMClient("chat")
    .AddKeyedChatClient("Qwen/Qwen3-0.6B");
```

## AppHost integration

In your AppHost project, install the `CommunityToolkit.Aspire.Hosting.VLLM` library and reference the model resource from the consuming project:

```csharp
var vllm = builder.AddVLLM("vllm");
var model = vllm.AddModel(name: "qwen", model: "Qwen/Qwen3-0.6B");

builder.AddProject<Projects.MyApp>("myapp")
    .WithReference(model)
    .WaitFor(model);
```

## Additional documentation

- https://docs.vllm.ai
- https://learn.microsoft.com/dotnet/ai/microsoft-extensions-ai

## Feedback & contributing

https://github.com/CommunityToolkit/Aspire
