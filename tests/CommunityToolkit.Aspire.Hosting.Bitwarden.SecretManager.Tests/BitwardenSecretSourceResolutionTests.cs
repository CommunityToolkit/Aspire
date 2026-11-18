// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#pragma warning disable ASPIREINTERACTION001

using Aspire.Hosting;
using Aspire.Hosting.Publishing;
using Aspire.Hosting.Utils;
using Microsoft.Extensions.DependencyInjection;

namespace CommunityToolkit.Aspire.Hosting.Bitwarden.SecretManager.Tests;

public class BitwardenSecretSourceResolutionTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ParameterReadsPreservePreparedInputAndIgnoreBoundRemoteValues(bool withContext)
    {
        using var builder = TestDistributedApplicationBuilder.Create(DistributedApplicationOperation.Publish);
        var manager = AddManager(builder);
        builder.Configuration["Parameters:bitwarden-input"] = "configured-input";
        var secret = manager.AddSecret("input").Resource;

        Assert.Single(builder.Resources, resource => resource.Name == secret.Name);
        Assert.Equal(4, builder.Resources.OfType<ParameterResource>().Count());
        Assert.Equal("configured-input", await ReadAsync(secret, withContext));

        await secret.SetValueAsync("prompted-input");
        Assert.Equal("prompted-input", await ReadAsync(secret, withContext));

        await manager.Resource.BindResolvedSecretAsync(Guid.NewGuid(), secret.RemoteName, "bound-value", default);
        Assert.Equal("prompted-input", await ReadAsync(secret, withContext));
        await manager.Resource.ResetResolvedValuesAsync(default);
        Assert.Equal("prompted-input", await ReadAsync(secret, withContext));
    }

    [Fact]
    public async Task ParameterValuePreservesMissingConfigurationAndDefaults()
    {
        using var builder = TestDistributedApplicationBuilder.Create(DistributedApplicationOperation.Publish);
        var secret = AddManager(builder).AddSecret("input").Resource;

        await Assert.ThrowsAsync<MissingParameterValueException>(() => ReadAsync(secret, false));
        var defaultSecret = new BitwardenSecretResource("default-secret", "default-secret", secret.Parent, value => value!.GetDefaultValue()) { Default = new FixedDefault() };
        Assert.Equal("default-input", await ReadAsync(defaultSecret, true));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ParameterValueWaitsForPromptAndCancellationDoesNotLoseTheInput(bool withContext)
    {
        using var builder = TestDistributedApplicationBuilder.Create(DistributedApplicationOperation.Publish);
        builder.Services.AddSingleton<IInteractionService>(new FakeInteractionService(canceled: true));
        var secret = AddManager(builder).AddSecret("input").Resource;
        using var app = builder.Build();
        var processor = app.Services.GetRequiredService<ParameterProcessor>();
        await processor.InitializeParametersAsync([secret]);
        using var cancellation = new CancellationTokenSource();

        var pending = ReadAsync(secret, withContext, cancellation.Token);
        Assert.False(pending.IsCompleted);
        cancellation.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => pending);

        await secret.SetValueAsync("prompted-input");
        Assert.Equal("prompted-input", await ReadAsync(secret, withContext));
    }

    [Theory]
    [InlineData(false, false)]
    [InlineData(false, true)]
    [InlineData(true, false)]
    [InlineData(true, true)]
    public async Task RemoteSourcePreservesNullUntilBoundAndDoesNotReadParameterInputs(bool byId, bool withContext)
    {
        using var builder = TestDistributedApplicationBuilder.Create(DistributedApplicationOperation.Publish);
        var manager = AddManager(builder);
        var id = Guid.NewGuid();
        var secret = (byId ? manager.GetSecret("reference", id) : manager.GetSecret("reference", "remote-name")).Resource;
        builder.Configuration[$"Parameters:{secret.Name}"] = "ignored-input";

        Assert.False(secret.IsManaged);
        Assert.Null(await ReadAsync(secret, withContext));
        await manager.Resource.BindResolvedSecretAsync(id, byId ? "name-from-server" : secret.RemoteName, string.Empty, default);
        Assert.Equal(string.Empty, await ReadAsync(secret, withContext));
        await manager.Resource.BindResolvedSecretAsync(id, byId ? "name-from-server" : secret.RemoteName, "remote-value", default);
        Assert.Equal("remote-value", await ReadAsync(secret, withContext));
        await manager.Resource.ResetResolvedValuesAsync(default);
        Assert.Null(await ReadAsync(secret, withContext));
    }

    private static async Task<string?> ReadAsync(BitwardenSecretResource secret, bool withContext, CancellationToken cancellationToken = default) =>
        withContext
            ? await ((IValueProvider)secret).GetValueAsync(new ValueProviderContext { Caller = new ContainerResource("consumer") }, cancellationToken)
            : await ((IValueProvider)secret).GetValueAsync(cancellationToken);

    private static IResourceBuilder<BitwardenSecretManagerResource> AddManager(IDistributedApplicationBuilder builder) =>
        builder.AddBitwardenSecretManager("bitwarden", builder.AddParameter("project"),
            builder.AddParameter("organization"), builder.AddParameter("token", secret: true));

    private sealed class FixedDefault : ParameterDefault
    {
        public override string GetDefaultValue() => "default-input";
        public override void WriteToManifest(ManifestPublishingContext context) => throw new NotSupportedException();
    }
}
