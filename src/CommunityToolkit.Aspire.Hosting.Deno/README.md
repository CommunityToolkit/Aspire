# CommunityToolkit.Aspire.Hosting.Deno library

Provides extension methods and resource definitions for a .NET Aspire AppHost to configure a Deno project.

## Getting Started

### Install the package

In your AppHost project, install the package using the following command:

```dotnetcli
dotnet add package CommunityToolkit.Aspire.Hosting.Deno
```

### Example usage

Then, in the _Program.cs_ file of `AppHost`, define a Deno resource, then call `AddDenoApp` or `AddDenoTask`:

```csharp
builder.AddDenoTask("vite-demo", taskName: "dev")
    .WithHttpEndpoint(env: "PORT")
    .WithEndpoint();

builder.AddDenoApp("oak-demo", "main.ts", permissionFlags: ["-E", "--allow-net"])
    .WithHttpEndpoint(env: "PORT")
    .WithEndpoint();
```

## Additional Information

https://learn.microsoft.com/dotnet/aspire/community-toolkit/deno

## Feedback & contributing

https://github.com/CommunityToolkit/Aspire

