// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
import type { GlitchTipResource, ParameterResource } from "./.aspire/modules/aspire.mjs";

// Compile-only coverage for the optional override. The running sample uses the automatic defaults.
export async function bindDeploymentParameters(
    glitchtip: GlitchTipResource,
    instanceUrl: ParameterResource,
    organization: ParameterResource,
    initialTeam: ParameterResource,
    apiToken: ParameterResource): Promise<GlitchTipResource> {
    return await glitchtip.withDeploymentParameters(instanceUrl, organization, initialTeam, apiToken);
}
