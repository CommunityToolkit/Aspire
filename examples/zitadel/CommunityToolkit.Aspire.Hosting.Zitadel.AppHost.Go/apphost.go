package main

import (
	"apphost/modules/aspire"
	"log"
)

func main() {
	builder, err := aspire.CreateBuilder()
	check(err)

	username := builder.AddParameter("zitadel-username", &aspire.AddParameterOptions{Value: aspire.StringPtr("admin")})
	password := builder.AddParameter("zitadel-password", &aspire.AddParameterOptions{
		Value: aspire.StringPtr("AspireZitadelPassword!1"), Secret: aspire.BoolPtr(true),
	})
	masterKey := builder.AddParameter("zitadel-master-key", &aspire.AddParameterOptions{
		Value: aspire.StringPtr("Aa1!Aa1!Aa1!Aa1!Aa1!Aa1!Aa1!Aa1!"), Secret: aspire.BoolPtr(true),
	})
	check(username.Err())
	check(password.Err())
	check(masterKey.Err())

	postgres := builder.AddPostgres("postgres")
	existingDatabase := postgres.AddDatabase("zitadel-existing-db")
	check(postgres.Err())
	check(existingDatabase.Err())

	zitadel := builder.AddZitadel("zitadel", &aspire.AddZitadelOptions{
		Port: aspire.Float64Ptr(8080), Username: &username, Password: &password, MasterKey: &masterKey,
	})
	zitadel.WithExternalDomain("auth.example.com")
	zitadel.WithDatabase(postgres, &aspire.WithDatabaseOptions{DatabaseName: aspire.StringPtr("zitadel-db")})
	check(zitadel.Err())

	minimal := builder.AddZitadel("zitadel-minimal")
	minimal.WithExistingDatabase(existingDatabase)
	minimal.WithExternalDomain("login.example.com")
	check(minimal.Err())

	app, err := builder.Build()
	check(err)
	check(app.Run())
}

func check(err error) {
	if err != nil {
		log.Fatal(aspire.FormatError(err))
	}
}
