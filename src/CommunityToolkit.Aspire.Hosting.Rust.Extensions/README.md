# CommunityToolkit.Aspire.Hosting.Rust.Extensions library

Provides an Aspire AppHost extension for running Rust applications with [Bacon](https://dystroy.org/bacon/). Bacon does not require the upstream `Aspire.Hosting.Rust` package.

## Getting Started

### Install the package

In your AppHost project, install the package using the following command:

```dotnetcli
dotnet add package CommunityToolkit.Aspire.Hosting.Rust.Extensions
```

### Example usage

Install Bacon and call `AddBaconApp` from your AppHost:

```csharp
var rustApp = builder.AddBaconApp("rust-app", "../actix_api");
```

## Additional Information

For applications using Cargo directly, use the upstream `Aspire.Hosting.Rust` package and its `AddRustApp` method instead. You can use Cargo and Bacon in the same AppHost by referencing both packages.

`AddBaconApp` returns `IResourceBuilder<RustAppExecutableResource>`, the same Toolkit type as in 13.6.1. If you used the upstream `RustAppResource` type explicitly with a Toolkit 17.0.0 prerelease, switch that declaration to `RustAppExecutableResource` (in `Aspire.Hosting.ApplicationModel`) or use `var`, then rebuild your AppHost.
When upgrading, replace the `CommunityToolkit.Aspire.Hosting.Rust` package reference with `CommunityToolkit.Aspire.Hosting.Rust.Extensions`; do not reference both Toolkit packages.

The Rust examples accept `ENABLE_BACON=1` to launch Bacon automatically. The Go and TypeScript AppHosts otherwise register Bacon for manual start. Install Cargo, rustc, and Bacon to run the Rust AppHost startup tests.

## Feedback & contributing

https://github.com/CommunityToolkit/Aspire

