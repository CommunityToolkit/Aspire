// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.Extensions.Logging;
using Xunit.Abstractions;

namespace Aspire.Hosting.Utils;

/// <summary>
/// Thin wrapper around <see cref="DistributedApplicationTestingBuilder"/> to implement
/// common conventions for Community Toolkit tests
/// </summary>
public static class TestDistributedApplicationBuilder
{
    public static IDistributedApplicationTestingBuilder Create(DistributedApplicationOperation operation)
    {
        var args = operation switch
        {
            DistributedApplicationOperation.Run => (string[])[],
            DistributedApplicationOperation.Publish => ["Publishing:Publisher=manifest"],
            _ => throw new ArgumentOutOfRangeException(nameof(operation))
        };

        return Create(args);
    }

    public static IDistributedApplicationTestingBuilder Create(params string[] args)
    {
        return DistributedApplicationTestingBuilder.Create(args);
    }

    public static IDistributedApplicationTestingBuilder Create(ITestOutputHelper testOutputHelper, params string[] args)
    {
        return Create(args)
        .WithTestAndResourceLogging(testOutputHelper);
    }
    
    public static T WithTestAndResourceLogging<T>(this T builder, ITestOutputHelper testOutputHelper)
        where T : IDistributedApplicationBuilder
    {
        builder.Services.AddLogging(logging => logging
            .AddXUnit(testOutputHelper)
            .AddFilter("Aspire.Hosting", LogLevel.Trace)
            .AddFilter(builder.Environment.ApplicationName, LogLevel.Trace)
        );
        return builder;
    }

}