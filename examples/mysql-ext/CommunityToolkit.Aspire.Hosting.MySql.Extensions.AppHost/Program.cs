var builder = DistributedApplication.CreateBuilder(args);

var mysql1 = builder.AddMySql("mysql1")
    .WithAdminer(c => c.WithHostPort(8989))
    .WithDbGate(c => c.WithHostPort(9999));
mysql1.AddDatabase("db1");
mysql1.AddDatabase("db2");

var mysql2 = builder.AddMySql("mysql2")
    .WithAdminer(c => c.WithHostPort(8989));
mysql2.AddDatabase("db3");
mysql2.AddDatabase("db4");

builder.Build().Run();
