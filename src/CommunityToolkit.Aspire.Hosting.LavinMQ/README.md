# CommunityToolkit.Hosting.LavinMQ

## Overview

This .NET Aspire Integration can be used to include [LavinMQ](https://lavinmq.com/) in a container.

## Usage

### Example 1: LavinMQ with default ports

```csharp
var builder = DistributedApplication.CreateBuilder(args);

var lavinmq = builder.AddLavinMQ("lavinmq");
```

### Example 2: LavinMQ with custom ports

```csharp
var builder = DistributedApplication.CreateBuilder(args);

var lavinmq = builder.AddLavinMQ("lavinmq", amqpPort: 5672, managementPort: 15672);
```
