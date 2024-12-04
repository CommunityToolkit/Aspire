# MassTransit RabbitMQ Aspire Client Extension

## Overview

This package provides an **Aspire client extension** for seamlessly integrating **MassTransit with RabbitMQ**. It works with the `Aspire.Hosting.RabbitMQ.AddRabbitMQ()` method for hosting.

The name string should match the name used in `Aspire.Hosting.RabbitMQ.AddRabbitMQ()`, as it references the connection string.

---

## Features

- Configures **MassTransit RabbitMQ** integration for clients.
- Automatically discovers and registers **consumers**, **sagas**, and **activities**.
- Supports **OpenTelemetry** and **Application Insights** for monitoring.
- Uses the same configuration format as the hosting environment for easy integration.
- **Multi-bus support** to configure multiple RabbitMQ instances.

---

## Usage

### Installation

To install, add the extension to your client application using `builder.Services` in `Startup.cs` or `Program.cs`.

### Example Usage

```csharp
builder.AddMassTransitRabbitMq(
    "rmq",
    options => { options.DisableTelemetry = false; },
    consumers =>
    {
        consumers.AddConsumer<SubmitOrderConsumer>();
        consumers.AddConsumer<CancelOrderConsumer>();
        consumers.AddConsumer<UpdateOrderConsumer>();
    }
);
```
### Multi-bus example


```csharp
public interface ISecondBus : IBus;
```

```csharp
        builder.AddMassTransitRabbitMq("rabbitmq1", massTransitConfiguration: x =>
        {
            x.AddConsumer<TestConsumer>();
        });
        builder.AddMassTransitRabbitMq<ISecondBus>("rabbitmq2", massTransitConfiguration: x =>
        {
            x.AddConsumer<TestConsumerTwo>();
        });
```
