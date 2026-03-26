import { createBuilder } from './.modules/aspire.js';

const builder = await createBuilder();
const sqlPassword = await builder.addParameterWithValue('sql-password', 'SqlServer_Pass123!', { secret: true });
const sqlServer = builder
    .addSqlServer('sqlserver', { password: sqlPassword })
    .withDbGate({
        containerName: 'dbgate',
        imageTag: '6.1.4',
    })
    .withAdminer({
        containerName: 'sqlserver-adminer',
        imageTag: '5.1.0',
    });

const _resolvedSqlServer = await sqlServer;

await builder.build().run();
