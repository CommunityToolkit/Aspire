// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Aspire.Hosting.Testing;

namespace CommunityToolkit.Aspire.Hosting.Agents.Tests;

internal static class TestDistributedApplicationBuilder
{
    public static IDistributedApplicationTestingBuilder Create(
        DistributedApplicationOperation operation = DistributedApplicationOperation.Run)
    {
        string[] args = operation == DistributedApplicationOperation.Publish
            ? ["AppHost:Operation=publish"]
            : [];
        return DistributedApplicationTestingBuilder.Create(args, (options, _) =>
        {
            options.TrustDeveloperCertificate = false;
        });
    }
}
