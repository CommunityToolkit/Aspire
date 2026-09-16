// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#pragma warning disable ASPIREUSERSECRETS001

using Aspire.Hosting;
using Aspire.Hosting.ApplicationModel;
using Aspire.Hosting.Publishing;
using Microsoft.Extensions.Configuration;

namespace CommunityToolkit.Aspire.Hosting.GlitchTip;

/// <summary>
/// Keeps only local generated credentials stable even when the AppHost's environment
/// does not automatically load user secrets. Explicit application configuration wins
/// before ParameterResource evaluates this default.
/// </summary>
internal sealed class GlitchTipLocalSecretDefault(IUserSecretsManager manager, string parameterName, int minLength) : ParameterDefault
{
    private readonly GenerateParameterDefault _generator = new() { MinLength = minLength };

    public override string GetDefaultValue()
    {
        if (!manager.IsAvailable || string.IsNullOrEmpty(manager.FilePath))
        {
            throw new DistributedApplicationException("Local GlitchTip requires an AppHost UserSecretsId to retain its generated credentials, or explicit values for all local secret parameters.");
        }

        // This configuration is private to the credential default. Never add the
        // secret store to the application's configuration or change its precedence.
        using var stored = new ConfigurationManager();
        stored.AddJsonFile(manager.FilePath, optional: true, reloadOnChange: false);
        var key = $"Parameters:{parameterName}";
        manager.GetOrSetSecret(stored, key, _generator.GetDefaultValue);
        var value = stored[key];
        // The manager deliberately makes writes best effort. A credential backed by
        // a persistent database must not proceed when that write did not persist.
        using var persisted = new ConfigurationManager();
        persisted.AddJsonFile(manager.FilePath, optional: true, reloadOnChange: false);
        if (value is null || persisted[key] != value)
        {
            throw new DistributedApplicationException("GlitchTip could not retain its generated local credential in the AppHost user secrets store.");
        }

        return value;
    }

    public override void WriteToManifest(ManifestPublishingContext context) => _generator.WriteToManifest(context);
}