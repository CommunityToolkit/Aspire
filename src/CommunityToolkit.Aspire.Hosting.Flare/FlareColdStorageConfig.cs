// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace Aspire.Hosting;

// The ClickHouse configuration WithColdStorage adds. Flare's query API looks for the flare_tiered policy
// and reports cold storage as unavailable without it, so the policy and volume names are fixed.
internal static class FlareColdStorageConfig
{
    public const string Directory = "/etc/clickhouse-server/config.d";
    public const string FileName = "flare-cold-storage.xml";

    // The keys and endpoint come from the container's environment, never from this file.
    // The tiered policy's first volume must be called "default": ALTER TABLE ... MODIFY SETTING
    // storage_policy only accepts a policy that keeps every volume of the table's current policy.
    // perform_ttl_move_on_insert=0 leaves moves to the background instead of checking each insert.
    public const string Contents =
        """
        <clickhouse>
            <storage_configuration>
                <disks>
                    <cold>
                        <type>s3</type>
                        <endpoint from_env="FLARE_COLD_ENDPOINT"/>
                        <access_key_id from_env="FLARE_COLD_ACCESS_KEY"/>
                        <secret_access_key from_env="FLARE_COLD_SECRET_KEY"/>
                        <region>us-east-1</region>
                        <metadata_path>/var/lib/clickhouse/disks/cold/</metadata_path>
                    </cold>
                </disks>
                <policies>
                    <flare_tiered>
                        <volumes>
                            <default>
                                <disk>default</disk>
                            </default>
                            <cold>
                                <disk>cold</disk>
                                <perform_ttl_move_on_insert>0</perform_ttl_move_on_insert>
                            </cold>
                        </volumes>
                    </flare_tiered>
                </policies>
            </storage_configuration>
        </clickhouse>
        """;
}
