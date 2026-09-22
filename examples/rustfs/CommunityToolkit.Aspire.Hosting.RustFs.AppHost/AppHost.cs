var builder = DistributedApplication.CreateBuilder(args);

var accessKey = builder.AddParameter("accessKey", "rustfsadmin");
var secretKey = builder.AddParameter("secretKey", "rustfsadmin", secret: true);

var rustfs = builder.AddRustFs("rustfs", accessKey, secretKey)
    .WithDataVolume()
    .AddBucket("mybucket");

builder.Build().Run();
