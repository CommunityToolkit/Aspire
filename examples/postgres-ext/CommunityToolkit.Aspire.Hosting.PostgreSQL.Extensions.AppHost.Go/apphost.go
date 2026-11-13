package main

import (
	"apphost/modules/aspire"
	"log"
)

func main() {
	builder, err := aspire.CreateBuilder()
	if err != nil {
		log.Fatal(aspire.FormatError(err))
	}

	postgres := builder.AddPostgres("postgres")
	postgres.WithDbGate(&aspire.WithDbGateOptions{ContainerName: aspire.StringPtr("postgres-dbgate")})
	postgres.WithAdminer(&aspire.WithAdminerOptions{ContainerName: aspire.StringPtr("postgres-adminer")})
	database := postgres.AddDatabase("appdb")
	check(postgres.PrimaryEndpoint().Err())
	check(postgres.Host().Err())
	check(postgres.Port().Err())
	_ = postgres.UriExpression()
	_ = postgres.JdbcConnectionString()
	if err := postgres.Err(); err != nil {
		log.Fatal(aspire.FormatError(err))
	}

	migrations := "../CommunityToolkit.Aspire.Hosting.PostgreSQL.Extensions.AppHost.TypeScript/migrations"
	database.WithFlywayMigration("flyway-migration", migrations)
	database.WithFlywayRepair("flyway-repair", migrations)
	if _, err := database.DatabaseName(); err != nil {
		log.Fatal(aspire.FormatError(err))
	}
	_ = database.UriExpression()
	_ = database.JdbcConnectionString()
	if err := database.Err(); err != nil {
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

func check(err error) {
	if err != nil {
		log.Fatal(aspire.FormatError(err))
	}
}
