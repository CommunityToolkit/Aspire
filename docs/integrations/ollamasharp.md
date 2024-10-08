# Aspire.CommunityTools.OllamaSharp

[![Aspire.CommunityToolkit.OllamaSharp](https://img.shields.io/nuget/v/Aspire.CommunityToolkit.OllamaSharp)](https://nuget.org/packages/Aspire.CommunityToolkit.OllamaSharp/) | [![Aspire.CommunityToolkit.OllamaSharp (latest)](<https://img.shields.io/nuget/vpre/Aspire.CommunityToolkit.OllamaSharp?label=nuget%20(preview)>)](https://nuget.org/packages/Aspire.CommunityToolkit.OllamaSharp/absoluteLatest)

## Overview

A .NET Aspire client integration that uses the [OllamaSharp](https://www.nuget.org/packages/OllamaSharp) client to interact with the Ollama container.

## Usage

Use the static `AddOllamaClientApi` method to add this client integration to the application builder of your client application. A callback can be provided to the `AddOllamaClientApi` method to configure the settings of the Ollama client.

```csharp
builder.AddOllamaClientApi("ollama");
```

Then you can inject the `IOllamaClientApi`` into your client application and use it to interact with the Ollama container.

```csharp
public class MyService(IOllamaClientApi ollamaClientApi)
{
    public async Task DoSomething()
    {
        var chat = new Chat(ollamaClientApi);
        while (true)
        {
            var message = Console.ReadLine();
            await foreach (var answerToken in chat.Send(message))
                Console.Write(answerToken);
        }
    }
}
```
