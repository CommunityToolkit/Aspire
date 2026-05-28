using Aspire.Components.Common.Tests;
using Aspire.Hosting;
using Aspire.Hosting.Utils;
using CommunityToolkit.Aspire.Testing;

namespace CommunityToolkit.Aspire.Hosting.RustFs.Tests;

[RequiresDocker]
public class RustFsFunctionalTests(ITestOutputHelper testOutputHelper)
{
    [Fact]
    public async Task AddBucketCreatesBucketViaHttpApi()
    {
        using var distributedApplicationBuilder = TestDistributedApplicationBuilder.Create(testOutputHelper);

        var accessKeyParameter = ParameterResourceBuilderExtensions.CreateDefaultPasswordParameter(distributedApplicationBuilder,
            "accessKey");
        distributedApplicationBuilder.Configuration["Parameters:accessKey"] = await accessKeyParameter.GetValueAsync(default);
        var accessKey = distributedApplicationBuilder.AddParameter(accessKeyParameter.Name);

        var secretKeyParameter = ParameterResourceBuilderExtensions.CreateDefaultPasswordParameter(distributedApplicationBuilder,
            "secretKey");
        distributedApplicationBuilder.Configuration["Parameters:secretKey"] = await secretKeyParameter.GetValueAsync(default);
        var secretKey = distributedApplicationBuilder.AddParameter(secretKeyParameter.Name);

        var rustfs = distributedApplicationBuilder.AddRustFs("rustfs", accessKey, secretKey);
        var bucket = rustfs.AddBucket("functional-bucket");

        await using var app = await distributedApplicationBuilder.BuildAsync();
        await app.StartAsync();

        var rns = app.Services.GetRequiredService<ResourceNotificationService>();

        await rns.WaitForResourceHealthyAsync(rustfs.Resource.Name);
        var snapshot = await rns.WaitForResourceAsync(
            bucket.Resource.Name,
            evt => evt.Snapshot.State?.Style == KnownResourceStateStyles.Success
                || evt.Snapshot.State?.Style == KnownResourceStateStyles.Error)
            .WaitAsync(TimeSpan.FromMinutes(2));

        testOutputHelper.WriteLine($"Bucket final state text={snapshot.Snapshot.State?.Text} style={snapshot.Snapshot.State?.Style}");
        Assert.Equal(KnownResourceStates.Running, snapshot.Snapshot.State?.Text);
    }

    [Fact]
    public async Task ResourceStartsAndHealthCheckPasses()
    {
        using var distributedApplicationBuilder = TestDistributedApplicationBuilder.Create(testOutputHelper);

        var accessKeyParameter = ParameterResourceBuilderExtensions.CreateDefaultPasswordParameter(distributedApplicationBuilder,
            "accessKey");
        distributedApplicationBuilder.Configuration["Parameters:accessKey"] = await accessKeyParameter.GetValueAsync(default);
        var accessKey = distributedApplicationBuilder.AddParameter(accessKeyParameter.Name);

        var secretKeyParameter = ParameterResourceBuilderExtensions.CreateDefaultPasswordParameter(distributedApplicationBuilder,
            "secretKey");
        distributedApplicationBuilder.Configuration["Parameters:secretKey"] = await secretKeyParameter.GetValueAsync(default);
        var secretKey = distributedApplicationBuilder.AddParameter(secretKeyParameter.Name);

        var rustfs = distributedApplicationBuilder.AddRustFs("rustfs", accessKey, secretKey);

        await using var app = await distributedApplicationBuilder.BuildAsync();

        await app.StartAsync();

        var rns = app.Services.GetRequiredService<ResourceNotificationService>();

        await rns.WaitForResourceHealthyAsync(rustfs.Resource.Name);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task WithDataShouldPersistStateBetweenUsages(bool useVolume)
    {
        string? volumeName = null;
        string? bindMountPath = null;

        try
        {
            using var builder1 = TestDistributedApplicationBuilder.Create(testOutputHelper);

            var accessKeyParameter = ParameterResourceBuilderExtensions.CreateDefaultPasswordParameter(builder1,
                "accessKey");
            builder1.Configuration["Parameters:accessKey"] = await accessKeyParameter.GetValueAsync(default);
            var accessKey1 = builder1.AddParameter(accessKeyParameter.Name);

            var secretKeyParameter = ParameterResourceBuilderExtensions.CreateDefaultPasswordParameter(builder1,
                "secretKey");
            builder1.Configuration["Parameters:secretKey"] = await secretKeyParameter.GetValueAsync(default);
            var secretKey1 = builder1.AddParameter(secretKeyParameter.Name);

            var rustfs1 = builder1.AddRustFs("rustfs", accessKey1, secretKey1);

            if (useVolume)
            {
                volumeName = VolumeNameGenerator.Generate(rustfs1, nameof(WithDataShouldPersistStateBetweenUsages));

                DockerUtils.AttemptDeleteDockerVolume(volumeName, throwOnFailure: true);
                rustfs1.WithDataVolume(volumeName);
            }
            else
            {
                bindMountPath = Directory.CreateTempSubdirectory().FullName;

                if (!OperatingSystem.IsWindows())
                {
                    const UnixFileMode ownershipPermissions =
                        UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute |
                        UnixFileMode.GroupRead | UnixFileMode.GroupWrite | UnixFileMode.GroupExecute |
                        UnixFileMode.OtherRead | UnixFileMode.OtherWrite | UnixFileMode.OtherExecute;

                    File.SetUnixFileMode(bindMountPath, ownershipPermissions);
                }

                rustfs1.WithDataBindMount(bindMountPath);
            }

            using (var app = builder1.Build())
            {
                await app.StartAsync();

                var rns = app.Services.GetRequiredService<ResourceNotificationService>();

                await rns.WaitForResourceHealthyAsync(rustfs1.Resource.Name);

                await app.StopAsync();
            }

            using var builder2 = TestDistributedApplicationBuilder.Create(testOutputHelper);
            builder2.Configuration["Parameters:accessKey"] = await accessKeyParameter.GetValueAsync(default);
            var accessKey2 = builder2.AddParameter(accessKeyParameter.Name);
            builder2.Configuration["Parameters:secretKey"] = await secretKeyParameter.GetValueAsync(default);
            var secretKey2 = builder2.AddParameter(secretKeyParameter.Name);

            var rustfs2 = builder2.AddRustFs("rustfs", accessKey2, secretKey2);

            if (useVolume)
            {
                rustfs2.WithDataVolume(volumeName);
            }
            else
            {
                rustfs2.WithDataBindMount(bindMountPath!);
            }

            using (var app = builder2.Build())
            {
                await app.StartAsync();

                var rns = app.Services.GetRequiredService<ResourceNotificationService>();

                await rns.WaitForResourceHealthyAsync(rustfs2.Resource.Name);

                await app.StopAsync();
            }
        }
        finally
        {
            if (volumeName is not null)
            {
                DockerUtils.AttemptDeleteDockerVolume(volumeName);
            }

            if (bindMountPath is not null)
            {
                try
                {
                    Directory.Delete(bindMountPath, recursive: true);
                }
                catch
                {
                    // Don't fail test if we can't clean the temporary folder
                }
            }
        }
    }
}
