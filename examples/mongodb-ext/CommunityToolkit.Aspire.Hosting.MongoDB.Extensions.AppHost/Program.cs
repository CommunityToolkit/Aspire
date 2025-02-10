var builder = DistributedApplication.CreateBuilder(args);

var mongodb1 = builder.AddMongoDB("mongodb1").WithDbGate(c => c.WithHostPort(8090));
mongodb1.AddDatabase("db1");
mongodb1.AddDatabase("db2");

var mongodb2 = builder.AddMongoDB("mongodb2").WithDbGate();
mongodb2.AddDatabase("db3");
mongodb2.AddDatabase("db4");

builder.Build().Run();
