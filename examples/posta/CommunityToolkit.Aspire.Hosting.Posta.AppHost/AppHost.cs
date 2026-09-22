var builder = DistributedApplication.CreateBuilder(args);

var postgresPassword = builder.AddParameter("postgres-password", "posta", secret: true);
var postgres = builder.AddPostgres("postgres", password: postgresPassword);
var database = postgres.AddDatabase("posta-db", "posta");
var redis = builder.AddRedis("redis");

var inboundEnabled = builder.AddParameter("posta-inbound-enabled", "false");
var relayEnabled = builder.AddParameter("posta-relay-enabled", "false");

var posta = builder.AddPosta("posta", database, redis)
    .WithDataVolume()
    .WithInboundSmtp(options => options.Enabled = inboundEnabled)
    .WithSmtpRelay(options => options.Enabled = relayEnabled);

// System SMTP sends Posta platform notifications through an external server.
// Configure it when an SMTP server is available:
// var smtpPassword = builder.AddParameter("posta-system-smtp-password", secret: true);
// posta.WithSystemSmtp(options =>
// {
//     options.Host = builder.AddParameter("posta-system-smtp-host", "smtp.example.com");
//     options.Port = builder.AddParameter("posta-system-smtp-port", "587");
//     options.Username = builder.AddParameter("posta-system-smtp-username", "notifications@example.com");
//     options.Password = smtpPassword;
//     options.From = builder.AddParameter("posta-system-smtp-from", "notifications@example.com");
//     options.Encryption = builder.AddParameter("posta-system-smtp-encryption", "starttls");
// });

builder.Build().Run();
