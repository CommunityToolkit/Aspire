# CommunityToolkit.Aspire.Hosting.Bun library

Provides extension methods and resource definitions for an Aspire AppHost to configure a Bun project.

## Getting Started

### Install the package

In your AppHost project, install the package using the following command:

```dotnetcli
dotnet add package CommunityToolkit.Aspire.Hosting.Bun
```

### Example usage

Then, in the _Program.cs_ file of `AppHost`, define a Bun resource, then call `AddBunApp`:

```csharp
builder.AddBunApp("bun-server", "main.ts")
    .WithHttpEndpoint(env: "PORT")
    .WithEndpoint();
```

## Additional Information

https://learn.microsoft.com/dotnet/aspire/community-toolkit/hosting-bun

## Feedback & contributing

https://github.com/CommunityToolkit/Aspire
