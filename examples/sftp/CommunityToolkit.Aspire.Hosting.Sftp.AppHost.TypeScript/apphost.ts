import { dirname, join } from 'node:path';
import { fileURLToPath } from 'node:url';

import { createBuilder, KeyType } from './.modules/aspire.js';

const currentDirectory = dirname(fileURLToPath(import.meta.url));
const appHostFixtureDirectory = join(currentDirectory, '..', 'CommunityToolkit.Aspire.Hosting.Sftp.AppHost');
const usersFile = join(appHostFixtureDirectory, 'etc', 'sftp', 'users.conf');
const hostKeyFile = join(appHostFixtureDirectory, 'etc', 'ssh', 'ssh_host_ed25519_key');
const userKeyFile = join(appHostFixtureDirectory, 'home', 'foo', '.ssh', 'keys', 'id_ed25519.pub');

const builder = await createBuilder();

// addSftp — explicit port
const sftp = await builder.addSftp('sftp', { port: 2222 });

// withUsersFile / withHostKeyFile / withUserKeyFile — keep export coverage without
// requiring checked-in SSH fixtures for runtime smoke.
if (process.env.ASPIRE_RUNTIME_SMOKE === '1') {
    await sftp.withEnvironment('SFTP_USERS', 'foo:pass:::upload');
} else {
    await sftp.withUsersFile(usersFile);
    await sftp.withHostKeyFile(hostKeyFile, KeyType.Ed25519);
    await sftp.withUserKeyFile('foo', userKeyFile, KeyType.Ed25519);
}

// addSftp — defaults
const sftpDefaults = await builder.addSftp('sftp-defaults');
await sftpDefaults.withEnvironment('SFTP_USERS', 'bar:pass:::upload');

if (process.env.ASPIRE_RUNTIME_SMOKE !== '1') {
    // ---- Property access on SftpContainerResource (ExposeProperties = true) ----
    const sftpResource = await sftp;
    const _host = await sftpResource.host.get();
    const _port = await sftpResource.port.get();
    const _uri = sftpResource.uriExpression;
    const _connectionString = sftpResource.connectionStringExpression;

    const sftpDefaultsResource = await sftpDefaults;
    const _defaultHost = await sftpDefaultsResource.host.get();
    const _defaultPort = await sftpDefaultsResource.port.get();
    const _defaultUri = sftpDefaultsResource.uriExpression;
    const _defaultConnectionString = sftpDefaultsResource.connectionStringExpression;
}

await builder.build().run();
