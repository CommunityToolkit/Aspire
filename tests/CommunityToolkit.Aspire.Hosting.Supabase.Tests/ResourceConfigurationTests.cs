using Aspire.Hosting;
using Aspire.Hosting.ApplicationModel;
using CommunityToolkit.Aspire.Hosting.Supabase.Builders;
using CommunityToolkit.Aspire.Hosting.Supabase.Resources;

namespace CommunityToolkit.Aspire.Hosting.Supabase.Tests;

[Collection("Supabase")]
public class ResourceConfigurationTests
{
    [Fact]
    public void ConfigureAuthWithSiteUrl()
    {
        var builder = DistributedApplication.CreateBuilder();
        builder.AddSupabase("supabase")
            .ConfigureAuth(auth => auth.WithSiteUrl("http://myapp.local:5000"));

        using var app = builder.Build();

        var appModel = app.Services.GetRequiredService<DistributedApplicationModel>();

        var authResource = Assert.Single(appModel.Resources.OfType<SupabaseAuthResource>());
        Assert.Equal("http://myapp.local:5000", authResource.SiteUrl);
    }

    [Fact]
    public void ConfigureAuthWithAutoConfirmDisabled()
    {
        var builder = DistributedApplication.CreateBuilder();
        builder.AddSupabase("supabase")
            .ConfigureAuth(auth => auth.WithAutoConfirm(false));

        using var app = builder.Build();

        var appModel = app.Services.GetRequiredService<DistributedApplicationModel>();

        var authResource = Assert.Single(appModel.Resources.OfType<SupabaseAuthResource>());
        Assert.False(authResource.AutoConfirm);
    }

    [Fact]
    public void ConfigureAuthWithDisableSignup()
    {
        var builder = DistributedApplication.CreateBuilder();
        builder.AddSupabase("supabase")
            .ConfigureAuth(auth => auth.WithDisableSignup(true));

        using var app = builder.Build();

        var appModel = app.Services.GetRequiredService<DistributedApplicationModel>();

        var authResource = Assert.Single(appModel.Resources.OfType<SupabaseAuthResource>());
        Assert.True(authResource.DisableSignup);
    }

    [Fact]
    public void ConfigureAuthWithAnonymousUsersDisabled()
    {
        var builder = DistributedApplication.CreateBuilder();
        builder.AddSupabase("supabase")
            .ConfigureAuth(auth => auth.WithAnonymousUsers(false));

        using var app = builder.Build();

        var appModel = app.Services.GetRequiredService<DistributedApplicationModel>();

        var authResource = Assert.Single(appModel.Resources.OfType<SupabaseAuthResource>());
        Assert.False(authResource.AnonymousUsersEnabled);
    }

    [Fact]
    public void ConfigureAuthWithCustomJwtExpiration()
    {
        var builder = DistributedApplication.CreateBuilder();
        builder.AddSupabase("supabase")
            .ConfigureAuth(auth => auth.WithJwtExpiration(7200));

        using var app = builder.Build();

        var appModel = app.Services.GetRequiredService<DistributedApplicationModel>();

        var authResource = Assert.Single(appModel.Resources.OfType<SupabaseAuthResource>());
        Assert.Equal(7200, authResource.JwtExpiration);
    }

    [Fact]
    public void ConfigureAuthWithMultipleSettings()
    {
        var builder = DistributedApplication.CreateBuilder();
        builder.AddSupabase("supabase")
            .ConfigureAuth(auth => auth
                .WithSiteUrl("http://custom.local")
                .WithAutoConfirm(false)
                .WithDisableSignup(true)
                .WithAnonymousUsers(false)
                .WithJwtExpiration(1800));

        using var app = builder.Build();

        var appModel = app.Services.GetRequiredService<DistributedApplicationModel>();

        var authResource = Assert.Single(appModel.Resources.OfType<SupabaseAuthResource>());
        Assert.Equal("http://custom.local", authResource.SiteUrl);
        Assert.False(authResource.AutoConfirm);
        Assert.True(authResource.DisableSignup);
        Assert.False(authResource.AnonymousUsersEnabled);
        Assert.Equal(1800, authResource.JwtExpiration);
    }

    [Fact]
    public void ConfigureStorageWithFileSizeLimit()
    {
        var builder = DistributedApplication.CreateBuilder();
        builder.AddSupabase("supabase")
            .ConfigureStorage(storage => storage.WithFileSizeLimit(100_000_000));

        using var app = builder.Build();

        var appModel = app.Services.GetRequiredService<DistributedApplicationModel>();

        var storageResource = Assert.Single(appModel.Resources.OfType<SupabaseStorageResource>());
        Assert.Equal(100_000_000, storageResource.FileSizeLimit);
    }

