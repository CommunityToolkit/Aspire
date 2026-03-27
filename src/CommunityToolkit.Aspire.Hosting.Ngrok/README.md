# CommunityToolkit.Aspire.Hosting.Ngrok library

Provides extension methods and resource definitions for an Aspire AppHost to configure a ngrok container.

## Getting Started

### Install the package

In your AppHost project, install the package using the following command:

```dotnetcli
dotnet add package CommunityToolkit.Aspire.Hosting.Ngrok
```

### Example usage

Then, in the _Program.cs_ file of app host, add a ngrok resource and add endpoints to be tunneled following methods:

```csharp
var myService = builder.AddProject<Projects.MyService>();
var otherSevice = builder.AddProject<Projects.OtherService>();

var authToken = builder
    .AddParameter("ngrok-auth-token", "your-ngrok-auth-token", secret: true);

builder.AddNgrok("ngrok", endpointPort: 59600) // omit endpointPort to use random port
    .WithAuthToken(authToken)
    .WithTunnelEndpoint(myService, "http", "<your-ngrok-domain>")
    .WithTunnelEndpoint(otherSevice, "http"); // ngrok will generate a random domain for this service
```

### Querying the ngrok tunneled endpoints

After the ngrok container has started, you can query the ngrok tunneled endpoints using api exposed by the ngrok container:

```bash
curl -H "Accept: application/json" -s http://localhost:59600/api/tunnels
```
This will return a JSON response with the ngrok tunneled endpoints.

```json
{
  "tunnels": [
    {
      "name": "my-http",
      "ID": "5baa78f84cffb31a96cccf5bbe992451",
      "uri": "/api/tunnels/my-http",
      "public_url": "https://<your-ngrok-domain>",
      "proto": "https",
      "config": {
        "addr": "http://host.docker.internal:5165",
        "inspect": true
      },
      // ...
    }, {
      "name": "other-http",
      "ID": "f7f1351d1307e3615ca7de310bf6bb61",
      "uri": "/api/tunnels/other-http",
      "public_url": "https://0849-94-134-176-242.ngrok-free.app",
      "proto": "https",
      "config": {
          "addr": "http://host.docker.internal:3657",
          "inspect": true
      },
      // ...
    }
  ],
  "uri": "/api/tunnels"
}
```

## Additional Information

- https://ngrok.com
- https://learn.microsoft.com/dotnet/aspire/community-toolkit/ngrok

## Feedback & contributing

- https://github.com/CommunityToolkit/Aspire