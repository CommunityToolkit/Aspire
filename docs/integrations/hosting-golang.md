# CommunityToolkit.Hosting.Golang

## Overview

This is a .NET Aspire Integration for [Go](https://go.dev/) applications.

## Usage

```csharp
var builder = DistributedApplication.CreateBuilder(args);

var golang = builder.AddGolangApp("golang", "../gin-api");

builder.Build().Run();
```

### Configuration
- `name`- The name of the resource.
- `workingDirectory`- The working directory to use for the command. If null, the working directory of the current process is used.
- `port`- This is the port that will be given to other resource to communicate with this resource. Deafults to `8080`.
- `args`- The optinal arguments to be passed to the executable when it is started.

### OpenTelemetry Configuration

In the [example](../../examples/golang/) folder, you can find an example of how to configure OpenTelemetry in the Go application to use the Aspire dashboard.