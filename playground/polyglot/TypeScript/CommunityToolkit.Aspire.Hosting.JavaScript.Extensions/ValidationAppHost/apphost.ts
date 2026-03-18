import path from 'node:path';
import { fileURLToPath } from 'node:url';
import { createBuilder } from './.modules/aspire.js';

const builder = await createBuilder();
const appHostDirectory = path.dirname(fileURLToPath(import.meta.url));
const repoRoot = process.env.CT_ASPIRE_REPO_ROOT ?? path.resolve(appHostDirectory, '..', '..', '..', '..', '..');
const examplesRoot = path.join(repoRoot, 'examples', 'javascript-ext');
const nxDemoRoot = path.join(examplesRoot, 'nx-demo');
const turborepoDemoRoot = path.join(examplesRoot, 'turborepo-demo');

const nx = await builder.addNxApp('nx-demo', { workingDirectory: nxDemoRoot });
await nx.withNpm({ install: true });
await nx.withPackageManagerLaunch();

const nxBlog = await nx.addApp('blog-monorepo');
await nxBlog.withHttpEndpoint({ env: 'PORT' });
await nxBlog.withMappedEndpointPort();
await nxBlog.withHttpHealthCheck();

const nxYarn = await builder.addNxApp('nx-yarn', { workingDirectory: nxDemoRoot });
await nxYarn.withYarn();
await nxYarn.withPackageManagerLaunch({ packageManager: 'yarn' });
await nxYarn.withExplicitStart();

const nxBlogWithOptions = await nxYarn.addApp('blog-monorepo-yarn', { appName: 'blog-monorepo' });
await nxBlogWithOptions.withHttpEndpoint({ env: 'PORT' });
await nxBlogWithOptions.withMappedEndpointPort({ endpointName: 'http' });
await nxBlogWithOptions.withExplicitStart();

const nxPnpm = await builder.addNxApp('nx-pnpm', { workingDirectory: nxDemoRoot });
await nxPnpm.withPnpm();
await nxPnpm.withPackageManagerLaunch({ packageManager: 'pnpm' });
await nxPnpm.withExplicitStart();

const turbo = await builder.addTurborepoApp('turborepo-demo', { workingDirectory: turborepoDemoRoot });
await turbo.withNpm({ install: true });
await turbo.withPackageManagerLaunch();

const turboWeb = await turbo.addApp('turbo-web', { filter: 'web' });
await turboWeb.withHttpEndpoint({ env: 'PORT' });
await turboWeb.withMappedEndpointPort();
await turboWeb.withHttpHealthCheck();

const turboYarn = await builder.addTurborepoApp('turborepo-yarn', { workingDirectory: turborepoDemoRoot });
await turboYarn.withYarn();
await turboYarn.withPackageManagerLaunch({ packageManager: 'yarn' });
await turboYarn.withExplicitStart();

const turboDocs = await turboYarn.addApp('docs');
await turboDocs.withHttpEndpoint({ env: 'PORT' });
await turboDocs.withMappedEndpointPort({ endpointName: 'http' });
await turboDocs.withExplicitStart();

const turboPnpm = await builder.addTurborepoApp('turborepo-pnpm', { workingDirectory: turborepoDemoRoot });
await turboPnpm.withPnpm();
await turboPnpm.withPackageManagerLaunch({ packageManager: 'pnpm' });
await turboPnpm.withExplicitStart();

await builder.build().run();
