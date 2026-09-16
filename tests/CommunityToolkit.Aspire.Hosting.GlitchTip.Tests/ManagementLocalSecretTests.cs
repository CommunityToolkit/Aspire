// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#pragma warning disable ASPIREUSERSECRETS001
// Boolean assertions intentionally keep generated credentials out of failed-test output.
#pragma warning disable xUnit2024

using Aspire.Hosting;
using Aspire.Hosting.Utils;
using Microsoft.Extensions.Configuration;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace CommunityToolkit.Aspire.Hosting.GlitchTip.Tests;

public class ManagementLocalSecretTests
{
    [Fact]
    public void GeneratedCredentialSurvivesNewDefaultWithoutLoadingApplicationSecrets()
    {
        using var store = new TestSecretStore();
        const string name = "glitchtip-worktree-postgres-password";
        var first = new GlitchTipLocalSecretDefault(store, name, 32).GetDefaultValue();
        var second = new GlitchTipLocalSecretDefault(store, name, 32).GetDefaultValue();
        Assert.True(first == second, "Local credential changed when the default was recreated.");
        Assert.True(first.Length >= 32);
        Assert.Equal(1, store.Writes);
    }

    [Fact]
    public void SeparateWorktreeKeysDoNotShareGeneratedCredentials()
    {
        using var store = new TestSecretStore();
        var first = new GlitchTipLocalSecretDefault(store, "glitchtip-worktree-a-postgres-password", 32).GetDefaultValue();
        var second = new GlitchTipLocalSecretDefault(store, "glitchtip-worktree-b-postgres-password", 32).GetDefaultValue();
        Assert.False(first == second, "Independent worktree credentials should be generated separately.");
        Assert.Equal(2, store.Writes);
    }

    [Fact]
    public async Task ExplicitParameterConfigurationWinsWithoutUserSecretsAvailability()
    {
        using var builder = TestDistributedApplicationBuilder.Create(DistributedApplicationOperation.Run);
        builder.Environment.EnvironmentName = "custom-local-environment";
        builder.Configuration.AddInMemoryCollection(new Dictionary<string, string?> { ["Parameters:configured"] = "explicit-test-credential" });
        using var store = new TestSecretStore { IsAvailable = false };
        var parameter = builder.AddParameter("configured", new GlitchTipLocalSecretDefault(store, "configured", 32), secret: true);
        Assert.True(await parameter.Resource.GetValueAsync(CancellationToken.None) == "explicit-test-credential");
        Assert.Equal(0, store.Writes);
    }

    [Fact]
    public void MissingStoreDoesNotGenerateAnUnstableDefault()
    {
        using var store = new TestSecretStore { IsAvailable = false };
        Assert.Throws<DistributedApplicationException>(() => new GlitchTipLocalSecretDefault(store, "credential", 32).GetDefaultValue());
        Assert.Equal(0, store.Writes);
    }

    [Fact]
    public void CorruptStoreIsNotOverwritten()
    {
        using var store = new TestSecretStore();
        File.WriteAllText(store.FilePath, "invalid-json");
        Assert.Throws<InvalidDataException>(() => new GlitchTipLocalSecretDefault(store, "credential", 32).GetDefaultValue());
        Assert.Equal("invalid-json", File.ReadAllText(store.FilePath));
        Assert.Equal(0, store.Writes);
    }

    [Fact]
    public void FailedPersistenceDoesNotReturnAnUnstableCredential()
    {
        using var store = new TestSecretStore { WriteSucceeds = false };
        var error = Assert.Throws<DistributedApplicationException>(() => new GlitchTipLocalSecretDefault(store, "credential", 32).GetDefaultValue());
        Assert.Contains("could not retain", error.Message);
        Assert.False(File.Exists(store.FilePath));
    }

    private sealed class TestSecretStore : IUserSecretsManager, IDisposable
    {
        public bool IsAvailable { get; init; } = true;
        public string FilePath { get; } = Path.Combine(Path.GetTempPath(), $"glitchtip-secret-test-{Guid.NewGuid():N}.json");
        internal int Writes { get; private set; }
        internal bool WriteSucceeds { get; init; } = true;

        public bool TrySetSecret(string name, string value)
        {
            if (!WriteSucceeds)
            {
                return false;
            }

            var values = File.Exists(FilePath) ? JsonNode.Parse(File.ReadAllText(FilePath))!.AsObject() : [];
            values[name] = value;
            File.WriteAllText(FilePath, values.ToJsonString());
            Writes++;
            return true;
        }

        public bool TryDeleteSecret(string name) => throw new NotSupportedException();

        public void GetOrSetSecret(IConfigurationManager configuration, string name, Func<string> valueGenerator)
        {
            if (configuration[name] is null)
            {
                var value = valueGenerator();
                configuration.AddInMemoryCollection(new Dictionary<string, string?> { [name] = value });
                TrySetSecret(name, value);
            }
        }

        public Task SaveStateAsync(JsonObject state, CancellationToken cancellationToken = default) => throw new NotSupportedException();

        public void Dispose() => File.Delete(FilePath);
    }
}