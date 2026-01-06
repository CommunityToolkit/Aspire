using Aspire.Hosting;
using Aspire.Hosting.Utils;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Polly;
using Renci.SshNet;
using Renci.SshNet.Common;
using Xunit.Abstractions;

namespace CommunityToolkit.Aspire.Hosting.Sftp.Tests;

public class SftpFunctionalTests : IDisposable
{
    private readonly IDistributedApplicationTestingBuilder builder;

    private IResourceBuilder<SftpContainerResource>? resourceBuilder;

    public SftpFunctionalTests(ITestOutputHelper logger)
    {
        builder = TestDistributedApplicationBuilder.Create();

        builder.Services.AddLogging(bld => bld.AddXUnit(logger));
    }

    public void Dispose()
    {
        builder.Dispose();
    }

    private async Task RunTestAsync(Action<HostApplicationBuilder> configure, EventHandler<HostKeyEventArgs>? hostKeyReceived = null)
    {
        Assert.NotNull(resourceBuilder);

        var app = builder.Build();

        await app.StartAsync();

        var rns = app.Services.GetRequiredService<ResourceNotificationService>();

        try
        {
            await rns.WaitForResourceAsync(resourceBuilder.Resource.Name, "Running", new CancellationTokenSource(TimeSpan.FromSeconds(15)).Token);
        }
        catch
        {
            ResourceEvent? resourceEvent = null;

            var res = rns.TryGetCurrentState(resourceBuilder.Resource.Name, out resourceEvent);

            throw;
        }

        var hostBuilder = Host.CreateApplicationBuilder();

        hostBuilder.Configuration[$"ConnectionStrings:{resourceBuilder.Resource.Name}"] = await resourceBuilder.Resource.ConnectionStringExpression.GetValueAsync(default);

        configure(hostBuilder);

        var host = hostBuilder.Build();

        await host.StartAsync();

        var client = host.Services.GetRequiredService<SftpClient>();

        client.HostKeyReceived += hostKeyReceived;

        try
        {
            var retryPolicy = Policy
                .Handle<Exception>()
                .WaitAndRetryAsync(3, _ => TimeSpan.FromMilliseconds(500));

            await retryPolicy.ExecuteAsync(async () =>
            {
                await client.ConnectAsync(new CancellationTokenSource(TimeSpan.FromMinutes(1)).Token);
            });
        }
        catch
        {
            throw;
        }
        finally
        {
            client.Disconnect();
        }
    }

    [Fact]
    public async Task VerifySftpResourceWithArgs()
    {
        resourceBuilder = builder
            .AddSftp("sftp")
            .WithArgs($"foo:pass:::uploads");

        await RunTestAsync(bld =>
        {
            bld.AddSftpClient(resourceBuilder.Resource.Name, cfg =>
            {
                cfg.Username = "foo";
                cfg.Password = "pass";
            });
        });
    }

    [Fact]
    public async Task VerifySftpResourceWithUsersEnvironmentVariable()
    {
        resourceBuilder = builder
            .AddSftp("sftp")
            .WithEnvironment("SFTP_USERS", "foo:pass:::uploads");

        await RunTestAsync(bld =>
        {
            bld.AddSftpClient(resourceBuilder.Resource.Name, cfg =>
            {
                cfg.Username = "foo";
                cfg.Password = "pass";
            });
        });
    }

    [Fact]
    public async Task VerifySftpResourceWithEncryptedPassword()
    {
        resourceBuilder = builder
            .AddSftp("sftp")
            .WithEnvironment("SFTP_USERS", "foo:$5$t9qxNlrcFqVBNnad$U27ZrjbKNjv4JkRWvi6MjX4x6KXNQGr8NTIySOcDgi4:e:::uploads");

        await RunTestAsync(bld =>
        {
            bld.AddSftpClient(resourceBuilder.Resource.Name, cfg =>
            {
                cfg.Username = "foo";
                cfg.Password = "pass";
            });
        });
    }

    [Fact]
    public async Task VerifySftpResourceWithUsersFile()
    {
        resourceBuilder = builder
            .AddSftp("sftp")
            .WithUsersFile("users.conf");

        await RunTestAsync(bld =>
        {
            bld.AddSftpClient(resourceBuilder.Resource.Name, cfg =>
            {
                cfg.Username = "foo";
                cfg.Password = "pass";
            });
        });
    }

    [Fact]
    public async Task VerifySftpResourceWithHostKey()
    {
        resourceBuilder = builder
            .AddSftp("sftp")
            .WithUsersFile("users.conf")
            .WithHostKeyFile("ssh_host_ed25519_key", KeyType.Ed25519);

        await RunTestAsync(bld =>
        {
            bld.AddSftpClient(resourceBuilder.Resource.Name, cfg =>
            {
                cfg.Username = "foo";
                cfg.Password = "pass";
            });
        }, (obj, args) =>
        {
            Assert.Equal("zfOQDzgMTHSJruZIK37h8L8Gfy3XIJmCXYdqW0OXS7s", args.FingerPrintSHA256);
        });
    }

    [Fact]
    public async Task VerifySftpResourceWithRsaKeys()
    {
        resourceBuilder = builder
            .AddSftp("sftp")
            .WithArgs($"foo::::uploads")
            .WithUserKeyFile("foo", "id_rsa.pub", KeyType.Rsa);

        await RunTestAsync(bld =>
        {
            bld.AddSftpClient(resourceBuilder.Resource.Name, cfg =>
            {
                cfg.Username = "foo";
                cfg.PrivateKeyFile = "id_rsa";
            });
        });
    }

    [Fact]
    public async Task VerifySftpResourceWithEd25519Keys()
    {
        resourceBuilder = builder
            .AddSftp("sftp")
            .WithArgs($"foo::::uploads")
            .WithUserKeyFile("foo", "id_ed25519.pub", KeyType.Ed25519);

        await RunTestAsync(bld =>
        {
            bld.AddSftpClient(resourceBuilder.Resource.Name, cfg =>
            {
                cfg.Username = "foo";
                cfg.PrivateKeyFile = "id_ed25519";
            });
        });
    }
}
