// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using CommunityToolkit.Aspire.Testing;

namespace CommunityToolkit.Aspire.Hosting.GlitchTip.Tests;

public class TypeScriptTests
{
    [Fact]
    public async Task GeneratedSdkCompilesThePublicHostingContract()
    {
        var root = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../../../"));
        var example = Path.Combine(root, "examples", "glitchtip", "GlitchTip.AppHost.TypeScript");
        var cancellationToken = TestContext.Current.CancellationToken;
        await ProcessTestUtilities.RunProcessAsync("aspire", ["restore", "--apphost", "apphost.mts", "--non-interactive"], example, cancellationToken);
        await ProcessTestUtilities.RunProcessAsync("node", ["node_modules/typescript/bin/tsc", "--noEmit"], example, cancellationToken);
        var generated = await File.ReadAllTextAsync(Path.Combine(example, ".aspire", "modules", "aspire.mts"), cancellationToken);
        Assert.Contains("withDeploymentParameters(", generated);
        Assert.DoesNotContain("publishToGlitchTip(", generated);
    }
}