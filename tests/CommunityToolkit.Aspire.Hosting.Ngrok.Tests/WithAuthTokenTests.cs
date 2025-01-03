using Aspire.Hosting;

namespace CommunityToolkit.Aspire.Hosting.Ngrok.Tests;

public class WithAuthTokenTests
{
    [Fact]
    public void WithAuthTokenStringSetsEnvironmentVariable()
    {
        var builder = DistributedApplication.CreateBuilder();
        
        builder.AddNgrok("ngrok")
            .WithAuthToken("your-ngrok-auth-token");

        using var app = builder.Build();

        var appModel = app.Services.GetRequiredService<DistributedApplicationModel>();
        var resource = Assert.Single(appModel.Resources.OfType<NgrokResource>());
        var environment = Assert.Single(resource.Annotations.OfType<EnvironmentCallbackAnnotation>());
        
        var context = new EnvironmentCallbackContext(new DistributedApplicationExecutionContext(new DistributedApplicationExecutionContextOptions(DistributedApplicationOperation.Run)));
        environment.Callback(context);
        
        Assert.Equal("your-ngrok-auth-token", context.EnvironmentVariables["NGROK_AUTHTOKEN"]);
    }
    
    [Fact]
    public void WithAuthTokenStringParameterEnvironmentVariable()
    {
        var builder = DistributedApplication.CreateBuilder();
        
        var parameter = builder
            .AddParameter("ngrok-authtoken", "your-ngrok-auth-token", secret: true);
        
        builder.AddNgrok("ngrok")
            .WithAuthToken(parameter);

        using var app = builder.Build();

        var appModel = app.Services.GetRequiredService<DistributedApplicationModel>();
        var resource = Assert.Single(appModel.Resources.OfType<NgrokResource>());
        var environment = Assert.Single(resource.Annotations.OfType<EnvironmentCallbackAnnotation>());
        
        var context = new EnvironmentCallbackContext(new DistributedApplicationExecutionContext(new DistributedApplicationExecutionContextOptions(DistributedApplicationOperation.Run)));
        environment.Callback(context);
        
        Assert.Equal("your-ngrok-auth-token", ((ParameterResource)context.EnvironmentVariables["NGROK_AUTHTOKEN"]).Value);
    }
    
    [Fact]
    public void WithAuthTokenNullResourceBuilderThrows()
    {
        IResourceBuilder<NgrokResource> resourceBuilder = null!;

        Assert.Throws<ArgumentNullException>(() => resourceBuilder.WithAuthToken("your-ngrok-auth-token"));
    }
    
    [Fact]
    public void WithAuthTokenNullStringThrows()
    {
        var builder = DistributedApplication.CreateBuilder();
        var ngrok = builder.AddNgrok("ngrok");

        Assert.Throws<ArgumentNullException>(() => ngrok.WithAuthToken((string)null!));
    }
    
    [Fact]
    public void WithAuthTokenEmptyStringThrows()
    {
        var builder = DistributedApplication.CreateBuilder();
        var ngrok = builder.AddNgrok("ngrok");

        Assert.Throws<ArgumentException>(() => ngrok.WithAuthToken(""));
    }
    
    [Fact]
    public void WithAuthTokenWhitespaceStringThrows()
    {
        var builder = DistributedApplication.CreateBuilder();
        var ngrok = builder.AddNgrok("ngrok");

        Assert.Throws<ArgumentException>(() => ngrok.WithAuthToken("  "));
    }
    
    [Fact]
    public void WithAuthTokenNullParameterThrows()
    {
        var builder = DistributedApplication.CreateBuilder();
        var ngrok = builder.AddNgrok("ngrok");

        Assert.Throws<ArgumentNullException>(() => ngrok.WithAuthToken((IResourceBuilder<ParameterResource>)null!));
    }
}