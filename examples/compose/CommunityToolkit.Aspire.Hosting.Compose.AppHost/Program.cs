var builder = DistributedApplication.CreateBuilder(args);

var infra = builder.AddCompose<Compose.Infra>();
var monitoring = builder.AddCompose<Compose.Monitoring>();

_ = infra.Postgres;
_ = infra.Redis;
_ = infra.Kafka;
_ = monitoring.Grafana;
_ = monitoring.Prometheus;

// var infra = builder.AddCompose(".infra/compose.yml");
// var monitoring = builder.AddCompose(".infra/compose.monitoring.yml");
// _ = infra["postgres"];
// _ = infra["redis"];
// _ = infra["kafka"];
// _ = monitoring["grafana"];
// _ = monitoring["prometheus"];

builder.Build().Run();