using Aspire.Components.Common.Tests;
using Aspire.Hosting;
using CommunityToolkit.Aspire.Testing;

namespace CommunityToolkit.Aspire.Hosting.Logto.Tests;

[RequiresDocker]
public class AppHostTest
{
    [Fact]
    public async Task LogtoResourceStartsAndRespondsOk()
    {
        var builder = DistributedApplication.CreateBuilder();
        var postgres = builder.AddPostgres("postgres");
        var logto = builder.AddLogtoContainer("logto", postgres);

        using var app = builder.Build();
        var rns = app.Services.GetRequiredService<ResourceNotificationService>();

        await app.StartAsync();

        // Wait for the resource to be healthy
        await rns.WaitForResourceHealthyAsync(logto.Resource.Name).WaitAsync(TimeSpan.FromMinutes(5));

        var httpClient = app.CreateHttpClient(logto.Resource.Name);
        var response = await httpClient.GetAsync("/");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }
}