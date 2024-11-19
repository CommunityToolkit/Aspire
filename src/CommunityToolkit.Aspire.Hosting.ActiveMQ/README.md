# CommunityToolkit.Hosting.ActiveMQ

## Overview

This .NET Aspire Integration runs [Active MQ Classig](https://activemq.apache.org/components/classic/) in a container.


## Usage

### Example 1: ActiveMQ with default tcp scheme

```csharp
var builder = DistributedApplication.CreateBuilder(args);

var amq = builder.AddActiveMQ("amq", 
        builder.AddParameter("user", "admin"),
        builder.AddParameter("password", "admin", secret: true), 
        61616)
        .PublishAsConnectionString()
        .WithEndpoint(port: 8161, targetPort: 8161, name: "web", scheme: "http");
```

### Example 2: ActiveMQ with activemq scheme for use with [MassTransit](https://masstransit.io/)

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
