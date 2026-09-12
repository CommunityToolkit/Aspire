// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace CommunityToolkit.Aspire.Hosting.GlitchTip.Management;

internal sealed record GlitchTipProject(string Id, string Slug, string Dsn);

internal sealed record GlitchTipMonitorDefinition(
    string Identity,
    string Url,
    int IntervalSeconds = 60,
    int TimeoutSeconds = 20,
    int ExpectedStatusCode = 200);