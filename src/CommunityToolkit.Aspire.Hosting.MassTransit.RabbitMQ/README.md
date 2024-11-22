# MassTransit RabbitMQ Aspire Hosting Extension

## Overview
Provides an **Aspire hosting extension** for integrating **MassTransit with RabbitMQ** in distributed .NET applications. It simplifies the configuration and management of RabbitMQ-backed message brokers in Aspire-based environments. 

---

## Features

- Configures RabbitMQ with **MassTransit** in Aspire applications.
- Loads settings from configuration with support for runtime overrides.
- Manages sensitive credentials as **Aspire parameterized resources**.
- Supports RabbitMQ management plugin with custom port options.

---

## Usage

### Installation

Add this extension to your project via builder.

### Example Usage

```csharp
var rmq = builder.AddMassTransit("RabbitMQ", options =>
{
    options.Username = "guest";
    options.Password = "guest";
    options.Port = 5672;
    options.ManagementPort = 999;
});
```


