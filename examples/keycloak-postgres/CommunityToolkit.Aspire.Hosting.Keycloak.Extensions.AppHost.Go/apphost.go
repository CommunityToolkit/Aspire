package main

import (
	"apphost/modules/aspire"
	"log"
)

// AddKeycloak is not exported by Aspire.Hosting.Keycloak yet. Match the TypeScript
// example's compile-only coverage of the generated WithPostgres signatures.
func configureKeycloak(keycloak aspire.KeycloakResource, db aspire.PostgresDatabaseResource, username, password aspire.ParameterResource) {
	keycloak.WithPostgres(db)
	keycloak.WithPostgres(db, &aspire.WithPostgresOptions{
		Username: &username, Password: &password, XaEnabled: aspire.BoolPtr(true),
	})
}

func main() {
	builder, err := aspire.CreateBuilder()
	if err != nil {
		log.Fatal(aspire.FormatError(err))
	}

	username := builder.AddParameter("db-username", &aspire.AddParameterOptions{Value: aspire.StringPtr("postgres")})
	password := builder.AddParameter("db-password", &aspire.AddParameterOptions{
		Value: aspire.StringPtr("Postgres!123"), Secret: aspire.BoolPtr(true),
	})
	postgres := builder.AddPostgres("keycloak-postgres", &aspire.AddPostgresOptions{UserName: &username, Password: &password})
	db := postgres.AddDatabase("keycloak")
	if err := db.Err(); err != nil {
		log.Fatal(aspire.FormatError(err))
	}

	app, err := builder.Build()
	if err != nil {
		log.Fatal(aspire.FormatError(err))
	}
	if err := app.Run(); err != nil {
		log.Fatal(aspire.FormatError(err))
	}
}
