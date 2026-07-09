# Posta hosting integration

Use this integration to model, configure, and orchestrate the [Posta](https://docs.goposta.dev/) self-hosted email delivery platform in an Aspire AppHost.

## Getting started

Install the package in your AppHost project:

```bash
aspire add CommunityToolkit.Aspire.Hosting.Posta
```

## Basic usage

```csharp
var builder = DistributedApplication.CreateBuilder(args);

var postgresPassword = builder.AddParameter("postgres-password", secret: true);
var postgres = builder.AddPostgres("postgres", password: postgresPassword);
var database = postgres.AddDatabase("posta-db", "posta");
var redis = builder.AddRedis("redis");

var posta = builder.AddPosta("posta", database, redis);

builder.AddProject<Projects.Api>("api")
    .WithReference(posta)
    .WaitFor(posta);

builder.Build().Run();
```

Posta stores data in PostgreSQL, uses Redis for queueing, and runs the embedded worker in the main container by default. `AddPosta` requires PostgreSQL database and Redis resources so the container starts with the dependencies it needs.

## Configuration

Use the `PostaOptions` callback when values are known in the AppHost and can be represented as typed literal values. Secret values inside `PostaOptions`, such as OAuth client secrets, S3 keys, SMTP passwords, encryption keys, inbound webhook secrets, `POSTA_DB_URL`, and Redis passwords, should still be passed as Aspire parameters.

```csharp
var encryptionKey = builder.AddParameter("posta-encryption-key", secret: true);
var smtpPassword = builder.AddParameter("posta-smtp-password", secret: true);

var posta = builder.AddPosta("posta", database, redis, options =>
{
    options.Environment = "production";
    options.WebUrl = "https://mail.example.com";
    options.ApiUrl = "https://mail.example.com";
    options.CorsOrigins = "https://app.example.com";
    options.MetricsEnabled = true;
    options.EncryptionKey = encryptionKey;

    options.SystemSmtpHost = "smtp.example.com";
    options.SystemSmtpPort = 587;
    options.SystemSmtpPassword = smtpPassword;
    options.SystemSmtpFrom = "notifications@example.com";
    options.SystemSmtpEncryption = "starttls";
});
```

For values coming from configuration, environment variables, user secrets, or publish-time parameters, use the grouped parameter-based methods. These methods accept options objects whose properties are `IResourceBuilder<ParameterResource>?`, so they work with `AddParameter` and `AddParameterFromConfiguration`.

```csharp
var smtpHost = builder.AddParameterFromConfiguration("posta-smtp-host", "Posta:Smtp:Host");
var smtpPort = builder.AddParameterFromConfiguration("posta-smtp-port", "Posta:Smtp:Port");
var smtpUsername = builder.AddParameterFromConfiguration("posta-smtp-username", "Posta:Smtp:Username");
var smtpPassword = builder.AddParameterFromConfiguration("posta-smtp-password", "Posta:Smtp:Password", secret: true);
var smtpFrom = builder.AddParameterFromConfiguration("posta-smtp-from", "Posta:Smtp:From");
var smtpEncryption = builder.AddParameterFromConfiguration("posta-smtp-encryption", "Posta:Smtp:Encryption");

var googleClientId = builder.AddParameterFromConfiguration("posta-google-client-id", "Posta:GoogleOAuth:ClientId");
var googleClientSecret = builder.AddParameterFromConfiguration("posta-google-client-secret", "Posta:GoogleOAuth:ClientSecret", secret: true);
var googleCallbackUrl = builder.AddParameterFromConfiguration("posta-google-callback-url", "Posta:GoogleOAuth:CallbackUrl");

var s3Endpoint = builder.AddParameterFromConfiguration("posta-s3-endpoint", "Posta:S3:Endpoint");
var s3Region = builder.AddParameterFromConfiguration("posta-s3-region", "Posta:S3:Region");
var s3Bucket = builder.AddParameterFromConfiguration("posta-s3-bucket", "Posta:S3:Bucket");
var s3AccessKey = builder.AddParameterFromConfiguration("posta-s3-access-key", "Posta:S3:AccessKey", secret: true);
var s3SecretKey = builder.AddParameterFromConfiguration("posta-s3-secret-key", "Posta:S3:SecretKey", secret: true);

var posta = builder.AddPosta("posta", database, redis)
    .WithSystemSmtp(options =>
    {
        options.Host = smtpHost;
        options.Port = smtpPort;
        options.Username = smtpUsername;
        options.Password = smtpPassword;
        options.From = smtpFrom;
        options.Encryption = smtpEncryption;
    })
    .WithGoogleOAuth(options =>
    {
        options.ClientId = googleClientId;
        options.ClientSecret = googleClientSecret;
        options.CallbackUrl = googleCallbackUrl;
    })
    .WithS3BlobStorage(options =>
    {
        options.Endpoint = s3Endpoint;
        options.Region = s3Region;
        options.Bucket = s3Bucket;
        options.AccessKey = s3AccessKey;
        options.SecretKey = s3SecretKey;
    });
```

You can also configure inbound SMTP and email verification through parameter-based options:

```csharp
var inboundEnabled = builder.AddParameterFromConfiguration("posta-inbound-enabled", "Posta:Inbound:Enabled");
var inboundPort = builder.AddParameterFromConfiguration("posta-inbound-port", "Posta:Inbound:Port");
var inboundWebhookSecret = builder.AddParameterFromConfiguration("posta-inbound-webhook-secret", "Posta:Inbound:WebhookSecret", secret: true);

var emailVerificationRequired = builder.AddParameterFromConfiguration("posta-email-verification-required", "Posta:EmailVerification:Required");

builder.AddPosta("posta", database, redis)
    .WithInboundSmtp(options =>
    {
        options.Enabled = inboundEnabled;
        options.Port = inboundPort;
        options.WebhookSecret = inboundWebhookSecret;
    })
    .WithEmailVerification(options =>
    {
        options.Required = emailVerificationRequired;
    });
```

## API overview

| Method | Purpose |
| --- | --- |
| `AddPosta(name, database, redis)` | Adds Posta and configures PostgreSQL and Redis references. |
| `AddPosta(name, database, redis, PostaOptions)` | Adds Posta with typed environment configuration. |
| `AddPosta(name, database, redis, Action<PostaOptions>)` | Adds Posta with callback-based typed environment configuration. |
| `WithReference(PostgresDatabaseResource)` | Configures the PostgreSQL database environment variables and waits for the database. |
| `WithReference(RedisResource, redisPassword)` | Configures Redis address/password environment variables and waits for Redis. |
| `WithSystemSmtp(PostaSystemSmtpOptions)` | Configures parameter-based system SMTP settings. |
| `WithSystemSmtp(Action<PostaSystemSmtpOptions>)` | Configures parameter-based system SMTP settings with a callback. |
| `WithInboundSmtp(PostaInboundSmtpOptions)` | Configures parameter-based inbound SMTP receiver settings. |
| `WithInboundSmtp(Action<PostaInboundSmtpOptions>)` | Configures parameter-based inbound SMTP receiver settings with a callback. |
| `WithS3BlobStorage(PostaS3BlobStorageOptions)` | Configures parameter-based S3-compatible attachment storage and sets the blob provider to `s3`. |
| `WithS3BlobStorage(Action<PostaS3BlobStorageOptions>)` | Configures parameter-based S3-compatible attachment storage with a callback and sets the blob provider to `s3`. |
| `WithGoogleOAuth(PostaGoogleOAuthOptions)` | Configures parameter-based Google OAuth login settings. |
| `WithGoogleOAuth(Action<PostaGoogleOAuthOptions>)` | Configures parameter-based Google OAuth login settings with a callback. |
| `WithEmailVerification(PostaEmailVerificationOptions)` | Configures parameter-based email verification settings. |
| `WithEmailVerification(Action<PostaEmailVerificationOptions>)` | Configures parameter-based email verification settings with a callback. |

Use `PostaOptions.DatabaseUrl`, `PostaOptions.RedisAddress`, and `PostaOptions.RedisPassword` only when you need to override the generated PostgreSQL or Redis environment values.

## Connection Properties

The Posta resource exposes the following connection properties:

| Name | Format |
| --- | --- |
| `Host` | Posta HTTP API host |
| `Port` | Posta HTTP API port |
| `Uri` | `http://{host}:{port}` |

These properties become environment variables named `[RESOURCE]__HOST`, `[RESOURCE]__PORT`, and `[RESOURCE]__URI` when referenced by another resource. The connection string uses `Endpoint=http://{host}:{port}`.

## Additional documentation

* [Posta documentation](https://docs.goposta.dev/)
* [Posta installation guide](https://docs.goposta.dev/docs/getting-started/installation/)
* [Posta configuration](https://docs.goposta.dev/docs/getting-started/configuration/)

## Feedback & contributing

https://github.com/CommunityToolkit/Aspire
