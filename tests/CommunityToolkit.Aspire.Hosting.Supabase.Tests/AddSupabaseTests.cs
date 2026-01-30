using Aspire.Hosting;
using Aspire.Hosting.ApplicationModel;
using CommunityToolkit.Aspire.Hosting.Supabase.Builders;
using CommunityToolkit.Aspire.Hosting.Supabase.Resources;

namespace CommunityToolkit.Aspire.Hosting.Supabase.Tests;

[Collection("Supabase")]
public class AddSupabaseTests
{
    [Fact]
    public void AddSupabaseCreatesStackResource()
    {
        var builder = DistributedApplication.CreateBuilder();
        builder.AddSupabase("supabase");

        using var app = builder.Build();

        var appModel = app.Services.GetRequiredService<DistributedApplicationModel>();

        var stackResource = Assert.Single(appModel.Resources.OfType<SupabaseStackResource>());
        Assert.Equal("supabase", stackResource.Name);
    }

    [Fact]
    public void AddSupabaseCreatesAllRequiredContainers()
    {
        var builder = DistributedApplication.CreateBuilder();
        builder.AddSupabase("supabase");

        using var app = builder.Build();

        var appModel = app.Services.GetRequiredService<DistributedApplicationModel>();

        // Verify all expected containers are created
        Assert.Single(appModel.Resources.OfType<SupabaseStackResource>());
        Assert.Single(appModel.Resources.OfType<SupabaseDatabaseResource>());
        Assert.Single(appModel.Resources.OfType<SupabaseAuthResource>());
        Assert.Single(appModel.Resources.OfType<SupabaseRestResource>());
        Assert.Single(appModel.Resources.OfType<SupabaseStorageResource>());
        Assert.Single(appModel.Resources.OfType<SupabaseKongResource>());
        Assert.Single(appModel.Resources.OfType<SupabaseMetaResource>());
    }

    [Fact]
    public void AddSupabaseWithCustomNameUsesCorrectPrefix()
    {
        var builder = DistributedApplication.CreateBuilder();
        builder.AddSupabase("my-supabase");

        using var app = builder.Build();

        var appModel = app.Services.GetRequiredService<DistributedApplicationModel>();

        var stackResource = Assert.Single(appModel.Resources.OfType<SupabaseStackResource>());
        Assert.Equal("my-supabase", stackResource.Name);

        var dbResource = Assert.Single(appModel.Resources.OfType<SupabaseDatabaseResource>());
        Assert.Equal("my-supabase-db", dbResource.Name);

        var authResource = Assert.Single(appModel.Resources.OfType<SupabaseAuthResource>());
        Assert.Equal("my-supabase-auth", authResource.Name);
    }

    [Fact]
    public void StackResourceHasDefaultJwtConfiguration()
    {
        var builder = DistributedApplication.CreateBuilder();
        builder.AddSupabase("supabase");

        using var app = builder.Build();

        var appModel = app.Services.GetRequiredService<DistributedApplicationModel>();

        var stackResource = Assert.Single(appModel.Resources.OfType<SupabaseStackResource>());

        Assert.NotEmpty(stackResource.JwtSecret);
        Assert.NotEmpty(stackResource.AnonKey);
        Assert.NotEmpty(stackResource.ServiceRoleKey);
    }

    [Fact]
    public void StackResourceReferencesAllSubResources()
    {
        var builder = DistributedApplication.CreateBuilder();
        builder.AddSupabase("supabase");

        using var app = builder.Build();

        var appModel = app.Services.GetRequiredService<DistributedApplicationModel>();

        var stackResource = Assert.Single(appModel.Resources.OfType<SupabaseStackResource>());

        Assert.NotNull(stackResource.Database);
        Assert.NotNull(stackResource.Auth);
        Assert.NotNull(stackResource.Rest);
        Assert.NotNull(stackResource.Storage);
        Assert.NotNull(stackResource.Kong);
        Assert.NotNull(stackResource.Meta);
    }

