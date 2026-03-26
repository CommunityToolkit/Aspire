import { ContainerLifetime, createBuilder } from './.modules/aspire.js';

const builder = await createBuilder();
const runtimeSmoke = process.env.ASPIRE_RUNTIME_SMOKE === '1';

const redisPassword = await builder.addParameterWithValue('redis-password', 'redis-password-value', { secret: true });
const redis = await builder.addRedis('cache', { password: redisPassword });

if (runtimeSmoke) {
    await redis.withDbGate({ containerName: 'cache-dbgate' });
} else {
    await redis.withDbGate({
        containerName: 'cache-dbgate',
        configureContainer: async (dbgate) => {
            await dbgate.withEnvironment('REDIS_EXTENSIONS_VALIDATION', '1');
            await dbgate.withLifetime(ContainerLifetime.Session);
        }
    });
}

await builder.build().run();