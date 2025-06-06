# CommunityToolkit.Hosting.Apache.Tika

## Overview

This .NET Aspire Integration can be used to host [Apache Tika](https://github.com/apache/tika-docker) in a container.

## Usage

### Example 1: Apache Tika hosting with default tcp scheme

```csharp
var builder = DistributedApplication.CreateBuilder(args);

var tika = builder.AddApacheTika("tika");
```
