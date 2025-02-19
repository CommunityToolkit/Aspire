# CommunityToolkit.Hosting.MailPit

## Overview

This .NET Aspire Integration runs [MailPit](https://github.com/axllent/mailpit) in a container.


## Usage

The MailPit integration exposes a connection string with the format `endpoint=smtp://<host>:<port>`.
This connection string can be used to with a DbConnectionStringBuilder to get the smtp endpoint.

### Example 1: Add MailPit with generated ports

```csharp
var builder = DistributedApplication.CreateBuilder(args);

var mailpit = builder.AddMailPit("mailpit");

var xyz = builder.AddProject<Xyz>("application")
    .WithReference(mailpit)
    .WaitFor(mailpit);

builder.Build().Run();
```

### Example 2: Add MailPit with user-defined ports

```csharp
var builder = DistributedApplication.CreateBuilder(args);

var mailpit = builder.AddMailPit("mailpit", 80, 25);

var xyz = builder.AddProject<Xyz>("application")
    .WithReference(mailpit)
    .WaitFor(mailpit);

builder.Build().Run();
```