    [Fact]
    public void DatabaseResourceHasDefaultConfiguration()
    {
        var builder = DistributedApplication.CreateBuilder();
        builder.AddSupabase("supabase");

        using var app = builder.Build();

        var appModel = app.Services.GetRequiredService<DistributedApplicationModel>();

        var dbResource = Assert.Single(appModel.Resources.OfType<SupabaseDatabaseResource>());

        Assert.Equal("postgres-insecure-dev-password", dbResource.Password);
        Assert.Equal(54322, dbResource.ExternalPort);
    }

    [Fact]
    public void AuthResourceHasDefaultConfiguration()
    {
        var builder = DistributedApplication.CreateBuilder();
        builder.AddSupabase("supabase");

        using var app = builder.Build();

        var appModel = app.Services.GetRequiredService<DistributedApplicationModel>();

        var authResource = Assert.Single(appModel.Resources.OfType<SupabaseAuthResource>());

        Assert.True(authResource.AutoConfirm);
        Assert.False(authResource.DisableSignup);
        Assert.True(authResource.AnonymousUsersEnabled);
        Assert.Equal(3600, authResource.JwtExpiration);
    }

    [Fact]
    public void KongResourceHasDefaultPort()
    {
        var builder = DistributedApplication.CreateBuilder();
        builder.AddSupabase("supabase");

        using var app = builder.Build();

        var appModel = app.Services.GetRequiredService<DistributedApplicationModel>();

        var kongResource = Assert.Single(appModel.Resources.OfType<SupabaseKongResource>());

        Assert.Equal(8000, kongResource.ExternalPort);
    }

    [Fact]
    public void ResourceNameCannotBeNull()
    {
        var builder = DistributedApplication.CreateBuilder();

        Assert.Throws<ArgumentNullException>(() => builder.AddSupabase(null!));
    }

    [Fact]
    public void ResourceNameCannotBeEmpty()
    {
        var builder = DistributedApplication.CreateBuilder();

        Assert.Throws<ArgumentException>(() => builder.AddSupabase(""));
    }

    [Fact]
    public void ResourceNameCannotBeWhitespace()
    {
        var builder = DistributedApplication.CreateBuilder();

        Assert.Throws<ArgumentException>(() => builder.AddSupabase("   "));
    }

    [Fact]
    public void StackResourceIsContainerResource()
    {
        var builder = DistributedApplication.CreateBuilder();
        builder.AddSupabase("supabase");

        using var app = builder.Build();

        var appModel = app.Services.GetRequiredService<DistributedApplicationModel>();

        var stackResource = Assert.Single(appModel.Resources.OfType<SupabaseStackResource>());

        Assert.IsAssignableFrom<ContainerResource>(stackResource);
    }

    [Fact]
    public void StackResourceImplementsIResourceWithConnectionString()
    {
        var builder = DistributedApplication.CreateBuilder();
        builder.AddSupabase("supabase");

        using var app = builder.Build();

        var appModel = app.Services.GetRequiredService<DistributedApplicationModel>();

        var stackResource = Assert.Single(appModel.Resources.OfType<SupabaseStackResource>());

        Assert.IsAssignableFrom<IResourceWithConnectionString>(stackResource);
    }

    [Fact]
    public void AllContainersHaveContainerImageAnnotation()
    {
        var builder = DistributedApplication.CreateBuilder();
        builder.AddSupabase("supabase");

        using var app = builder.Build();

        var appModel = app.Services.GetRequiredService<DistributedApplicationModel>();

        var containerResources = appModel.Resources.OfType<ContainerResource>();

        foreach (var container in containerResources)
        {
            Assert.True(container.TryGetAnnotationsOfType<ContainerImageAnnotation>(out var annotations),
                $"Container {container.Name} should have ContainerImageAnnotation");
            Assert.NotEmpty(annotations);
        }
    }

    [Fact]
    public void DatabaseContainerHasCorrectImage()
    {
        var builder = DistributedApplication.CreateBuilder();
        builder.AddSupabase("supabase");

        using var app = builder.Build();

        var appModel = app.Services.GetRequiredService<DistributedApplicationModel>();

        var dbResource = Assert.Single(appModel.Resources.OfType<SupabaseDatabaseResource>());

        Assert.True(dbResource.TryGetAnnotationsOfType<ContainerImageAnnotation>(out var annotations));
        var imageAnnotation = Assert.Single(annotations);
        Assert.Equal("supabase/postgres", imageAnnotation.Image);
    }

