# CommunityToolkit.Hosting.PapercutStmp

## Overview

This .NET Aspire Integration runs [Papercut SMTP](https://github.com/ChangemakerStudios/Papercut-SMTP) in a container.


## Usage

The Papercut SMTP integration exposes a connection string with the format `endpoint=smtp://<host>:<port>`.
This connection string can be used to with a DbConnectionStringBuilder to get the smtp endpoint.

### Example 1: Add Papercut SMTP with generated ports

```csharp
var builder = DistributedApplication.CreateBuilder(args);

var papercut = builder.AddPapercutSmtp("papercut");

var xyz = builder.AddProject<Xyz>("application")
    .WithReference(papercut)
    .WaitFor(papercut);

builder.Build().Run();
```

### Example 2: Add Papercut SMTP with user-defined ports

```csharp
var builder = DistributedApplication.CreateBuilder(args);

var papercut = builder.AddPapercutSmtp("papercut", 80, 25);

var xyz = builder.AddProject<Xyz>("application")
    .WithReference(papercut)
    .WaitFor(papercut);

builder.Build().Run();
```
