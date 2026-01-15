using Aspire.Components.Common.Tests;
using Aspire.Hosting;
using Aspire.Hosting.Utils;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Polly;
using Renci.SshNet;
using Renci.SshNet.Common;
using Xunit.Abstractions;

namespace CommunityToolkit.Aspire.Hosting.Sftp.Tests;

[RequiresDocker]
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

        using var runningTokenSource = new CancellationTokenSource(TimeSpan.FromSeconds(15));

        await rns.WaitForResourceHealthyAsync(resourceBuilder.Resource.Name, runningTokenSource.Token);

        var hostBuilder = Host.CreateApplicationBuilder();

        var connectionString = await resourceBuilder.Resource.ConnectionStringExpression.GetValueAsync(default);

        hostBuilder.Configuration[$"ConnectionStrings:{resourceBuilder.Resource.Name}"] = connectionString;

        configure(hostBuilder);

        var host = hostBuilder.Build();

        await host.StartAsync();

        var client = host.Services.GetRequiredService<SftpClient>();

        client.HostKeyReceived += hostKeyReceived;

        try
        {
            var uri = new Uri(connectionString!);

            var retryPolicy = Policy
                .Handle<Exception>()
                .WaitAndRetryAsync(5, _ => TimeSpan.FromMilliseconds(1000));

            await retryPolicy.ExecuteAsync(async () =>
            {
                using var connectTokenSource = new CancellationTokenSource(TimeSpan.FromMinutes(1));

                await client.ConnectAsync(connectTokenSource.Token);
            });

            Assert.True(client.IsConnected);

            Assert.NotNull(client.ConnectionInfo);
            Assert.Equal(uri.Host, client.ConnectionInfo.Host);
            Assert.Equal(uri.Port, client.ConnectionInfo.Port);
            Assert.True(client.ConnectionInfo.IsAuthenticated);
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
    public async Task VerifySftpResourceWithPrivateRsaKey()
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
    public async Task VerifySftpResourceWithPrivateEd25519Key()
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
