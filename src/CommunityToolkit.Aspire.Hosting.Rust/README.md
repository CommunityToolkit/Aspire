# CommunityToolkit.Aspire.Hosting.Rust library

Provides an Aspire AppHost extension for running Rust applications with [Bacon](https://dystroy.org/bacon/), using Aspire's Rust resource.

## Getting Started

### Install the package

In your AppHost project, install the package using the following command:

```dotnetcli
dotnet add package CommunityToolkit.Aspire.Hosting.Rust
```

### Example usage

Install Bacon and call `AddBaconApp` from your AppHost:

```csharp
var rustApp = builder.AddBaconApp("rust-app", "../actix_api");
```

## Additional Information

For applications using Cargo directly, use the upstream `Aspire.Hosting.Rust` package and its `AddRustApp` method instead.

## Feedback & contributing

https://github.com/CommunityToolkit/Aspire

