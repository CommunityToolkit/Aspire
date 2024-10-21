// Copied from https://github.com/dotnet/aspire/blob/b51d08a617a60ae30f8305d98f1e34e1ed90da1a/tests/Aspire.Components.Common.Tests/TestLoggerFactory.cs
// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Collections.Concurrent;

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace Aspire.Components.ConformanceTests;

public sealed class TestLoggerFactory : ILoggerFactory
{
    public List<ILoggerProvider> LoggerProviders { get; } = new();
    public ConcurrentBag<string> Categories { get; } = new();

    public void AddProvider(ILoggerProvider provider) => LoggerProviders.Add(provider);

    public ILogger CreateLogger(string categoryName)
    {
        Categories.Add(categoryName);
        return NullLogger.Instance;
    }

    public void Dispose() { }
}