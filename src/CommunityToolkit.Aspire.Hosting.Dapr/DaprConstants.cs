// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace Aspire.Hosting;

/// <summary>
/// Contains constants used in Dapr integration.
/// </summary>
public static class DaprConstants
{
    /// <summary>
    /// Contains constants for Dapr building blocks.
    /// </summary>
    public static class BuildingBlocks
    {
        /// <summary>
        /// The name of the Dapr Pub/Sub building block.
        /// </summary>
        public const string PubSub = "pubsub";

        /// <summary>
        /// The name of the Dapr State Store building block.
        /// </summary>
        public const string StateStore = "state";
    }
}
