import { createBuilder } from './.modules/aspire.js';

const builder = await createBuilder();

// addMongoDB + withDbGate — default DbGate container name
const mongo = await builder.addMongoDB("mongo");
const mongoWithDbGate = await mongo.withDbGate();
await mongoWithDbGate.addDatabase("catalog");
await mongoWithDbGate.withDataVolume({ name: "mongo-data" });

// addMongoDB + withDbGate — explicit DbGate container name
const mongoWithNamedDbGate = await builder.addMongoDB("mongo-named");
await mongoWithNamedDbGate.withDbGate({ containerName: "mongo-ui" });
await mongoWithNamedDbGate.addDatabase("orders", { databaseName: "orders-db" });

await builder.build().run();
