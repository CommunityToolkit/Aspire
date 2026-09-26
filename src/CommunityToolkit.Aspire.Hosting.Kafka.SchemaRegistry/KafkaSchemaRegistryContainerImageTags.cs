// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace Aspire.Hosting;

/// <summary>
/// Container image defaults for Confluent Schema Registry.
/// </summary>
internal static class KafkaSchemaRegistryContainerImageTags
{
    /// <summary>The container registry.</summary>
    public const string Registry = "docker.io";
    /// <summary>The Schema Registry image.</summary>
    public const string Image = "confluentinc/cp-schema-registry";
    /// <summary>The tested Schema Registry image version.</summary>
    public const string Tag = "8.2.0";
}
