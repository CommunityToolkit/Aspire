package main

import (
	"apphost/modules/aspire"
	"log"
)

func main() {
	builder, err := aspire.CreateBuilder()
	check(err)

	postgres := builder.AddPostgres("postgres")
	redis := builder.AddRedis("redis")
	check(postgres.Err())
	check(redis.Err())

	logto := builder.AddLogto("logto", postgres)
	logto.WithRedis(redis)
	logto.WithDatabaseSeeding()
	check(logto.Err())

	oidc := builder.AddDotnetProject("clientOIDC", "../CommunityToolkit.Aspire.Logto.ClientOIDC/CommunityToolkit.Aspire.Logto.ClientOIDC.csproj")
	oidc.WithReference(logto).WaitFor(logto)
	check(oidc.Err())
	jwt := builder.AddDotnetProject("clientJWT", "../CommunityToolkit.Aspire.Logto.ClientJWT/CommunityToolkit.Aspire.Logto.ClientJWT.csproj")
	jwt.WithReference(logto).WaitFor(logto)
	check(jwt.Err())

	app, err := builder.Build()
	check(err)
	check(app.Run())
}

func check(err error) {
	if err != nil {
		log.Fatal(aspire.FormatError(err))
	}
}
