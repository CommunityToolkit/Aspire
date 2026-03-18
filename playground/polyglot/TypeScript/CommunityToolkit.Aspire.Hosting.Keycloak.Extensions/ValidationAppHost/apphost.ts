import { createBuilder, type KeycloakResource, type WithPostgresOptions } from './.modules/aspire.js';

const builder = await createBuilder();

const dbUserName = await builder.addParameterWithValue("db-username", "postgres");
const dbPassword = await builder.addParameterWithValue("db-password", "Postgres!123", { secret: true });

const postgres = await builder.addPostgres("keycloak-postgres", {
    userName: dbUserName,
    password: dbPassword
});
const db = await postgres.addDatabase("keycloak");

// Aspire.Hosting.Keycloak does not yet export addKeycloak to TypeScript, so validate the generated
// withPostgres signature directly against the generated Keycloak resource handle types.
type KeycloakWithPostgresParameters = Parameters<KeycloakResource["withPostgres"]>;

const withPostgresDefaults: KeycloakWithPostgresParameters = [db];
const withPostgresOptions: WithPostgresOptions = {
    username: dbUserName,
    password: dbPassword,
    xaEnabled: true
};
const withPostgresExplicit: KeycloakWithPostgresParameters = [db, withPostgresOptions];
void withPostgresDefaults;
void withPostgresExplicit;

await builder.build().run();
