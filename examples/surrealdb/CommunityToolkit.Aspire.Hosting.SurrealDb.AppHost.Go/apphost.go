package main

import (
	"apphost/modules/aspire"
	"log"
)

func main() {
	builder, err := aspire.CreateBuilder()
	check(err)

	primary := builder.AddSurrealServer("primary", &aspire.AddSurrealServerOptions{
		Port: aspire.Float64Ptr(18000), Path: aspire.StringPtr("memory"),
	})
	primary.WithDataVolume(&aspire.WithDataVolumeOptions{Name: aspire.StringPtr("surreal-primary-data")})
	primary.WithLogLevel("Information")
	primary.WithSurrealist(&aspire.WithSurrealistOptions{ContainerName: aspire.StringPtr("surrealist-primary")})
	primary.WithSurrealDbOtlpExporter()
	check(primary.Err())

	namespace := primary.AddNamespace("appns", &aspire.AddNamespaceOptions{NamespaceName: aspire.StringPtr("polyglotNs")})
	namespace.WithCreationScript("DEFINE NAMESPACE IF NOT EXISTS `polyglotNs`;")
	check(namespace.Err())
	database := namespace.AddDatabase("appdb", &aspire.AddDatabaseOptions{DatabaseName: aspire.StringPtr("polyglotDb")})
	database.WithCreationScript("DEFINE DATABASE IF NOT EXISTS `polyglotDb`;")
	check(database.Err())

	mounted := builder.AddSurrealServer("mounted", &aspire.AddSurrealServerOptions{
		Port: aspire.Float64Ptr(18001), Path: aspire.StringPtr("memory"),
	})
	mounted.WithDataBindMount("./data")
	mounted.WithInitFiles("./seed.surql")
	check(mounted.Err())

	check(primary.PrimaryEndpoint().Err())
	check(primary.Host().Err())
	check(primary.Port().Err())
	check(primary.PasswordParameter().Err())
	_ = primary.UriExpression()
	_ = primary.ConnectionStringExpression()

	namespaceParent := namespace.Parent()
	check(namespaceParent.Err())
	_ = namespace.ConnectionStringExpression()
	_, err = namespace.NamespaceName()
	check(err)
	_, err = namespaceParent.Name()
	check(err)

	databaseParent := database.Parent()
	check(databaseParent.Err())
	_ = database.ConnectionStringExpression()
	_, err = database.DatabaseName()
	check(err)
	_, err = databaseParent.Name()
	check(err)
	server := databaseParent.Parent()
	check(server.Err())
	_, err = server.Name()
	check(err)

	check(mounted.PrimaryEndpoint().Err())
	check(mounted.Host().Err())
	check(mounted.Port().Err())
	_ = mounted.UriExpression()
	_ = mounted.ConnectionStringExpression()

	app, err := builder.Build()
	check(err)
	check(app.Run())
}

func check(err error) {
	if err != nil {
		log.Fatal(aspire.FormatError(err))
	}
}