    [Fact]
    public void AuthContainerHasCorrectImage()
    {
        var builder = DistributedApplication.CreateBuilder();
        builder.AddSupabase("supabase");

        using var app = builder.Build();

        var appModel = app.Services.GetRequiredService<DistributedApplicationModel>();

        var authResource = Assert.Single(appModel.Resources.OfType<SupabaseAuthResource>());

        Assert.True(authResource.TryGetAnnotationsOfType<ContainerImageAnnotation>(out var annotations));
        var imageAnnotation = Assert.Single(annotations);
        Assert.Equal("supabase/gotrue", imageAnnotation.Image);
    }

    [Fact]
    public void RestContainerHasCorrectImage()
    {
        var builder = DistributedApplication.CreateBuilder();
        builder.AddSupabase("supabase");

        using var app = builder.Build();

        var appModel = app.Services.GetRequiredService<DistributedApplicationModel>();

        var restResource = Assert.Single(appModel.Resources.OfType<SupabaseRestResource>());

        Assert.True(restResource.TryGetAnnotationsOfType<ContainerImageAnnotation>(out var annotations));
        var imageAnnotation = Assert.Single(annotations);
        Assert.Equal("postgrest/postgrest", imageAnnotation.Image);
    }

    [Fact]
    public void StorageContainerHasCorrectImage()
    {
        var builder = DistributedApplication.CreateBuilder();
        builder.AddSupabase("supabase");

        using var app = builder.Build();

        var appModel = app.Services.GetRequiredService<DistributedApplicationModel>();

        var storageResource = Assert.Single(appModel.Resources.OfType<SupabaseStorageResource>());

        Assert.True(storageResource.TryGetAnnotationsOfType<ContainerImageAnnotation>(out var annotations));
        var imageAnnotation = Assert.Single(annotations);
        Assert.Equal("supabase/storage-api", imageAnnotation.Image);
    }

    [Fact]
    public void KongContainerHasCorrectImage()
    {
        var builder = DistributedApplication.CreateBuilder();
        builder.AddSupabase("supabase");

        using var app = builder.Build();

        var appModel = app.Services.GetRequiredService<DistributedApplicationModel>();

        var kongResource = Assert.Single(appModel.Resources.OfType<SupabaseKongResource>());

        Assert.True(kongResource.TryGetAnnotationsOfType<ContainerImageAnnotation>(out var annotations));
        var imageAnnotation = Assert.Single(annotations);
        Assert.Equal("kong", imageAnnotation.Image);
    }

    [Fact]
    public void DatabaseResourceHasEndpoint()
    {
        var builder = DistributedApplication.CreateBuilder();
        builder.AddSupabase("supabase");

        using var app = builder.Build();

        var appModel = app.Services.GetRequiredService<DistributedApplicationModel>();

        var dbResource = Assert.Single(appModel.Resources.OfType<SupabaseDatabaseResource>());

        Assert.True(dbResource.TryGetAnnotationsOfType<EndpointAnnotation>(out var endpoints));
        Assert.NotEmpty(endpoints);
    }

    [Fact]
    public void KongResourceHasHttpEndpoint()
    {
        var builder = DistributedApplication.CreateBuilder();
        builder.AddSupabase("supabase");

        using var app = builder.Build();

        var appModel = app.Services.GetRequiredService<DistributedApplicationModel>();

        var kongResource = Assert.Single(appModel.Resources.OfType<SupabaseKongResource>());

        Assert.True(kongResource.TryGetAnnotationsOfType<EndpointAnnotation>(out var endpoints));
        var httpEndpoint = endpoints.FirstOrDefault(e => e.Name == "http");
        Assert.NotNull(httpEndpoint);
        Assert.Equal(8000, httpEndpoint.Port);
    }
}
