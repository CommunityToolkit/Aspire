using CommunityToolkit.Aspire.Hosting.Apache.Tika;

var builder = DistributedApplication.CreateBuilder(args);

builder.AddApacheTika("tika");

builder.Build().Run();
