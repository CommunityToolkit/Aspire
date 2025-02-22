var builder = DistributedApplication.CreateBuilder(args);

var sqlserver1 = builder.AddSqlServer("sqlserver1")
    .WithDbGate(c => c.WithHostPort(8068));
sqlserver1.AddDatabase("db1");
sqlserver1.AddDatabase("db2");

var sqlserver2 = builder.AddSqlServer("sqlserver2")
    .WithDbGate();
sqlserver2.AddDatabase("db3");
sqlserver2.AddDatabase("db4");

builder.Build().Run();
