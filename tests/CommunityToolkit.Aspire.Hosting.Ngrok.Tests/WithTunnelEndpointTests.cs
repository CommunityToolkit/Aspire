using Aspire.Hosting;

namespace CommunityToolkit.Aspire.Hosting.Ngrok.Tests;

public class WithTunnelEndpointTests
{
    [Fact]
    public void WithTunnelEndpointSetsAnnotationEndpointName()
    {
        var builder = DistributedApplication.CreateBuilder();
        
        var api = builder
            .AddProject<Projects.CommunityToolkit_Aspire_Hosting_Ngrok_ApiService>("api");

        builder.AddNgrok("ngrok")
            .WithTunnelEndpoint(api,"http");

        using var app = builder.Build();

        var appModel = app.Services.GetRequiredService<DistributedApplicationModel>();
        var resource = Assert.Single(appModel.Resources.OfType<NgrokResource>());
        var annotation = Assert.Single(resource.Annotations.OfType<NgrokEndpointAnnotation>());
        var endpoint = Assert.Single(annotation.Endpoints);
        
        Assert.Equal("http", endpoint.EndpointName);
    }
    
    [Fact]
    public void WithTunnelEndpointSetsAnnotationUrlToNullByDefault()
    {
        var builder = DistributedApplication.CreateBuilder();
        
        var api = builder
            .AddProject<Projects.CommunityToolkit_Aspire_Hosting_Ngrok_ApiService>("api");

        builder.AddNgrok("ngrok")
            .WithTunnelEndpoint(api,"http");

        using var app = builder.Build();

        var appModel = app.Services.GetRequiredService<DistributedApplicationModel>();
        var resource = Assert.Single(appModel.Resources.OfType<NgrokResource>());
        var annotation = Assert.Single(resource.Annotations.OfType<NgrokEndpointAnnotation>());
        var endpoint = Assert.Single(annotation.Endpoints);
        
        Assert.Null(endpoint.Url);
    }
    
    [Fact]
    public void WithTunnelEndpointSetsAnnotationUrl()
    {
        var builder = DistributedApplication.CreateBuilder();
        
        var api = builder
            .AddProject<Projects.CommunityToolkit_Aspire_Hosting_Ngrok_ApiService>("api");

        builder.AddNgrok("ngrok")
            .WithTunnelEndpoint(api,"http", "custom-url");

        using var app = builder.Build();

        var appModel = app.Services.GetRequiredService<DistributedApplicationModel>();
        var resource = Assert.Single(appModel.Resources.OfType<NgrokResource>());
        var annotation = Assert.Single(resource.Annotations.OfType<NgrokEndpointAnnotation>());
        var endpoint = Assert.Single(annotation.Endpoints);
        
        Assert.Equal("custom-url", endpoint.Url);
    }

    [Fact]
    public void WithTunnelEndpointSetsLabelsToNullByDefault()
    {
        var builder = DistributedApplication.CreateBuilder();
        
        var api = builder
            .AddProject<Projects.CommunityToolkit_Aspire_Hosting_Ngrok_ApiService>("api");

        builder.AddNgrok("ngrok")
            .WithTunnelEndpoint(api,"http");

        using var app = builder.Build();

        var appModel = app.Services.GetRequiredService<DistributedApplicationModel>();
        var resource = Assert.Single(appModel.Resources.OfType<NgrokResource>());
        var annotation = Assert.Single(resource.Annotations.OfType<NgrokEndpointAnnotation>());
        var endpoint = Assert.Single(annotation.Endpoints);
        
        Assert.Null(endpoint.Labels);
    }

    [Fact]
    public void WithTunnelEndpointSetsLabels()
    {
        var builder = DistributedApplication.CreateBuilder();
        
        var api = builder
            .AddProject<Projects.CommunityToolkit_Aspire_Hosting_Ngrok_ApiService>("api");

        builder.AddNgrok("ngrok")
            .WithTunnelEndpoint(api,"http", "custom-url", new Dictionary<string, string>() { ["key"] = "value" });

        using var app = builder.Build();

        var appModel = app.Services.GetRequiredService<DistributedApplicationModel>();
        var resource = Assert.Single(appModel.Resources.OfType<NgrokResource>());
        var annotation = Assert.Single(resource.Annotations.OfType<NgrokEndpointAnnotation>());
        var endpoint = Assert.Single(annotation.Endpoints);
        
        Assert.Equal("key", endpoint.Labels!.Keys.First());
        Assert.Equal("value", endpoint.Labels!["key"]);
    }
    
    [Fact]
    public void WithTunnelNullResourceBuilderThrows()
    {
        var builder = DistributedApplication.CreateBuilder();
        var api = builder
            .AddProject<Projects.CommunityToolkit_Aspire_Hosting_Ngrok_ApiService>("api");
        
        IResourceBuilder<NgrokResource> resourceBuilder = null!;

        Assert.Throws<ArgumentNullException>(() => resourceBuilder.WithTunnelEndpoint(api, "http"));
    }
    
    [Fact]
    public void WithTunnelNullResourceThrows()
    {
        var builder = DistributedApplication.CreateBuilder();
        var ngrok = builder.AddNgrok("ngrok");
        
        IResourceBuilder<IResourceWithEndpoints> resource = null!;

        Assert.Throws<ArgumentNullException>(() => ngrok.WithTunnelEndpoint(resource, "http"));
    }
    
    [Fact]
    public void WithTunnelNullEndpointNameThrows()
    {
        var builder = DistributedApplication.CreateBuilder();
        var api = builder
            .AddProject<Projects.CommunityToolkit_Aspire_Hosting_Ngrok_ApiService>("api");
        var ngrok = builder.AddNgrok("ngrok");

        Assert.Throws<ArgumentNullException>(() => ngrok.WithTunnelEndpoint(api, null!));
    }
    
    [Fact]
    public void WithTunnelEmptyEndpointNameThrows()
    {
        var builder = DistributedApplication.CreateBuilder();
        var api = builder
            .AddProject<Projects.CommunityToolkit_Aspire_Hosting_Ngrok_ApiService>("api");
        var ngrok = builder.AddNgrok("ngrok");

        Assert.Throws<ArgumentException>(() => ngrok.WithTunnelEndpoint(api, ""));
    }
    
    [Fact]
    public void WithTunnelEmptyUrlThrows()
    {
        var builder = DistributedApplication.CreateBuilder();
        var api = builder
            .AddProject<Projects.CommunityToolkit_Aspire_Hosting_Ngrok_ApiService>("api");
        var ngrok = builder.AddNgrok("ngrok");

        Assert.Throws<ArgumentException>(() => ngrok.WithTunnelEndpoint(api, "http", ""));
    }
    
    [Fact]
    public void WithTunnelWhitespaceUrlThrows()
    {
        var builder = DistributedApplication.CreateBuilder();
        var api = builder
            .AddProject<Projects.CommunityToolkit_Aspire_Hosting_Ngrok_ApiService>("api");
        var ngrok = builder.AddNgrok("ngrok");

        Assert.Throws<ArgumentException>(() => ngrok.WithTunnelEndpoint(api, "http", "   "));
    }
}