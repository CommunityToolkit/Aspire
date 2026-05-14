using Aspire.Hosting;

namespace CommunityToolkit.Aspire.Hosting.Ngrok.Tests;

public class NgrokArgsTests
{
    [Fact]
    public async Task AddNgrokWithNoTunnelEndpointsPassesStartNoneAndConfigPath()
    {
        var builder = DistributedApplication.CreateBuilder();

        builder.AddNgrok("ngrok");

        using var app = builder.Build();

        var appModel = app.Services.GetRequiredService<DistributedApplicationModel>();
        var resource = Assert.Single(appModel.Resources.OfType<NgrokResource>());

        var argsAnnotation = Assert.Single(resource.Annotations.OfType<CommandLineArgsCallbackAnnotation>());
        var context = new CommandLineArgsCallbackContext([], resource, CancellationToken.None);
        await argsAnnotation.Callback(context);

        Assert.Equal(["start", "--none", "--config", "/var/tmp/ngrok/ngrok.yml"], context.Args);
    }

    [Fact]
    public async Task AddNgrokWithTunnelEndpointPassesStartAllAndConfigPath()
    {
        var builder = DistributedApplication.CreateBuilder();

        var api = builder.AddProject<Projects.CommunityToolkit_Aspire_Hosting_Ngrok_ApiService>("api");

        builder.AddNgrok("ngrok")
            .WithTunnelEndpoint(api, "http");

        using var app = builder.Build();

        var appModel = app.Services.GetRequiredService<DistributedApplicationModel>();
        var resource = Assert.Single(appModel.Resources.OfType<NgrokResource>());

        var argsAnnotation = Assert.Single(resource.Annotations.OfType<CommandLineArgsCallbackAnnotation>());
        var context = new CommandLineArgsCallbackContext([], resource, CancellationToken.None);
        await argsAnnotation.Callback(context);

        Assert.Equal(["start", "--all", "--config", "/var/tmp/ngrok/ngrok.yml"], context.Args);
    }

    [Fact]
    public async Task AddNgrokArgsUseResourceNameInConfigPath()
    {
        var builder = DistributedApplication.CreateBuilder();

        builder.AddNgrok("my-tunnel");

        using var app = builder.Build();

        var appModel = app.Services.GetRequiredService<DistributedApplicationModel>();
        var resource = Assert.Single(appModel.Resources.OfType<NgrokResource>());

        var argsAnnotation = Assert.Single(resource.Annotations.OfType<CommandLineArgsCallbackAnnotation>());
        var context = new CommandLineArgsCallbackContext([], resource, CancellationToken.None);
        await argsAnnotation.Callback(context);

        Assert.Contains("/var/tmp/ngrok/my-tunnel.yml", context.Args);
    }
}
