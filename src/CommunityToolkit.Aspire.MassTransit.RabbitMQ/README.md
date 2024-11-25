# MassTransit RabbitMQ Aspire Client Extension

## Overview

Provides an **Aspire client extension** for integrating **MassTransit with RabbitMQ**, leveraging the same configuration as the hosting environment. It includes optional support for telemetry.

---

## Features

- Configures MassTransit RabbitMQ integration for clients.
- Automatically discovers and registers consumers, sagas, and activities.
- Supports **OpenTelemetry** and **Application Insights** for monitoring.
- Uses the same configuration format as the hosting environment for seamless integration.

---

## Usage

### Installation

Add this extension to your client application using `builder.Services` in your `Startup` or `Program.cs`.

### Example Usage

```csharp
builder.Services.AddMassTransitClient("RabbitMQ", telemetry: true);
```

