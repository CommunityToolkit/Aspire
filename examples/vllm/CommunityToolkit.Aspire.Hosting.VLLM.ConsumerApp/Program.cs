using CommunityToolkit.Aspire.Hosting.VLLM.ConsumerApp;

var builder = Host.CreateApplicationBuilder(args);

builder.AddVLLMClient("vllm")
    .AddChatClient("Qwen/Qwen3-0.6B");

builder.Services.AddHostedService<Worker>();

builder.Build().Run();
