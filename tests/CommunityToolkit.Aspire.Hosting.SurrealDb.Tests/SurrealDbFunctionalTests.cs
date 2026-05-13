// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Aspire.Components.Common.Tests;
using Aspire.Hosting;
using Aspire.Hosting.Utils;
using Bogus;
using CommunityToolkit.Aspire.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Hosting;
using Polly;
using SurrealDb.Net;
using SurrealDb.Net.Exceptions;
using System.Data;
using SurrealRecord = SurrealDb.Net.Models.Record;

namespace CommunityToolkit.Aspire.Hosting.SurrealDb.Tests;

[RequiresDocker]
public class SurrealDbFunctionalTests(ITestOutputHelper testOutputHelper)
{
    private const int _generatedTodoCount = 10;
    private static readonly Todo[] _todoList;

    static SurrealDbFunctionalTests()
    {
        _todoList = [.. new TodoFaker().Generate(_generatedTodoCount)];

        int index = 0;
        foreach (var todo in _todoList)
        {
            todo.Id = (Todo.Table, (++index).ToString());
        }
    }

    [Fact]
    public async Task VerifySurrealDbResource()
    {
        using var cts = new CancellationTokenSource(TimeSpan.FromMinutes(5));
        var ct = cts.Token;
        using var builder = TestDistributedApplicationBuilder.Create(testOutputHelper);

        var surrealServer = builder.AddSurrealServer("surreal");

        var db = surrealServer
            .AddNamespace("ns")
            .AddDatabase("db");

        using var app = builder.Build();

        await app.StartAsync();

        await app.ResourceNotifications.WaitForResourceHealthyAsync(db.Resource.Name, ct);

        var hb = Host.CreateApplicationBuilder();

        hb.Configuration[$"ConnectionStrings:{db.Resource.Name}"] = await db.Resource.ConnectionStringExpression.GetValueAsync(default);

        hb.AddSurrealClient(db.Resource.Name);

        using var host = hb.Build();

        await host.StartAsync(ct);

        var surrealDbClient = host.Services.GetRequiredService<SurrealDbClient>();

        await CreateTestData(surrealDbClient, ct);
        await AssertTestData(surrealDbClient, ct);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task WithDataShouldPersistStateBetweenUsages(bool useVolume)
    {
        using var cts = new CancellationTokenSource(TimeSpan.FromMinutes(5));
        var ct = cts.Token;

        string? volumeName = null;
        string? bindMountPath = null;

        try
        {
            using var builder1 = TestDistributedApplicationBuilder.Create(testOutputHelper);
            
            var password1 = builder1.AddParameter("surreal-password", secret: true);
            password1.Resource.Default = new PasswordConstantDefault();
            
            var surrealServer1 = builder1.AddSurrealServer("surreal", path: "rocksdb://data/db.db", password: password1);

            var db1 = surrealServer1
                .AddNamespace("ns")
                .AddDatabase("db");

            if (useVolume)
            {
                // Use a deterministic volume name to prevent them from exhausting the machines if deletion fails
                volumeName = VolumeNameGenerator.Generate(surrealServer1, nameof(WithDataShouldPersistStateBetweenUsages));

                // if the volume already exists (because of a crashing previous run), delete it
                DockerUtils.AttemptDeleteDockerVolume(volumeName, throwOnFailure: true);
                surrealServer1.WithDataVolume(volumeName);
            }
            else
            {
                bindMountPath = Directory.CreateTempSubdirectory().FullName;
                surrealServer1.WithDataBindMount(bindMountPath);

                if (!OperatingSystem.IsWindows())
                {
                    File.SetUnixFileMode(bindMountPath, UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute | UnixFileMode.OtherRead | UnixFileMode.OtherWrite | UnixFileMode.OtherExecute);
                }
            }

            using (var app = builder1.Build())
            {
                await app.StartAsync(ct);

                await app.ResourceNotifications.WaitForResourceHealthyAsync(surrealServer1.Resource.Name, ct);
                await app.ResourceNotifications.WaitForResourceHealthyAsync(db1.Resource.Name, ct);

                try
                {
                    var hb = Host.CreateApplicationBuilder();

                    hb.Configuration[$"ConnectionStrings:{db1.Resource.Name}"] = await db1.Resource.ConnectionStringExpression.GetValueAsync(ct);

                    hb.AddSurrealClient(db1.Resource.Name);

                    using var host = hb.Build();
                    await host.StartAsync(ct);

                    await using var surrealDbClient = host.Services.GetRequiredService<SurrealDbClient>();
                    await CreateTestData(surrealDbClient, ct);
                    await AssertTestData(surrealDbClient, ct);
                }
                finally
                {
                    // Stops the container, or the Volume would still be in use
                    await app.StopAsync(ct);
                }
            }

            using var builder2 = TestDistributedApplicationBuilder.Create(testOutputHelper);
            
            var password2 = builder2.AddParameter("surreal-password", secret: true);
            password2.Resource.Default = new PasswordConstantDefault();
            
            var surrealServer2 = builder2.AddSurrealServer("surreal", path: "rocksdb://data/db.db", password: password2);

            var db2 = surrealServer2
                .AddNamespace("ns")
                .AddDatabase("db");

            if (useVolume)
            {
                surrealServer2.WithDataVolume(volumeName);
            }
            else
            {
                surrealServer2.WithDataBindMount(bindMountPath!);
            }

            using (var app = builder2.Build())
            {
                await app.StartAsync(ct);

                await app.ResourceNotifications.WaitForResourceHealthyAsync(surrealServer2.Resource.Name, ct);
                await app.ResourceNotifications.WaitForResourceHealthyAsync(db2.Resource.Name, ct);

                try
                {
                    var hb = Host.CreateApplicationBuilder();

                    hb.Configuration[$"ConnectionStrings:{db2.Resource.Name}"] = await db2.Resource.ConnectionStringExpression.GetValueAsync(ct);

                    hb.AddSurrealClient(db2.Resource.Name);

                    using var host = hb.Build();
                    await host.StartAsync(ct);
                    await using var surrealDbClient = host.Services.GetRequiredService<SurrealDbClient>();
                    await AssertTestData(surrealDbClient, ct);
                }
                finally
                {
                    // Stops the container, or the Volume would still be in use
                    await app.StopAsync(ct);
                }
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

    [Fact]
    public async Task VerifyWaitForOnSurrealDbBlocksDependentResources()
    {
        using var cts = new CancellationTokenSource(TimeSpan.FromMinutes(10));
        using var builder = TestDistributedApplicationBuilder.Create(testOutputHelper);

        var healthCheckTcs = new TaskCompletionSource<HealthCheckResult>();
        builder.Services.AddHealthChecks().AddAsyncCheck("blocking_check", () => healthCheckTcs.Task);

        var resource = builder.AddSurrealServer("resource")
            .WithHealthCheck("blocking_check");

        var dependentResource = builder.AddSurrealServer("dependentresource")
            .WaitFor(resource);

        using var app = builder.Build();

        var pendingStart = app.StartAsync(cts.Token);

        var rns = app.Services.GetRequiredService<ResourceNotificationService>();

        await rns.WaitForResourceAsync(resource.Resource.Name, KnownResourceStates.Running, cts.Token);

        await rns.WaitForResourceAsync(dependentResource.Resource.Name, KnownResourceStates.Waiting, cts.Token);

        healthCheckTcs.SetResult(HealthCheckResult.Healthy());

        await rns.WaitForResourceAsync(resource.Resource.Name, re => re.Snapshot.HealthStatus == HealthStatus.Healthy, cts.Token);

        await rns.WaitForResourceAsync(dependentResource.Resource.Name, KnownResourceStates.Running, cts.Token);

        await pendingStart;

        await app.StopAsync();
    }
    
    [Fact(Skip = "Feature is unstable and blocking the release")]
    public async Task VerifyWithInitFiles()
    {
        // Creates a script that should be executed when the container is initialized.
    
        using var cts = new CancellationTokenSource(TimeSpan.FromMinutes(10));
        var pipeline = new ResiliencePipelineBuilder()
            .AddRetry(new() { MaxRetryAttempts = 10, BackoffType = DelayBackoffType.Linear, Delay = TimeSpan.FromSeconds(2), ShouldHandle = new PredicateBuilder().Handle<SurrealDbException>() })
            .Build();
    
        var initDirPath = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());
    
        Directory.CreateDirectory(initDirPath);
        
        var initFilePath = Path.Combine(initDirPath, "init.surql");
    
        var surrealNsName = "ns1";
        var surrealDbName = "db1";
        
        try
        {
            await File.WriteAllTextAsync(
                initFilePath, 
        $"""
                USE NS {surrealNsName};
                USE DB {surrealDbName};
                
                CREATE car SET Brand = "BatMobile";
                """, 
                cts.Token
            );
    
            using var builder = TestDistributedApplicationBuilder.Create(testOutputHelper);

#pragma warning disable CTASPIRE002 // Type is for evaluation purposes only and is subject to change or removal in future updates. Suppress this diagnostic to proceed.
            var surrealServer = builder
                .AddSurrealServer("surreal")
                .WithInitFiles(initFilePath);
#pragma warning restore CTASPIRE002 // Type is for evaluation purposes only and is subject to change or removal in future updates. Suppress this diagnostic to proceed.

            var ns = surrealServer.AddNamespace(surrealNsName);
            var db = ns.AddDatabase(surrealDbName);
    
            using var app = builder.Build();
    
            await app.StartAsync(cts.Token);
                
            var rns = app.Services.GetRequiredService<ResourceNotificationService>();
            
            await rns.WaitForResourceAsync(db.Resource.Name, KnownResourceStates.Running, cts.Token);
    
            var hb = Host.CreateApplicationBuilder();
    
            hb.Configuration.AddInMemoryCollection(new Dictionary<string, string?>
            {
                [$"ConnectionStrings:{db.Resource.Name}"] = await db.Resource.ConnectionStringExpression.GetValueAsync(cts.Token)
            });
    
            hb.AddSurrealClient(db.Resource.Name);
    
            using var host = hb.Build();
    
            await host.StartAsync(cts.Token);

            // Wait until the database is available
            await rns.WaitForResourceHealthyAsync(db.Resource.Name, cts.Token);
    
            await pipeline.ExecuteAsync(async token =>
            {
                var client = host.Services.GetRequiredService<SurrealDbClient>();

                var cars = (await client.Select<Car>(Car.Table, token)).ToList();
                var car = Assert.Single(cars);
                Assert.Equal("BatMobile", car.Brand);
            }, cts.Token);
        }
        finally
        {
            try
            {
                Directory.Delete(initDirPath);
            }
            catch
            {
                // Don't fail test if we can't clean the temporary folder
            }
        }
    }
    
    [Fact(Skip = "Feature is unstable and blocking the release")]
    public async Task AddDatabaseCreatesDatabaseWithCustomScript()
    {
        using var cts = new CancellationTokenSource(TimeSpan.FromMinutes(5));

        using var builder = TestDistributedApplicationBuilder.Create(testOutputHelper);
        
        var surrealNsName = "ns1";
        var surrealDbName = "db1";

        var surreal = builder.AddSurrealServer("surreal", strictMode: true);

#pragma warning disable CTASPIRE002 // Type is for evaluation purposes only and is subject to change or removal in future updates. Suppress this diagnostic to proceed.
        var db = surreal
            .AddNamespace(surrealNsName)
            .AddDatabase(surrealDbName)
            .WithCreationScript(
                $"""
                DEFINE DATABASE IF NOT EXISTS {surrealDbName};
                USE DATABASE {surrealDbName};
                
                DEFINE TABLE todo;
                DEFINE FIELD Title ON todo TYPE string;
                DEFINE FIELD DueBy ON todo TYPE datetime;
                DEFINE FIELD IsComplete ON todo TYPE bool;
                """
            );
#pragma warning restore CTASPIRE002 // Type is for evaluation purposes only and is subject to change or removal in future updates. Suppress this diagnostic to proceed.

        using var app = builder.Build();

        await app.StartAsync(cts.Token);

        var hb = Host.CreateApplicationBuilder();

        hb.Configuration[$"ConnectionStrings:{db.Resource.Name}"] = await db.Resource.ConnectionStringExpression.GetValueAsync(default);

        hb.AddSurrealClient(db.Resource.Name);

        using var host = hb.Build();

        await host.StartAsync();

        await app.ResourceNotifications.WaitForResourceHealthyAsync(db.Resource.Name, cts.Token);

        await using var client = host.Services.GetRequiredService<SurrealDbClient>();

        await CreateTestData(client, cts.Token);
        await AssertTestData(client, cts.Token);
    }

    private static async Task CreateTestData(SurrealDbClient surrealDbClient, CancellationToken ct)
    {
        await surrealDbClient.Insert(Todo.Table, _todoList, ct);
    }

    private static async Task AssertTestData(SurrealDbClient surrealDbClient, CancellationToken ct)
    {
        var records = await surrealDbClient.Select<Todo>(Todo.Table, ct);
        Assert.Equal(_generatedTodoCount, records.Count());

        var firstRecord = await surrealDbClient.Select<Todo>((Todo.Table, "1"), ct);
        Assert.NotNull(firstRecord);
        Assert.Equivalent(firstRecord, _todoList[0]);
    }

    private sealed class Todo : SurrealRecord
    {
        internal const string Table = "todo";

        public string? Title { get; set; }
        public DateOnly? DueBy { get; set; } = null;
        public bool IsComplete { get; set; } = false;
    }

    private class TodoFaker : Faker<Todo>
    {
        public TodoFaker()
        {
            RuleFor(o => o.Title, f => f.Lorem.Sentence());
            RuleFor(o => o.DueBy, f => f.Date.SoonDateOnly());
            RuleFor(o => o.IsComplete, f => f.Random.Bool());
        }
    }

    private sealed class Car : SurrealRecord
    {
        internal const string Table = "car";

        public string Brand { get; set; } = string.Empty;
    }
}