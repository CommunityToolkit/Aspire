using CommunityToolkit.Aspire.Hosting.VLLM.ConsumerApp;

var builder = Host.CreateApplicationBuilder(args);

builder.AddVLLMClient("qwen")
    .AddChatClient();

builder.Services.AddHostedService<Worker>();

builder.Build().Run();
