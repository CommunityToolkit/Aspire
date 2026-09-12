// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Aspire.Hosting;
using Aspire.Hosting.Publishing;
using Aspire.Hosting.Utils;
using CommunityToolkit.Aspire.Hosting.Bitwarden.SecretManager.Extensions;

namespace CommunityToolkit.Aspire.Hosting.Bitwarden.SecretManager.Tests;

public class BitwardenSecretSourceResolutionTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ParameterExpressionPreservesConfigurationAndBoundValuePrecedence(bool withContext)
    {
        using var builder = TestDistributedApplicationBuilder.Create(DistributedApplicationOperation.Publish);
        var manager = AddManager(builder);
        builder.Configuration["Parameters:bitwarden-input"] = "configured-input";
        var secret = manager.AddSecret("input").Resource;

        Assert.True(secret.AcceptsParameterInput);
        var expression = Assert.IsType<ReferenceExpression>(secret.ValueSource);
        Assert.Equal("{bitwarden-input.value}", expression.ValueExpression);
        Assert.Single(builder.Resources, resource => resource.Name == secret.Name);
        Assert.Equal(4, builder.Resources.OfType<ParameterResource>().Count());
        Assert.Equal("configured-input", await ReadAsync(secret, withContext));

        secret.InitializeWaitForValue();
        secret.ResolveWaitForValue("prompted-input");
        Assert.Equal("prompted-input", await ReadAsync(secret, withContext));

        manager.Resource.BindResolvedSecret(Guid.NewGuid(), secret.RemoteName, "bound-value");
        Assert.Equal("bound-value", await ReadAsync(secret, withContext));
        manager.Resource.ResetResolvedValues();
        Assert.Equal("prompted-input", await ReadAsync(secret, withContext));
    }

    [Fact]
    public async Task ParameterExpressionPreservesMissingConfigurationAndDefaults()
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
    public async Task ParameterExpressionWaitsForPromptAndCancellationDoesNotLoseTheInput(bool withContext)
    {
        using var builder = TestDistributedApplicationBuilder.Create(DistributedApplicationOperation.Publish);
        var secret = AddManager(builder).AddSecret("input").Resource;
        secret.InitializeWaitForValue();
        using var cancellation = new CancellationTokenSource();

        var pending = ReadAsync(secret, withContext, cancellation.Token);
        Assert.False(pending.IsCompleted);
        cancellation.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => pending);

        secret.ResolveWaitForValue("prompted-input");
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
        secret.InitializeWaitForValue();
        secret.ResolveWaitForValue("ignored-prompt");

        Assert.False(secret.AcceptsParameterInput);
        Assert.False(secret.IsManaged);
        Assert.Null(await ReadAsync(secret, withContext));
        manager.Resource.BindResolvedSecret(id, byId ? "name-from-server" : secret.RemoteName, string.Empty);
        Assert.Equal(string.Empty, await ReadAsync(secret, withContext));
        manager.Resource.BindResolvedSecret(id, byId ? "name-from-server" : secret.RemoteName, "remote-value");
        Assert.Equal("remote-value", await ReadAsync(secret, withContext));
        manager.Resource.ResetResolvedValues();
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