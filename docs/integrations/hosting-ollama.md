# Aspire.CommunityToolkit.Hosting.Ollama

[![Aspire.CommunityToolkit.Ollama](https://img.shields.io/nuget/v/Aspire.CommunityToolkit.Ollama)](https://nuget.org/packages/Aspire.CommunityToolkit.Ollama/) | [![Aspire.CommunityToolkit.Ollama (latest)](<https://img.shields.io/nuget/vpre/Aspire.CommunityToolkit.Ollama?label=nuget%20(preview)>)](https://nuget.org/packages/Aspire.CommunityToolkit.Ollama/absoluteLatest)

## Overview

An Aspire component leveraging the [Ollama](https://ollama.com) container with support for downloading a model on startup.

## Usage

Use the static `AddOllama` method to add this container component to the application builder.

```csharp
// The distributed application builder is created here

var ollama = builder.AddOllama("ollama").AddModel("llama3");

// The builder is used to build and run the app somewhere down here
```

### Configuration

The AddOllama method has optional arguments to set the `name` and `port`.
The `name` is what gets displayed in the Aspire orchestration app against this component.
The `port` is provided randomly by Aspire. If for whatever reason you need a fixed port, you can set that here.

## Downloading the LLM

When the Ollama container for this component first spins up, this component will download the LLM(s).
The progress of this download will be displayed in the State column for this component on the Aspire orchestration app.
Important: Keep the Aspire orchestration app open until the download is complete, otherwise the download will be cancelled.
In the spirit of productivity, we recommend kicking off this process before heading for lunch.
This component binds a volume called "ollama" so that once the model is fully downloaded, it'll be available for subsequent runs.

## Accessing the Ollama server from other Aspire components

You can pass the ollama component to other Aspire components in the usual way:

```csharp
builder.AddMyComponent().WithReference(ollama);
```

Within that component (e.g. a web app), you can fetch the Ollama connection string from the application builder as follows.
Note that if you changed the name of the Ollama component via the `name` argument, then you'll need to use that here when specifying which connection string to get.

```csharp
var connectionString = builder.Configuration.GetConnectionString("ollama");
```

You can then call any of the Ollama endpoints through this connection string. We recommend using the [OllamaSharp](https://www.nuget.org/packages/OllamaSharp) client to do this.
