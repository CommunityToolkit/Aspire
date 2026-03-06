using Aspire.Hosting;
using Aspire.Hosting.ApplicationModel;
using Aspire.Hosting.Utils;

namespace CommunityToolkit.Aspire.Hosting.Keycloak.Extensions.Tests;

public class KeycloakNeonExtensionTests
{
    [Fact]
    public void WithNeonDatabase_Should_Throw_If_Builder_Is_Null()
    {
        IResourceBuilder<KeycloakResource>? builder = null;

        using var appBuilder = TestDistributedApplicationBuilder.Create();
        var apiKey = appBuilder.AddParameter("neon-api-key", "test", secret: true);
        var neon = appBuilder.AddNeon("neon", apiKey);
        var db = neon.AddDatabase("appdb");

        Assert.Throws<ArgumentNullException>(() => builder!.WithNeonDatabase(db));
    }

    [Fact]
    public void WithNeonDatabase_Should_Throw_If_Database_Is_Null()
    {
        using var builder = TestDistributedApplicationBuilder.Create();
        var kc = builder.AddKeycloak("kc");

        Assert.Throws<ArgumentNullException>(() => kc.WithNeonDatabase(null!));
    }

    [Fact]
    public async Task WithNeonDatabase_Sets_Static_Env_Vars()
    {
        using var builder = TestDistributedApplicationBuilder.Create();
        var apiKey = builder.AddParameter("neon-api-key", "test", secret: true);
        var neon = builder.AddNeon("neon", apiKey);
        var db = neon.AddDatabase("appdb");

        var kc = builder.AddKeycloak("kc")
            .WithNeonDatabase(db);

        var env = await GetEnvironmentVariablesAsync(kc);

        Assert.Equal("postgres", env["KC_DB"]);
        Assert.Equal("false", env["KC_TRANSACTION_XA_ENABLED"]);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task WithNeonDatabase_XA_Flag_Set_Correctly(bool xaEnabled)
    {
        using var builder = TestDistributedApplicationBuilder.Create();
        var apiKey = builder.AddParameter("neon-api-key", "test", secret: true);
        var neon = builder.AddNeon("neon", apiKey);
        var db = neon.AddDatabase("appdb");

        var kc = builder.AddKeycloak("kc")
            .WithNeonDatabase(db, xaEnabled: xaEnabled);

        var env = await GetEnvironmentVariablesAsync(kc);

        Assert.Equal(xaEnabled.ToString().ToLowerInvariant(), env["KC_TRANSACTION_XA_ENABLED"]);
    }

    [Fact]
    public async Task WithNeonDatabase_Sets_Schema_When_Provided()
    {
        using var builder = TestDistributedApplicationBuilder.Create();
        var apiKey = builder.AddParameter("neon-api-key", "test", secret: true);
        var neon = builder.AddNeon("neon", apiKey);
        var db = neon.AddDatabase("appdb");

        var kc = builder.AddKeycloak("kc")
            .WithNeonDatabase(db, schema: "keycloak");

        var env = await GetEnvironmentVariablesAsync(kc);

        Assert.Equal("keycloak", env["KC_DB_SCHEMA"]);
    }

    [Fact]
    public async Task WithNeonDatabase_Does_Not_Set_Schema_When_Null()
    {
        using var builder = TestDistributedApplicationBuilder.Create();
        var apiKey = builder.AddParameter("neon-api-key", "test", secret: true);
        var neon = builder.AddNeon("neon", apiKey);
        var db = neon.AddDatabase("appdb");

        var kc = builder.AddKeycloak("kc")
            .WithNeonDatabase(db);

        var env = await GetEnvironmentVariablesAsync(kc);

        Assert.False(env.ContainsKey("KC_DB_SCHEMA"));
    }

    [Fact]
    public void WithNeonDatabase_Sets_Entrypoint_To_Shell()
    {
        using var builder = TestDistributedApplicationBuilder.Create();
        var apiKey = builder.AddParameter("neon-api-key", "test", secret: true);
        var neon = builder.AddNeon("neon", apiKey);
        var db = neon.AddDatabase("appdb");

        var kc = builder.AddKeycloak("kc")
            .WithNeonDatabase(db);

        Assert.Equal("/bin/sh", kc.Resource.Entrypoint);
    }

    [Fact]
    public void WithNeonDatabase_Adds_BindMount_In_RunMode()
    {
        using var builder = TestDistributedApplicationBuilder.Create();
        var apiKey = builder.AddParameter("neon-api-key", "test", secret: true);
        var neon = builder.AddNeon("neon", apiKey);
        var db = neon.AddDatabase("appdb");

        var kc = builder.AddKeycloak("kc")
            .WithNeonDatabase(db);

        Assert.True(kc.Resource.TryGetAnnotationsOfType<ContainerMountAnnotation>(out var annotations));
        var bindMount = annotations.FirstOrDefault(a => a.Target == "/neon-output");

        Assert.NotNull(bindMount);
        Assert.Equal(ContainerMountType.BindMount, bindMount.Type);
        Assert.True(bindMount.IsReadOnly);
        Assert.Equal(neon.Resource.HostOutputDirectory, bindMount.Source);
    }

    [Fact]
    public void WithNeonDatabase_BindMount_Source_Matches_Provisioner_Output_Directory()
    {
        using var builder = TestDistributedApplicationBuilder.Create();
        var apiKey = builder.AddParameter("neon-api-key", "test", secret: true);
        var neon = builder.AddNeon("neon", apiKey);
        var db = neon.AddDatabase("appdb");

        // Verify HostOutputDirectory is populated in run mode
        Assert.NotNull(neon.Resource.HostOutputDirectory);
        Assert.True(Directory.Exists(Path.GetDirectoryName(neon.Resource.HostOutputDirectory))
            || neon.Resource.HostOutputDirectory.Length > 0);

        var kc = builder.AddKeycloak("kc")
            .WithNeonDatabase(db);

        Assert.True(kc.Resource.TryGetAnnotationsOfType<ContainerMountAnnotation>(out var annotations));
        var bindMount = annotations.First(a => a.Target == "/neon-output");

        Assert.Equal(neon.Resource.HostOutputDirectory, bindMount.Source);
    }

    [Fact]
    public void WithNeonDatabase_Has_CommandLineArgs_Callback()
    {
        using var builder = TestDistributedApplicationBuilder.Create();
        var apiKey = builder.AddParameter("neon-api-key", "test", secret: true);
        var neon = builder.AddNeon("neon", apiKey);
        var db = neon.AddDatabase("appdb");

        var kc = builder.AddKeycloak("kc")
            .WithNeonDatabase(db);

        Assert.True(kc.Resource.TryGetAnnotationsOfType<CommandLineArgsCallbackAnnotation>(out var annotations));
        Assert.NotEmpty(annotations);
    }

    [Fact]
    public void WithNeonDatabase_Returns_Same_Builder()
    {
        using var builder = TestDistributedApplicationBuilder.Create();
        var apiKey = builder.AddParameter("neon-api-key", "test", secret: true);
        var neon = builder.AddNeon("neon", apiKey);
        var db = neon.AddDatabase("appdb");

        var kc = builder.AddKeycloak("kc");
        var result = kc.WithNeonDatabase(db);

        Assert.Same(kc, result);
    }

    [Fact]
    public void WithNeonDatabase_PublishMode_UsesNamedVolume()
    {
        using var builder = TestDistributedApplicationBuilder.Create(DistributedApplicationOperation.Publish);
        var apiKey = builder.AddParameter("neon-api-key", "test", secret: true);
        var neon = builder.AddNeon("neon", apiKey);
        var db = neon.AddDatabase("appdb");

        var kc = builder.AddKeycloak("kc")
            .WithNeonDatabase(db);

        Assert.True(kc.Resource.TryGetAnnotationsOfType<ContainerMountAnnotation>(out var annotations));
        var volumeMount = annotations.FirstOrDefault(a => a.Target == "/neon-output");

        Assert.NotNull(volumeMount);
        Assert.Equal(ContainerMountType.Volume, volumeMount!.Type);
        Assert.Equal(neon.Resource.OutputVolumeName, volumeMount.Source);
        Assert.True(volumeMount.IsReadOnly);
    }

    [Fact]
    public void WithNeonDatabase_PublishMode_DoesNotUseBindMount()
    {
        using var builder = TestDistributedApplicationBuilder.Create(DistributedApplicationOperation.Publish);
        var apiKey = builder.AddParameter("neon-api-key", "test", secret: true);
        var neon = builder.AddNeon("neon", apiKey);
        var db = neon.AddDatabase("appdb");

        var kc = builder.AddKeycloak("kc")
            .WithNeonDatabase(db);

        Assert.True(kc.Resource.TryGetAnnotationsOfType<ContainerMountAnnotation>(out var annotations));
        var neonMounts = annotations.Where(a => a.Target == "/neon-output").ToList();

        Assert.DoesNotContain(neonMounts, m => m.Type == ContainerMountType.BindMount);
    }

    [Fact]
    public void WithNeonDatabase_PublishMode_SetsOutputVolumeName()
    {
        using var builder = TestDistributedApplicationBuilder.Create(DistributedApplicationOperation.Publish);
        var apiKey = builder.AddParameter("neon-api-key", "test", secret: true);
        var neon = builder.AddNeon("neon", apiKey);
        var db = neon.AddDatabase("appdb");

        Assert.NotNull(neon.Resource.OutputVolumeName);
        Assert.Null(neon.Resource.HostOutputDirectory);
    }

    private static async Task<Dictionary<string, object>> GetEnvironmentVariablesAsync(
        IResourceBuilder<KeycloakResource> keycloak)
    {
        Assert.True(keycloak.Resource.TryGetAnnotationsOfType<EnvironmentCallbackAnnotation>(out var annotations));

        var context = new EnvironmentCallbackContext(
            new DistributedApplicationExecutionContext(
                new DistributedApplicationExecutionContextOptions(DistributedApplicationOperation.Run)));

        foreach (var annotation in annotations)
        {
            await annotation.Callback(context);
        }

        return context.EnvironmentVariables.ToDictionary(
            kvp => kvp.Key,
            kvp => kvp.Value);
    }
}
