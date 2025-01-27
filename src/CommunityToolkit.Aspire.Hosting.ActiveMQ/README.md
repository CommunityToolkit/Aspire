# CommunityToolkit.Hosting.ActiveMQ

## Overview

This .NET Aspire Integration runs [Active MQ Classic](https://activemq.apache.org/components/classic/) in a container.


## Usage

### Example 1: ActiveMQ Classic with default tcp scheme

```csharp
var builder = DistributedApplication.CreateBuilder(args);

var amq = builder.AddActiveMQ("amq", 
        builder.AddParameter("user", "admin"),
        builder.AddParameter("password", "admin", secret: true), 
        61616)
        .PublishAsConnectionString()
        .WithEndpoint(port: 8161, targetPort: 8161, name: "web", scheme: "http");
```

### Example 2: ActiveMQ Classic with activemq scheme for use with [MassTransit](https://masstransit.io/)

```csharp
var builder = DistributedApplication.CreateBuilder(args);

var amq = builder.AddActiveMQ("amq", 
        builder.AddParameter("user", "admin"),
        builder.AddParameter("password", "admin", secret: true), 
        61616, 
        "activemq")
        .PublishAsConnectionString()
        .WithEndpoint(port: 8161, targetPort: 8161, name: "web", scheme: "http");
```

### Example 3: ActiveMQ Artemis with activemq scheme for use with [MassTransit](https://masstransit.io/)

```csharp
var builder = DistributedApplication.CreateBuilder(args);

var amq = builder.AddActiveMQArtemis("amq", 
        builder.AddParameter("user", "admin"),
        builder.AddParameter("password", "admin", secret: true), 
        61616, 
        "activemq")
        .PublishAsConnectionString()
        .WithEndpoint(port: 8161, targetPort: 8161, name: "web", scheme: "http");
```
