# CommunityToolkit.Hosting.ActiveMQ

## Overview

This .NET Aspire Integration can be used to [Active MQ Classic](https://activemq.apache.org/components/classic/) and [Active MQ Artemis](https://activemq.apache.org/components/artemis/) in a container.

## Usage

### Example 1: ActiveMQ Classic with default tcp scheme

```csharp
var builder = DistributedApplication.CreateBuilder(args);

var amq = builder.AddActiveMQ("amq",
        builder.AddParameter("user", "admin"),
        builder.AddParameter("password", "admin", secret: true),
        61616,
        webPort: 8161);
```

### Example 2: ActiveMQ Classic with activemq scheme for use with [MassTransit](https://masstransit.io/)

```csharp
var builder = DistributedApplication.CreateBuilder(args);

var amq = builder.AddActiveMQ("amq",
        builder.AddParameter("user", "admin"),
        builder.AddParameter("password", "admin", secret: true),
        61616,
        "activemq",
        webPort: 8161);
```

### Example 3: ActiveMQ Artemis with default tcp scheme

```csharp
var builder = DistributedApplication.CreateBuilder(args);

var amq = builder.AddActiveMQArtemis("amq",
        builder.AddParameter("user", "admin"),
        builder.AddParameter("password", "admin", secret: true),
        61616,
        webPort: 8161);
```

### Example 2: ActiveMQ Artemis with activemq scheme for use with [MassTransit](https://masstransit.io/)

```csharp
var builder = DistributedApplication.CreateBuilder(args);

var amq = builder.AddActiveMQArtemis("amq",
        builder.AddParameter("user", "admin"),
        builder.AddParameter("password", "admin", secret: true),
        61616,
        "activemq",
        webPort: 8161);
```
