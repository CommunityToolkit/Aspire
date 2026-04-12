using Aspire.Hosting;

const string ModelUrl = "https://huggingface.co/ggml-org/Qwen2.5-Coder-0.5B-Q8_0-GGUF/resolve/main/qwen2.5-coder-0.5b-q8_0.gguf";

var builder = DistributedApplication.CreateBuilder(args);

//Define two llama servers, sharing the same data volume, allowing them to share the model files and avoid redundant downloads.
var llamaServer = builder.AddLlamaServer("llamaserver", ModelUrl)
    .WithDataVolume()
    .WithContextSize(65535)
    .WithModelAlias("tiny-model");


builder.Build().Run();
