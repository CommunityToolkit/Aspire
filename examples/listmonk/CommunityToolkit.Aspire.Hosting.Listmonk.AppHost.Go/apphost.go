package main

import (
	"apphost/modules/aspire"
	"log"
)

func main() {
	builder, err := aspire.CreateBuilder()
	check(err)
	password := builder.AddParameter("admin-password", &aspire.AddParameterOptions{
		Value:  aspire.StringPtr("SuperSecret123!"),
		Secret: aspire.BoolPtr(true),
	})
	check(password.Err())
	postgres := builder.AddPostgres("postgres")
	database := postgres.AddDatabase("listmonkdb")
	defaultPostgres := builder.AddPostgres("listmonk-default-postgres")
	defaultDatabase := defaultPostgres.AddDatabase("listmonk-default-db")
	check(postgres.Err())
	check(database.Err())
	check(defaultPostgres.Err())
	check(defaultDatabase.Err())

	listmonk := builder.AddListmonk("listmonk", &aspire.AddListmonkOptions{Port: aspire.Float64Ptr(31900)})
	defaults := builder.AddListmonk("listmonk-default")
	listmonk.WithReference(database)
	listmonk.WithAdminCredentials("admin", password)
	listmonk.WithDatabaseMaxOpenConnections(25)
	listmonk.WithDatabaseMaxIdleConnections(25)
	listmonk.WithDatabaseMaxLifetime("300s")
	listmonk.WithTimeZone("Etc/UTC")
	listmonk.WithUserId(0)
	listmonk.WithGroupId(0)
	listmonk.WithUploadsVolume()
	defaults.WithReference(defaultDatabase)
	defaults.WithDatabaseSslMode("disable")
	check(listmonk.PrimaryEndpoint().Err())
	check(listmonk.Host().Err())
	check(listmonk.Port().Err())
	_ = listmonk.UriExpression()
	_ = listmonk.ConnectionStringExpression()
	check(listmonk.Err())
	check(defaults.Err())

	app, err := builder.Build()
	check(err)
	check(app.Run())
}

func check(err error) {
	if err != nil {
		log.Fatal(aspire.FormatError(err))
	}
}
