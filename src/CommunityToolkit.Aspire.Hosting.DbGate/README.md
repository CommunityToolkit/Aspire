# CommunityToolkit.Aspire.Hosting.DbGate library

Provides extension methods and resource definitions for the .NET Aspire AppHost to support running [DbGate](https://dbgate.org) containers.

## Getting Started

### Install the package

In your AppHost project, install the package using the following command:

```dotnetcli
dotnet add package CommunityToolkit.Aspire.Hosting.DbGate
```

### Example usage

Then, in the _Program.cs_ file of `AppHost`, define an DbGate resource, then call `AddDbGate`:

```csharp
var builder = DistributedApplication.CreateBuilder(args);

var dbgate = builder.AddDbGate("dbgate");

builder.Build().Run();
```

## Additional Information

https://learn.microsoft.com/dotnet/aspire/community-toolkit/hosting-meilisearch

## Feedback & contributing

https://github.com/CommunityToolkit/Aspire

## Remarks
- Multiple `AddDbGate` calls will return the same resource builder instance.
- This package is designed to be used internally by the community toolkit and is not intended to be used directly in the application code.
