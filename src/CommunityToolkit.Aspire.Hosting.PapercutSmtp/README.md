# CommunityToolkit.Hosting.PapercutStmp

## Overview

This .NET Aspire Integration runs [Papercut SMTP](https://github.com/ChangemakerStudios/Papercut-SMTP) in a container.


## Usage

### Example 1: Add Papercut SMTP with generated ports

```csharp
var builder = DistributedApplication.CreateBuilder(args);

var papercut = builder.AddPapercutSmtp("papercut");

builder.Build().Run();
```

### Example 2: Add Papercut SMTP with user-defined ports

```csharp
var builder = DistributedApplication.CreateBuilder(args);

var papercut = builder.AddPapercutSmtp("papercut", 80, 25);

builder.Build().Run();
```
