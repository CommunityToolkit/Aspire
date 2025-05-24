var builder = DistributedApplication.CreateBuilder(args);

var postgres1 = builder.AddPostgres("postgres1")
    .WithAdminer();
postgres1.AddDatabase("db1");
postgres1.AddDatabase("db2");

var postgres2 = builder.AddPostgres("postgres2")
    .WithAdminer();
postgres2.AddDatabase("db3");
postgres2.AddDatabase("db4");

var sqlserver1 = builder.AddSqlServer("sqlserver1")
    .WithAdminer();
sqlserver1.AddDatabase("db5");
sqlserver1.AddDatabase("db6");

var sqlserver2 = builder.AddSqlServer("sqlserver2")
    .WithAdminer();
sqlserver2.AddDatabase("db7");
sqlserver2.AddDatabase("db8");

var mysql1 = builder.AddMySql("mysql1")
    .WithAdminer();
mysql1.AddDatabase("db9");
mysql1.AddDatabase("db10");

var mysql2 = builder.AddMySql("mysql2")
    .WithAdminer();
mysql2.AddDatabase("db11");
mysql2.AddDatabase("db12");

builder.Build().Run();