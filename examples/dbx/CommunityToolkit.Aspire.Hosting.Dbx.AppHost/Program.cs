var builder = DistributedApplication.CreateBuilder(args);

var postgres1 = builder.AddPostgres("postgres1")
    .WithDbx(c => c.WithHostPort(8068));
postgres1.AddDatabase("db1");
postgres1.AddDatabase("db2");

var postgres2 = builder.AddPostgres("postgres2")
    .WithDbx();
postgres2.AddDatabase("db3");
postgres2.AddDatabase("db4");

var mongodb1 = builder.AddMongoDB("mongodb1").WithDbx();
mongodb1.AddDatabase("db5");
mongodb1.AddDatabase("db6");

var mongodb2 = builder.AddMongoDB("mongodb2").WithDbx();
mongodb2.AddDatabase("db7");
mongodb2.AddDatabase("db8");

var redis1 = builder.AddRedis("redis1").WithDbx();
var redis2 = builder.AddRedis("redis2").WithDbx();

var sqlserver1 = builder.AddSqlServer("sqlserver1")
    .WithDbx(c => c.WithHostPort(8068));
sqlserver1.AddDatabase("db9");
sqlserver1.AddDatabase("db10");

var sqlserver2 = builder.AddSqlServer("sqlserver2")
    .WithDbx();
sqlserver2.AddDatabase("db11");
sqlserver2.AddDatabase("db12");

var mysql1 = builder.AddMySql("mysql1")
    .WithDbx(c => c.WithHostPort(8068));
mysql1.AddDatabase("db13");
mysql1.AddDatabase("db14");

var mysql2 = builder.AddMySql("mysql2")
    .WithDbx();
mysql2.AddDatabase("db15");
mysql2.AddDatabase("db16");

builder.Build().Run();