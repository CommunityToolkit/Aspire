import { createBuilder } from './.modules/aspire.js';

const builder = await createBuilder();

const mysql = await builder.addMySql('mysql');

await mysql
    .withAdminer({
        configureContainer: async (_adminer) => { },
        containerName: 'mysql-adminer'
    })
    .withDbGate({
        configureContainer: async (_dbGate) => { },
        containerName: 'mysql-dbgate'
    });

const resolvedMySql = await mysql;
const _primaryEndpoint = await resolvedMySql.primaryEndpoint.get();
const _host = await resolvedMySql.host.get();
const _port = await resolvedMySql.port.get();
const _connectionString = await resolvedMySql.connectionStringExpression.get();

await builder.build().run();
