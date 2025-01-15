# CommunityToolkit.Aspire.OllamaSharp library

Registers `IOllamaClientApi` in the DI container to interact with the [Ollama](https://ollama.com) API and optionally supports registering an `IChatClient` or `IEmbeddingGenerator` from [Microsoft.Extensions.AI](https://devblogs.microsoft.com/dotnet/introducing-microsoft-extensions-ai-preview/).

## Getting Started

### Prerequisites

-   Ollama HTTP(S) endpoint

### Install the package

Install the .NET Aspire OllamaSharp library using the following command:

```dotnetcli
dotnet add package CommunityToolkit.Aspire.OllamaSharp
```

### Example usage

In the _Program.cs_ file of your project, call the `AddOllamaClientApi` extension method to register the `IOllamaClientApi` in the DI container. This method takes the connection name as a parameter:

```csharp
builder.AddOllamaClientApi("ollama");
```

Then, in your service, inject `IOllamaClientApi` and use it to interact with the Ollama API:

```csharp
public class MyService(IOllamaClientApi ollamaClientApi)
{
    // ...
}
```

#### Integration with Microsoft.Extensions.AI

To use the integration with Microsoft.Extensions.AI, call the `AddOllamaSharpChatClient` or `AddOllamaSharpEmbeddingGenerator` extension method in the _Program.cs_ file of your project. These methods take the connection name as a parameter, just as `AddOllamaClientApi` does, and will register the `IOllamaApiClient`, as well as the `IChatClient` or `IEmbeddingGenerator` in the DI container. The `IEmbeddingsGenerator` is registered with the generic arguments of `<string, Embedding<float>>`.

## Additional documentation

-   https://github.com/awaescher/OllamaSharp
-   https://learn.microsoft.com/dotnet/aspire/community-toolkit/ollama
-   https://devblogs.microsoft.com/dotnet/introducing-microsoft-extensions-ai-preview/

## Feedback & contributing

https://github.com/CommunityToolkit/Aspire
