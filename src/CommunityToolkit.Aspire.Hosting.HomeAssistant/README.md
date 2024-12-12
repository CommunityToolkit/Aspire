# CommunityToolkit.Aspire.Hosting.HomeAssistant library

Provides extension methods and resource definitions for the .NET Aspire AppHost to support running [HomeAssistant](https://www.home-assistant.io/) containers.

## Getting Started

### Install the package

In your AppHost project, install the package using the following command:

```dotnetcli
dotnet add package CommunityToolkit.Aspire.Hosting.HomeAssistant
```

### Example usage

Then, in the _Program.cs_ file of `AppHost`, define an HomeAssistant resource, then call `AddHomeAssistant`:

```csharp
var homeAssistant = builder.AddHomeAssistant("home-assistant")
    .WithDataVolume()
    .WithLifetime(ContainerLifetime.Persistant);
```

## Additional Information

https://learn.microsoft.com/dotnet/aspire/community-toolkit/hosting-home-assistant

## Feedback & contributing

https://github.com/CommunityToolkit/Aspire

