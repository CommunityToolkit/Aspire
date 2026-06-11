// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Aspire.Hosting.Testing;
using Aspire.Hosting.Utils;

namespace CommunityToolkit.Aspire.Hosting.Kind.Tests;

/// <summary>
/// Creates a builder configured for publish-mode testing with the current
/// Aspire pipeline config keys (AppHost:Operation, Pipeline:*).
/// </summary>
internal static class PublishTestBuilder
{
    internal static IDistributedApplicationTestingBuilder CreateForPublish(string outputPath)
    {
        return TestDistributedApplicationBuilder.Create(
            "AppHost:Operation=publish", $"Pipeline:OutputPath={outputPath}", "Pipeline:Step=publish");
    }
}
