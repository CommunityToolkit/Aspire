# CommunityToolkit.Aspire.Hosting.LlamaCpp library

Provides extension methods and resource definitions for the Aspire AppHost to support running [LlamaCpp](https://github.com/ggml-org/llama.cpp) containers.

The CommunityToolkit already includes the awesome CommunityToolkit.Aspire.Hosting.Ollama project which is a very mature OpenAI-compatible server based on LlamaCpp.
However, some scenarios may need more lightweight alternatives that still allow full OpenAI api compatibility. For such scenarios, this library comes in handy.

You can choose the CommunityToolkit.Aspire.Hosting.LlamaCpp library when:

- You want to run inference in small, lightweight containers.
- You'll run inference in IOT and EDGE devices with limited resources.
- You want to segregate your models in different small containers rather than in one larger one.
- You don't need the extra features of an Ollama-based server.
- You want to have more control over the configuration of your LlamaCpp containers.*
- You want to use less resources for scaling out many small containers rather than fewer larger ones.
- You want to have specific versions/optimizations of LlamaCpp server that may not be supported by Ollama yet.*

*At this moment, this library provides support for LlamaCpp server containers. Plans for the future include support for other LlamaCpp-based tools, like llama-cli, llama-completion and quantization tools.

## Getting Started

### Install the package

In your AppHost project, install the package using the following command:

```dotnetcli
dotnet add package CommunityToolkit.Aspire.Hosting.LlamaCpp
```

### Example usage

Then, in the _Program.cs_ file of `AppHost`, define a LlamaCpp server resource, then call `AddLlamaServer`. You need to pass the url of a model that will be downloaded on start:

```csharp
// Define the url of the model to be downloaded on start. Let's use the Phi-3-mini-4k-instruct model from HuggingFace as an example.
var modelUrl = "https://huggingface.co/microsoft/Phi-3-mini-4k-instruct-gguf/resolve/main/Phi-3-mini-4k-instruct-q4.gguf";

var llamaServer = builder.AddLlamaServer("llamaserver", modelUrl);
```
This will add a LlamaCpp server to your distributed application. The LlamaCpp server already provides a lightweight chat Web UI to interact with the model.

The library also provides the ability to load multimodal models, define a data volume for the model and mmproj files, have that volume shared between llamaserver containers, and more.

Useful extension methods include:
- .WithReasoning(bool useReasoning = true) // Explicitly enables or disables the output of thinking (cot), if supported by the model.
- .WithApikeys(params string[] keys) // Defines one or more valid Api keys that will be set as requirement to make requests to the REST api.
- .WithContextSize(int size = 0) // Sets a limit to the context size for the model.
- .WithModelAlias(string alias) //Defines the alias that will be used by OpenAI-compatible clients to make requests to the model.
- .WithMultimodalProjection(string projectionFileUrl) // Adds a multimodal projection file for multimodal (image/text) models.
- .WithDataVolume(string? name = null, bool isReadOnly = false) // Adds a volume and sets it as the storage for the downloaded model/s.
- .WithDataVolume(IResourceBuilder<LlamaCppServerResource> volumeOwner, bool isReadOnly = false) // Explicitly uses the same volume that is used by another LlamaCppServer resource. Useful for having several server instances that use the same model files, so they are downloaded once and shared among them.

Please see the additional documentation to learn about all the features available in the library.

## Additional Information

Please refer to the official CommunityToolkit documentation for more details on how to use the library and its features.

## Feedback & contributing

https://github.com/CommunityToolkit/Aspire

