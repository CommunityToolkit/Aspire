# CommunityToolkit.Aspire.Sftp

Registers an [SftpClient](https://github.com/sshnet/SSH.NET) in the DI container for connecting to SFTP servers using SSH.NET.

## Getting started

### Prerequisites

- SFTP server.

### Install the package

Install the .NET Aspire SFTP Client library with [NuGet](https://www.nuget.org):

```dotnetcli
dotnet add package CommunityToolkit.Aspire.Sftp
```

## Usage example

In the _Program.cs_ file of your project, call the `AddSftpClient` extension method to register an `SftpClient` for use via the dependency injection container. The method takes a connection name parameter.

```csharp
builder.AddSftpClient("sftp");
```

## Configuration

The .NET Aspire SFTP Client integration provides multiple options to configure the server connection based on the requirements and conventions of your project.

### Use a connection string

When using a connection string from the `ConnectionStrings` configuration section, you can provide the name of the connection string when calling `builder.AddSftpClient()`:

```csharp
builder.AddSftpClient("sftp");
```

And then the connection string will be retrieved from the `ConnectionStrings` configuration section:

```json
{
    "ConnectionStrings": {
        "sftp": "sftp://localhost:22"
    }
}
```

### Use configuration providers

The .NET Aspire SFTP Client integration supports [Microsoft.Extensions.Configuration](https://learn.microsoft.com/dotnet/api/microsoft.extensions.configuration). It loads the `SftpSettings` from configuration by using the `Aspire:Sftp:Client` key. Example `appsettings.json` that configures some of the options:

```json
{
  "Aspire": {
    "Sftp": {
      "Client": {
        "ConnectionString": "sftp://localhost:22",
        "Username": "admin",
        "Password": "password",
        "DisableHealthChecks": false
      }
    }
  }
}
```

### Use inline delegates

Also you can pass the `Action<SftpSettings> configureSettings` delegate to set up some or all the options inline:

```csharp
builder.AddSftpClient("sftp", settings => 
{
    settings.Username = "admin";
    settings.Password = "password";
});
```

### Authentication options

The SFTP client supports two authentication methods:

1. **Password authentication**: Provide `Username` and `Password`.
2. **Private key authentication**: Provide `Username` and `PrivateKeyFile` path.

```json
{
  "Aspire": {
    "Sftp": {
      "Client": {
        "ConnectionString": "sftp://localhost:22",
        "Username": "admin",
        "PrivateKeyFile": "/path/to/private/key"
      }
    }
  }
}
```

### Keyed services

The integration also supports keyed services for multiple SFTP connections:

```csharp
builder.AddKeyedSftpClient("sftp1");
builder.AddKeyedSftpClient("sftp2");
```

Then inject the keyed client:

```csharp
public class MyService
{
    private readonly SftpClient _client1;
    private readonly SftpClient _client2;

    public MyService(
        [FromKeyedServices("sftp1")] SftpClient client1,
        [FromKeyedServices("sftp2")] SftpClient client2)
    {
        _client1 = client1;
        _client2 = client2;
    }
}
```

## AppHost extensions

In your AppHost project, install the `CommunityToolkit.Aspire.Hosting.Sftp` library with [NuGet](https://www.nuget.org):

```dotnetcli
dotnet add package CommunityToolkit.Aspire.Hosting.Sftp
```

Then, in the _Program.cs_ file of `AppHost`, register SFTP and consume the connection using the following methods:

```csharp
var sftp = builder.AddSftp("sftp");

var myService = builder.AddProject<Projects.MyService>()
                       .WithReference(sftp);
```

The `WithReference` method configures a connection in the `MyService` project named `sftp`. In the _Program.cs_ file of `MyService`, the SFTP connection can be consumed using:

```csharp
builder.AddSftpClient("sftp");
```

Then, in your service, inject `SftpClient` and use it to interact with the SFTP server:

```csharp
public class MyService
{
    private readonly SftpClient _client;

    public MyService(SftpClient client)
    {
        _client = client;
        if (!_client.IsConnected)
        {
            _client.Connect();
        }
    }

    public void UploadFile(string localPath, string remotePath)
    {
        using var fileStream = File.OpenRead(localPath);
        _client.UploadFile(fileStream, remotePath);
    }

    public void DownloadFile(string remotePath, string localPath)
    {
        using var fileStream = File.Create(localPath);
        _client.DownloadFile(remotePath, fileStream);
    }
}
```

## Additional documentation

- https://github.com/sshnet/SSH.NET
- https://learn.microsoft.com/dotnet/aspire/community-toolkit/hosting-sftp

## Feedback & contributing

https://github.com/CommunityToolkit/Aspire
