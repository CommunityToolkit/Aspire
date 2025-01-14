# CommunityToolkit.Aspire.Hosting.Ollama library

Provides extension methods and resource definitions for the .NET Aspire AppHost to support running [Ollama](https://ollama.com) containers with support for downloading a model on startup.

It also provides support for running [Open WebUI](https://openwebui.com) to interact with the Ollama container.

## Getting Started

### Install the package

In your AppHost project, install the package using the following command:

```dotnetcli
dotnet add package CommunityToolkit.Aspire.Hosting.Ollama
```

### Example usage

Then, in the _Program.cs_ file of `AppHost`, define an Ollama resource, then call `AddOllama`:

```csharp
var ollama = builder.AddOllama("ollama")
    .WithModel("phi3")
    .WithOpenWebUI();
```

## Additional Information

https://learn.microsoft.com/dotnet/aspire/community-toolkit/ollama

## Feedback & contributing

https://github.com/CommunityToolkit/Aspire

