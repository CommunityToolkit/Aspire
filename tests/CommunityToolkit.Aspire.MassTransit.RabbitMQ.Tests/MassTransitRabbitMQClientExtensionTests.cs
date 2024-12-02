// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using MassTransit;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using OpenTelemetry.Metrics;

namespace CommunityToolkit.Aspire.MassTransit.RabbitMQ.Tests;

public class MassTransitRabbitMqExtensionsTest
{
    [Fact]
    public void AddMassTransitRabbitMq_ShouldThrowWhenBuilderIsNull()
    {
        IHostApplicationBuilder builder = null!;

        var connectionName = "rabbitmq";

        var action = () => builder.AddMassTransitRabbitMq(connectionName);

        var exception = Assert.Throws<ArgumentNullException>(action);
        Assert.Equal(nameof(builder), exception.ParamName);
    }

    [Fact]
    public void AddMassTransitRabbitMq_ShouldThrowWhenNameIsNull()
    {
        var builder = Host.CreateEmptyApplicationBuilder(null);

        string name = null!;

        var action = () => builder.AddMassTransitRabbitMq(name);

        var exception = Assert.Throws<ArgumentNullException>(action);
        Assert.Equal(nameof(name), exception.ParamName);
    }

    [Fact]
    public void AddMassTransitRabbitMq_ShouldThrowWhenNameIsEmpty()
    {
        var builder = Host.CreateEmptyApplicationBuilder(null);

        string name = "";

        var action = () => builder.AddMassTransitRabbitMq(name);

        var exception = Assert.Throws<ArgumentException>(action);
        Assert.Equal(nameof(name), exception.ParamName);
    }
    
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void AddMassTransitRabbitMq_TelemetryShouldBeRegisteredWhenEnabled(bool disableTelemetry)
    {
        var builder = Host.CreateEmptyApplicationBuilder(null);

        // Add configuration for multiple RabbitMQ instances
        builder.Configuration.AddInMemoryCollection([
            new KeyValuePair<string, string?>("ConnectionStrings:rabbitmq1", "amqp://localhost:5672"),
        ]);
        builder.AddMassTransitRabbitMq("rabbitmq1", options =>
        {
            options.DisableTelemetry = disableTelemetry;
        });

        using var host = builder.Build();

        var telemetryService = host.Services.GetService<MeterProvider>();

        if (disableTelemetry)
        {
            Assert.Null(telemetryService);
        }
        else
        {
            Assert.NotNull(telemetryService);
        }
    }

    [Fact]
    public void AddMassTransitRabbitMq_ShouldThrowExceptionForMissingConnectionString()
    {
        var builder = Host.CreateEmptyApplicationBuilder(null);

        var exception = Assert.Throws<InvalidOperationException>(() =>
        {
            builder.AddMassTransitRabbitMq("rmq");
        });

        Assert.Equal("RabbitMQ connection string is missing or empty in configuration.", exception.Message);
    }


    [Fact]
    public void CanAddMultipleRabbitMqClients()
    {
        var builder = Host.CreateEmptyApplicationBuilder(null);

        // Add configuration for multiple RabbitMQ instances
        builder.Configuration.AddInMemoryCollection([
            new KeyValuePair<string, string?>("ConnectionStrings:rabbitmq1", "amqp://localhost:5672"),
            new KeyValuePair<string, string?>("ConnectionStrings:rabbitmq2", "amqp://remotehost:5673")
        ]);


        // Add the first RabbitMQ client
        builder.AddMassTransitRabbitMq("rabbitmq1", configureConsumers: x =>
        {
            x.AddConsumer<TestConsumer>();
        });

        // Add the second RabbitMQ client with a different configuration
        builder.AddMassTransitRabbitMq<ISecondBus>("rabbitmq2", configureConsumers: x =>
        {
            x.AddConsumer<TestConsumerTwo>();
        });

        // Build the host
        using var host = builder.Build();

        // Resolve both buses
        var bus1 = host.Services.GetRequiredService<IBus>();
        var bus2 = host.Services.GetRequiredService<ISecondBus>();

        // Assert they are not the same instance
        Assert.NotSame(bus1, bus2);

        // Assert consumers are registered for their respective buses
        var consumer1 = host.Services.GetService<TestConsumer>();
        var consumer2 = host.Services.GetService<TestConsumerTwo>();
        Assert.NotNull(consumer1);
        Assert.NotNull(consumer2);
    }


    [Fact]
    public void CanConfigureConsumersAndSagas()
    {
        var builder = Host.CreateEmptyApplicationBuilder(null);

        // Add configuration for multiple RabbitMQ instances
        builder.Configuration.AddInMemoryCollection([
            new KeyValuePair<string, string?>("ConnectionStrings:rabbitmq1", "amqp://localhost:5672")
        ]);

        builder.AddMassTransitRabbitMq("rabbitmq1", configureConsumers: x =>
        {
            x.AddConsumer<TestConsumer>();
            x.AddSaga<TestSagaState>().InMemoryRepository();
        });
        using var host = builder.Build();

        var consumerService = host.Services.GetService<TestConsumer>();
        var saga = host.Services.GetService<ISagaRepository<TestSagaState>>();
        Assert.NotNull(consumerService);
        Assert.NotNull(saga);
    }
}