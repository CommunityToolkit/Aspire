using Aspire.Contribs.WebApp.Clients;
using Aspire.Contribs.WebApp.Components;
using Aspire.Contribs.WebApp.Services;

var builder = WebApplication.CreateBuilder(args);

builder.AddServiceDefaults();

// Add services to the container.
builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();

builder.Services.AddHttpClient<IApiClient, WebApiClient>("apiapp", client =>
{
    client.BaseAddress = new Uri("https+http://apiapp");
});
builder.Services.AddHttpClient<IApiClient, SpringContainerClient>("containerapp", client =>
{
    client.BaseAddress = new Uri("http://containerapp");
});
builder.Services.AddHttpClient<IApiClient, SpringExecutableClient>("executableapp", client =>
{
    client.BaseAddress = new Uri("http://executableapp");
});
builder.Services.AddScoped<IApiClientService, ApiClientService>();

var app = builder.Build();

app.MapDefaultEndpoints();

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error", createScopeForErrors: true);
    // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
    app.UseHsts();
}

app.UseHttpsRedirection();

app.UseStaticFiles();
app.UseAntiforgery();

app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();

app.Run();