    [Fact]
    public void ConfigureStorageWithBackend()
    {
        var builder = DistributedApplication.CreateBuilder();
        builder.AddSupabase("supabase")
            .ConfigureStorage(storage => storage.WithBackend("s3"));

        using var app = builder.Build();

        var appModel = app.Services.GetRequiredService<DistributedApplicationModel>();

        var storageResource = Assert.Single(appModel.Resources.OfType<SupabaseStorageResource>());
        Assert.Equal("s3", storageResource.Backend);
    }

    [Fact]
    public void ConfigureStorageWithImageTransformationEnabled()
    {
        var builder = DistributedApplication.CreateBuilder();
        builder.AddSupabase("supabase")
            .ConfigureStorage(storage => storage.WithImageTransformation(true));

        using var app = builder.Build();

        var appModel = app.Services.GetRequiredService<DistributedApplicationModel>();

        var storageResource = Assert.Single(appModel.Resources.OfType<SupabaseStorageResource>());
        Assert.True(storageResource.EnableImageTransformation);
    }

    [Fact]
    public void ConfigureStorageWithImageTransformationDisabled()
    {
        var builder = DistributedApplication.CreateBuilder();
        builder.AddSupabase("supabase")
            .ConfigureStorage(storage => storage.WithImageTransformation(false));

        using var app = builder.Build();

        var appModel = app.Services.GetRequiredService<DistributedApplicationModel>();

        var storageResource = Assert.Single(appModel.Resources.OfType<SupabaseStorageResource>());
        Assert.False(storageResource.EnableImageTransformation);
    }

    [Fact]
    public void ConfigureDatabaseWithCustomPassword()
    {
        var builder = DistributedApplication.CreateBuilder();
        builder.AddSupabase("supabase")
            .ConfigureDatabase(db => db.WithPassword("my-secure-password"));

        using var app = builder.Build();

        var appModel = app.Services.GetRequiredService<DistributedApplicationModel>();

        var dbResource = Assert.Single(appModel.Resources.OfType<SupabaseDatabaseResource>());
        Assert.Equal("my-secure-password", dbResource.Password);
    }

    [Fact]
    public void ConfigureDatabaseWithCustomPort()
    {
        var builder = DistributedApplication.CreateBuilder();
        builder.AddSupabase("supabase")
            .ConfigureDatabase(db => db.WithPort(5433));

        using var app = builder.Build();

        var appModel = app.Services.GetRequiredService<DistributedApplicationModel>();

        var dbResource = Assert.Single(appModel.Resources.OfType<SupabaseDatabaseResource>());
        Assert.Equal(5433, dbResource.ExternalPort);
    }

    [Fact]
    public void ConfigureMultipleComponents()
    {
        var builder = DistributedApplication.CreateBuilder();
        builder.AddSupabase("supabase")
            .ConfigureDatabase(db => db.WithPort(5433))
            .ConfigureAuth(auth => auth.WithSiteUrl("http://test.local"))
            .ConfigureStorage(storage => storage.WithFileSizeLimit(50_000_000));

        using var app = builder.Build();

        var appModel = app.Services.GetRequiredService<DistributedApplicationModel>();

        var dbResource = Assert.Single(appModel.Resources.OfType<SupabaseDatabaseResource>());
        var authResource = Assert.Single(appModel.Resources.OfType<SupabaseAuthResource>());
        var storageResource = Assert.Single(appModel.Resources.OfType<SupabaseStorageResource>());

        Assert.Equal(5433, dbResource.ExternalPort);
        Assert.Equal("http://test.local", authResource.SiteUrl);
        Assert.Equal(50_000_000, storageResource.FileSizeLimit);
    }

    [Fact]
    public void StackResourceProvidesApiUrl()
    {
        var builder = DistributedApplication.CreateBuilder();
        builder.AddSupabase("supabase");

        using var app = builder.Build();

        var appModel = app.Services.GetRequiredService<DistributedApplicationModel>();

        var stackResource = Assert.Single(appModel.Resources.OfType<SupabaseStackResource>());

        var apiUrl = stackResource.GetApiUrl();
        Assert.Equal("http://localhost:8000", apiUrl);
    }

    [Fact]
    public void StackResourceProvidesStudioUrl()
    {
        var builder = DistributedApplication.CreateBuilder();
        builder.AddSupabase("supabase");

        using var app = builder.Build();

        var appModel = app.Services.GetRequiredService<DistributedApplicationModel>();

        var stackResource = Assert.Single(appModel.Resources.OfType<SupabaseStackResource>());

        var studioUrl = stackResource.GetStudioUrl();
        Assert.Equal("http://localhost:54323", studioUrl);
    }

