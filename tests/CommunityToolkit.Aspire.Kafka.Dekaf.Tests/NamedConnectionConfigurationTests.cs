// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Dekaf.Admin;
using Dekaf.Consumer;
using Dekaf.Producer;
using Dekaf.ShareConsumer;
using Microsoft.Extensions.Hosting;

namespace CommunityToolkit.Aspire.Kafka.Dekaf.Tests;

public class NamedConnectionConfigurationTests
{
    public static TheoryData<string, bool, string, bool> BootstrapServerConfigurations
    {
        get
        {
            TheoryData<string, bool, string, bool> configurations = [];
            foreach (var role in new[] { "Producer", "Consumer", "ShareConsumer", "AdminClient" })
            foreach (var keyed in new[] { false, true })
            foreach (var defaultFormat in new[] { "Array", "Scalar", "ConnectionString" })
            foreach (var namedArray in new[] { false, true })
            {
                configurations.Add(role, keyed, defaultFormat, namedArray);
            }
            return configurations;
        }
    }

    [Theory]
    [MemberData(nameof(BootstrapServerConfigurations))]
    public async Task NamedBootstrapServersReplaceDefaultServers(string role, bool keyed, string defaultFormat, bool namedArray)
    {
        var builder = ClientTestHelpers.CreateBuilder();
        builder.Configuration["ConnectionStrings:messaging"] = null;
        var section = $"Aspire:Kafka:Dekaf:{role}";
        if (defaultFormat == "Array")
        {
            builder.Configuration[$"{section}:Config:BootstrapServers:0"] = "default:9092";
            builder.Configuration[$"{section}:Config:BootstrapServers:1"] = "default-backup:9092";
        }
        else if (defaultFormat == "Scalar")
        {
            builder.Configuration[$"{section}:Config:BootstrapServers"] = "default:9092,default-backup:9092";
        }
        else
        {
            builder.Configuration[$"{section}:ConnectionString"] = "default:9092,default-backup:9092";
        }
        builder.Configuration[$"{section}:messaging:Config:BootstrapServers{(namedArray ? ":0" : "")}"] = "named:9092";

        switch (role, keyed)
        {
            case ("Producer", false): builder.AddDekafKafkaProducer<string, string>("messaging"); break;
            case ("Producer", true): builder.AddKeyedDekafKafkaProducer<string, string>("messaging"); break;
            case ("Consumer", false): builder.AddDekafKafkaConsumer<string, string>("messaging"); break;
            case ("Consumer", true): builder.AddKeyedDekafKafkaConsumer<string, string>("messaging"); break;
            case ("ShareConsumer", false): builder.AddDekafKafkaShareConsumer<string, string>("messaging"); break;
            case ("ShareConsumer", true): builder.AddKeyedDekafKafkaShareConsumer<string, string>("messaging"); break;
            case ("AdminClient", false): builder.AddDekafKafkaAdminClient("messaging"); break;
            case ("AdminClient", true): builder.AddKeyedDekafKafkaAdminClient("messaging"); break;
        }

        await using var services = builder.Services.BuildServiceProvider();
        var servers = role switch
        {
            "Producer" => ClientTestHelpers.GetOptions<ProducerOptions>(ClientTestHelpers.GetProducer(services, keyed)).BootstrapServers,
            "Consumer" => ClientTestHelpers.GetOptions<ConsumerOptions>(ClientTestHelpers.GetConsumer(services, keyed)).BootstrapServers,
            "ShareConsumer" => ClientTestHelpers.GetOptions<ShareConsumerOptions>(ClientTestHelpers.GetShareConsumer(services, keyed)).BootstrapServers,
            "AdminClient" => ClientTestHelpers.GetOptions<AdminClientOptions>(keyed
                ? services.GetRequiredKeyedService<IAdminClient>("messaging")
                : services.GetRequiredService<IAdminClient>()).BootstrapServers,
            _ => throw new ArgumentOutOfRangeException(nameof(role))
        };
        Assert.Equal(["named:9092"], servers);
    }
}
