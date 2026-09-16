// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace CommunityToolkit.Aspire.Hosting.GlitchTip.Management;

/// <summary>
/// A management failure whose message is constructed locally without response
/// bodies, credentials, URLs, or nested transport exception details.
/// </summary>
internal sealed class GlitchTipManagementException(string message) : InvalidOperationException(message);
