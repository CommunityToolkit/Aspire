using Aspire.CommunityToolkit.Hosting.Ollama.Web.MEAI.Components;
using Microsoft.Extensions.AI;

var builder = WebApplication.CreateBuilder(args);

// Add service defaults & Aspire components.
builder.AddServiceDefaults();

// Add services to the container.
builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();

builder.Services.AddOutputCache();

builder.AddOllamaApiClient("ollama");

builder.Services.AddChatClient(
    builder => 
        {
            return new OllamaChatClient(new Uri("http://localhost:11434/"), modelId: "phi3.5");
        });

var app = builder.Build();

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error", createScopeForErrors: true);
    // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
    app.UseHsts();
}

app.UseHttpsRedirection();

app.UseStaticFiles();
app.UseAntiforgery();

app.UseOutputCache();

app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();

app.MapDefaultEndpoints();


//app.Services.AddChatClient(builder => builder
//    .UseLogging()
//    .UseFunctionInvocation()
//    .UseDistributedCache()
//    .UseOpenTelemetry()
//    .Use(new OllamaChatClient(new Uri("http://localhost:11434/"), "llama3.2").AsChatClient());

app.Run();
