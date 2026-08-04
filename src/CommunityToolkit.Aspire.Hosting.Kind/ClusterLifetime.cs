// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace Aspire.Hosting;

/// <summary>
/// Specifies the lifetime behavior of a Kind cluster relative to the AppHost session.
/// </summary>
public enum ClusterLifetime
{
    /// <summary>
    /// The cluster is deleted when the AppHost shuts down.
    /// This is the default behavior.
    /// </summary>
    Session,

    /// <summary>
    /// The cluster survives AppHost restarts and is reused on the next startup.
    /// </summary>
    Persistent
}
