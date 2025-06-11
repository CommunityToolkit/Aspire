# CommunityToolkit.Aspire.Hosting.HashiCorp.Vault library

Provides extension methods and resource definitions for the .NET Aspire AppHost to support running [HeshiCorp Vault](https://www.hashicorp.com/en/products/vault) containers.

## Getting Started

### Install the package

In your AppHost project, install the package using the following command:

```dotnetcli
dotnet add package CommunityToolkit.Aspire.Hosting.HashiCorp.Vault
```

### Example usage

Then, in the _Program.cs_ file of `AppHost`, add a Vault resource and consume the connection using the following methods:

```csharp
var builder = DistributedApplication.CreateBuilder(args);

var vault = builder.AddVault("vault");

var myService = builder.AddProject<Projects.MyService>()
                       .WithReference(vault);

builder.Build().Run();
```

## Additional Information



## Feedback & contributing

https://github.com/CommunityToolkit/Aspire

