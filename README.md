# Raygun.Aspire.Hosting.Ollama

An Aspire component leveraging the Ollama container with support for downloading a model on startup.

# Installation

## 1. Install the NuGet package

Install the [Raygun.Aspire.Hosting.Ollama](https://www.nuget.org/packages/Raygun.Aspire.Hosting.Ollama) NuGet package into the Aspire orchestration project (AppHost). Either use the NuGet package management GUI in the IDE you use, OR use the below dotnet command.

```bash
dotnet add package Raygun.Aspire.Hosting.Ollama
```

## 2. Add the Ollama container

Use the static `AddOllama` method to add this container component to the application builder.

```csharp
// The distributed application builder is created here

var ollama = builder.AddOllama();

// The builder is used to build and run the app somewhere down here
```

# Configuration

The AddOllama method has optional arguments to set the `name`, `port` and `modelName`.
The `name` is what gets displayed in the Aspire orchestration app against this component.
The `port` is provided randomly by Aspire. If for whatever reason you need a fixed port, you can set that here.
The `modelName` specifies what LLM to pull when it starts up. The default is `llama3`. You can also set this to null to prevent any models being pulled on startup - leaving you with a plain Ollama container to work with.

# Downloading the LLM

When the Ollama container for this component first spins up, this component will download the LLM (llama3 unless otherwise specified).
The progress of this download will be displayed in the State column for this component on the Aspire orchestration app.
Important: Keep the Aspire orchestration app open until the download is complete, otherwise the download will be cancelled.
In the spirit of productivity, we recommend kicking off this process before heading for lunch.
This component binds a volume called "ollama" so that once the model is fully downloaded, it'll be available for subsequent runs.

# Accessing the Ollama server from other Aspire components

You can pass the ollama component to other Aspire components in the usual way:

```csharp
builder.AddMyComponent().WithReference(ollama);
```

Within that component (e.g. a web app), you can fetch the Ollama connection string from the application builder as follows.
Note that if you changed the name of the Ollama component via the `name` argument, then you'll need to use that here when specifying which connection string to get.

```csharp
var connectionString = builder.Configuration.GetConnectionString("Ollama");
```

You can then call any of the Ollama endpoints through this connection string. We recommend using the [OllamaSharp](https://www.nuget.org/packages/OllamaSharp) client to do this.