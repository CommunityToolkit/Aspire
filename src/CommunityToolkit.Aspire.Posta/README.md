# Posta client integration

Use this integration to call the Posta HTTP API through an `HttpClient` configured with Aspire service discovery.

## Getting started

```shell
dotnet add package CommunityToolkit.Aspire.Posta
```

Register the client in the consuming project:

```csharp
builder.AddPostaClient("posta", settings =>
{
    settings.ApiKey = builder.Configuration["Posta:ApiKey"];
});
```

The hosting resource exposes an `Endpoint=...` connection string, so an AppHost can use `.WithReference(posta)`.

## Calling an API section

The client intentionally covers the application-facing email, template, subscriber-list, health, and information APIs. These areas are exposed through separate interfaces.

```csharp
using CommunityToolkit.Aspire.Posta.Models.Emails;
using CommunityToolkit.Aspire.Posta.Clients;

public sealed class MailService(IPostaClient posta)
{
    public Task<SendAnEmailResponse?> SendAsync(CancellationToken cancellationToken) =>
        posta.Emails.SendAnEmailAsync(
            new SendAnEmailRequest
            {
                From = "Acme <hello@example.com>",
                To = ["user@example.com"],
                Subject = "Hello",
                Html = "<h1>Hello!</h1>"
            },
            cancellationToken);
}
```

Path and query parameters are supplied separately and are escaped by the client:

```csharp
await posta.Emails.GetEmailDetailsAsync(
    new GetEmailDetailsRequest
    {
        Id = emailId
    },
    cancellationToken);
```

API-key and JWT operations select the appropriate configured credential automatically. A dynamic application-wide implementation can replace `CommunityToolkit.Aspire.Posta.Configuration.IPostaCredentialProvider` in DI.

For multiple Posta instances, register keyed clients with `AddKeyedPostaClient(...)` and resolve them through `GetRequiredKeyedService<IPostaClient>(key)`.

## Request and response models

Models are grouped by API area under `CommunityToolkit.Aspire.Posta.Models`, for example `Models.Emails`, `Models.Templates`, and `Models.SubscriberLists`. Reusable nested schemas are under `Models.Shared`. JSON property names, required members, path parameters, and query parameters follow Posta's OpenAPI document.

Administrative, OAuth, workspace-management, analytics, campaign, and other uncommon APIs are not duplicated in this package. They can still be called through `IPostaSectionClient.SendAsync<TResponse>` with a custom `PostaEndpoint` and `PostaRequest`.

## Overriding endpoints

Paths are not embedded in the section clients. Replace `IPostaEndpoints`, or derive from `PostaEndpoints` and override only the changed operation:

```csharp
public sealed class CustomPostaEndpoints : PostaEndpoints
{
    public override PostaEndpoint SendAnEmail { get; } =
        new(HttpMethod.Post, "/custom/v1/emails/send", PostaAuthentication.ApiKey);
}

builder.Services.AddSingleton<IPostaEndpoints, CustomPostaEndpoints>();
```

## Additional documentation

- [Posta API reference](https://app.goposta.dev/docs)
- [Posta OpenAPI document](https://app.goposta.dev/openapi.json)

## Feedback & contributing

Submit issues and pull requests through the CommunityToolkit.Aspire repository.
