# CommunityToolkit.Aspire.OllamaSharp library

Registers `IOllamaApiClient` in the DI container to interact with the [Ollama](https://ollama.com) API and optionally supports registering an `IChatClient` or `IEmbeddingGenerator` from [Microsoft.Extensions.AI](https://devblogs.microsoft.com/dotnet/introducing-microsoft-extensions-ai-preview/).

## Getting Started

### Prerequisites

-   Ollama HTTP(S) endpoint

### Install the package

Install the .NET Aspire OllamaSharp library using the following command:

```dotnetcli
dotnet add package CommunityToolkit.Aspire.OllamaSharp
```

### Example usage

In the _Program.cs_ file of your project, call the `AddOllamaApiClient` extension method to register the `IOllamaApiClient` in the DI container. This method takes the connection name as a parameter:

```csharp
builder.AddOllamaApiClient("ollama");
```

Then, in your service, inject `IOllamaApiClient` and use it to interact with the Ollama API:

```csharp
public class MyService(IOllamaApiClient ollamaApiClient)
{
    // ...
}
```

#### Integration with Microsoft.Extensions.AI

To use the integration with Microsoft.Extensions.AI, call the `AddOllamaSharpChatClient` or `AddOllamaSharpEmbeddingGenerator` extension method in the _Program.cs_ file of your project. These methods take the connection name as a parameter, just as `AddOllamaApiClient` does, and will register the `IOllamaApiClient`, as well as the `IChatClient` or `IEmbeddingGenerator` in the DI container. The `IEmbeddingsGenerator` is registered with the generic arguments of `<string, Embedding<float>>`.

#### Configuring OpenTelemetry

When using the chat client integration, you can optionally configure the OpenTelemetry chat client to control telemetry behavior such as enabling sensitive data:

```csharp
builder.AddOllamaApiClient("ollama")
    .AddChatClient(otel => otel.EnableSensitiveData = true);
```

The integration automatically registers the Microsoft.Extensions.AI telemetry source (`Experimental.Microsoft.Extensions.AI`) with OpenTelemetry for distributed tracing.

## Additional documentation

-   https://github.com/awaescher/OllamaSharp
-   https://learn.microsoft.com/dotnet/aspire/community-toolkit/ollama
-   https://devblogs.microsoft.com/dotnet/introducing-microsoft-extensions-ai-preview/

## Feedback & contributing

https://github.com/CommunityToolkit/Aspire
