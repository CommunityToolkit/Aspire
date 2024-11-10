# CommunityToolkit.Aspire.Hosting.Rust library

Provides extensions methods and resource definitions for the .NET Aspire AppHost to support running Rust applications.

## Getting Started

### Install the package

In your AppHost project, install the package using the following command:

```dotnetcli
dotnet add package CommunityToolkit.Aspire.Hosting.Rust
```

### Example usage

Then, in the _Program.cs_ file of `AppHost`, define a Rust resource, then call `AddRustApp`:

```csharp
var golang = builder.AddRustApp("rust-app", "../actix_api");
```

## Additional Information

https://learn.microsoft.com/dotnet/aspire/community-toolkit/hosting-rust

## Feedback & contributing

https://github.com/CommunityToolkit/Aspire

