// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace Aspire.Hosting;

internal static class FlareContainerImageTags
{
    public const string Registry = "docker.io";

    // All four Flare images are released in lockstep, so they share one tag.
    public const string Tag = "0.6";

    public const string IngestImage = "apardev/flare-ingest";
    public const string ApiImage = "apardev/flare-api";
    public const string AlertWorkerImage = "apardev/flare-alert-worker";
    public const string DashboardImage = "apardev/flare-dashboard";
}
