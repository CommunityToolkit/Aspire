# Aspire.CommunityToolkit.OllamaSharp library

Registers `IOllamaClientApi` in the DI container to interact with the [Ollama](https://ollama.com) API.

## Getting Started

### Prerequisites

-   Ollama HTTP(S) endpoint

### Install the package

Install the .NET Aspire OllamaSharp library using the following command:

```dotnetcli
dotnet add package Aspire.CommunityToolkit.OllamaSharp
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

## Additional documentation

-   https://github.com/awaescher/OllamaSharp
-   https://learn.microsoft.com/dotnet/aspire/community-toolkit/hosting-ollama

## Feedback & contributing

https://github.com/CommunityToolkit/Aspire