    [Fact]
    public void StackResourceProvidesPostgresConnectionString()
    {
        var builder = DistributedApplication.CreateBuilder();
        builder.AddSupabase("supabase");

        using var app = builder.Build();

        var appModel = app.Services.GetRequiredService<DistributedApplicationModel>();

        var stackResource = Assert.Single(appModel.Resources.OfType<SupabaseStackResource>());

        var connectionString = stackResource.GetPostgresConnectionString();
        Assert.Contains("Host=localhost", connectionString);
        Assert.Contains("Port=54322", connectionString);
        Assert.Contains("Database=postgres", connectionString);
        Assert.Contains("Username=postgres", connectionString);
    }

    [Fact]
    public void ConnectionStringExpressionReturnsKongUrl()
    {
        var builder = DistributedApplication.CreateBuilder();
        builder.AddSupabase("supabase");

        using var app = builder.Build();

        var appModel = app.Services.GetRequiredService<DistributedApplicationModel>();

        var stackResource = Assert.Single(appModel.Resources.OfType<SupabaseStackResource>());

        var expression = stackResource.ConnectionStringExpression;
        Assert.NotNull(expression);
    }

    [Fact]
    public void DatabaseResourceReferencesParentStack()
    {
        var builder = DistributedApplication.CreateBuilder();
        builder.AddSupabase("supabase");

        using var app = builder.Build();

        var appModel = app.Services.GetRequiredService<DistributedApplicationModel>();

        var stackResource = Assert.Single(appModel.Resources.OfType<SupabaseStackResource>());
        var dbResource = Assert.Single(appModel.Resources.OfType<SupabaseDatabaseResource>());

        Assert.NotNull(dbResource);
        Assert.Equal(stackResource, dbResource.Annotations.OfType<ResourceRelationshipAnnotation>()
            .FirstOrDefault(a => a.Type == "Parent")?.Resource);
    }

    [Fact]
    public void AuthResourceReferencesParentStack()
    {
        var builder = DistributedApplication.CreateBuilder();
        builder.AddSupabase("supabase");

        using var app = builder.Build();

        var appModel = app.Services.GetRequiredService<DistributedApplicationModel>();

        var stackResource = Assert.Single(appModel.Resources.OfType<SupabaseStackResource>());
        var authResource = Assert.Single(appModel.Resources.OfType<SupabaseAuthResource>());

        Assert.NotNull(authResource);
        Assert.Equal(stackResource, authResource.Annotations.OfType<ResourceRelationshipAnnotation>()
            .FirstOrDefault(a => a.Type == "Parent")?.Resource);
    }

    [Fact]
    public void StorageResourceHasDefaultFileSizeLimit()
    {
        var builder = DistributedApplication.CreateBuilder();
        builder.AddSupabase("supabase");

        using var app = builder.Build();

        var appModel = app.Services.GetRequiredService<DistributedApplicationModel>();

        var storageResource = Assert.Single(appModel.Resources.OfType<SupabaseStorageResource>());
        Assert.True(storageResource.FileSizeLimit > 0);
    }

    [Fact]
    public void StorageResourceHasDefaultBackend()
    {
        var builder = DistributedApplication.CreateBuilder();
        builder.AddSupabase("supabase");

        using var app = builder.Build();

        var appModel = app.Services.GetRequiredService<DistributedApplicationModel>();

        var storageResource = Assert.Single(appModel.Resources.OfType<SupabaseStorageResource>());
        Assert.NotEmpty(storageResource.Backend);
    }

    [Fact]
    public void RestResourceHasDefaultSchemas()
    {
        var builder = DistributedApplication.CreateBuilder();
        builder.AddSupabase("supabase");

        using var app = builder.Build();

        var appModel = app.Services.GetRequiredService<DistributedApplicationModel>();

        var restResource = Assert.Single(appModel.Resources.OfType<SupabaseRestResource>());
        Assert.NotEmpty(restResource.Schemas);
        Assert.Contains("public", restResource.Schemas);
    }

    [Fact]
    public void RestResourceHasDefaultAnonRole()
    {
        var builder = DistributedApplication.CreateBuilder();
        builder.AddSupabase("supabase");

        using var app = builder.Build();

        var appModel = app.Services.GetRequiredService<DistributedApplicationModel>();

        var restResource = Assert.Single(appModel.Resources.OfType<SupabaseRestResource>());
        Assert.Equal("anon", restResource.AnonRole);
    }
}
