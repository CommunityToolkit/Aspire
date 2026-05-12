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
const _primaryEndpoint = await resolvedMySql.getEndpoint("tcp");
const _host = await _primaryEndpoint.host.get();
const _port = await _primaryEndpoint.port.get();
const _connectionString = await resolvedMySql.connectionStringExpression.get();

await builder.build().run();
