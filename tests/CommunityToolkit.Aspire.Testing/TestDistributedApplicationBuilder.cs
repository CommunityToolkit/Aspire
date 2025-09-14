// Copied from aspire
// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Reflection;
using Aspire.Components.Common.Tests;
using Aspire.Hosting.Dashboard;
using Aspire.Hosting.Eventing;
using Aspire.Hosting.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Xunit.Abstractions;

namespace Aspire.Hosting.Utils;

/// <summary>
/// DistributedApplication.CreateBuilder() creates a builder that includes configuration to read from appsettings.json.
/// The builder has a FileSystemWatcher, which can't be cleaned up unless a DistributedApplication is built and disposed.
/// This class wraps the builder and provides a way to automatically dispose it to prevent test failures from excessive
/// FileSystemWatcher instances from many tests.
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