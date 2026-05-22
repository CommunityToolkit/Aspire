import { createBuilder } from "./.modules/aspire.js";

const builder = await createBuilder();

const mysql = await builder.addMySql("mysql");

await mysql
    .withAdminer({
        configureContainer: async (_adminer) => {},
        containerName: "mysql-adminer",
    })
    .withDbGate({
        configureContainer: async (_dbGate) => {},
        containerName: "mysql-dbgate",
    });

const _primaryEndpoint = await mysql.primaryEndpoint();
const _host = await mysql.host();
const _port = await mysql.port();
const _connectionString = await mysql.connectionStringExpression();

await builder.build().run();
