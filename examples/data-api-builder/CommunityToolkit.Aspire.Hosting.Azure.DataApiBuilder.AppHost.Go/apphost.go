package main

import (
	"apphost/modules/aspire"
	"log"
	"path/filepath"
)

func main() {
	builder, err := aspire.CreateBuilder()
	check(err)

	configDirectory, err := filepath.Abs("../CommunityToolkit.Aspire.Hosting.Azure.DataApiBuilder.AppHost.TypeScript")
	check(err)
	primaryConfig := filepath.Join(configDirectory, "dab-config.json")
	secondaryConfig := filepath.Join(configDirectory, "dab-config-2.json")
	sql := builder.AddSqlServer("sql").
		WithBindMount(filepath.Join(configDirectory, "sql-server"), "/usr/config").
		WithBindMount("../database", "/docker-entrypoint-initdb.d").
		WithEntrypoint("/usr/config/entrypoint.sh")
	check(sql.Err())
	db := sql.AddDatabase("trek")
	check(db.Err())
	dab := builder.AddDataAPIBuilder("dab", &aspire.AddDataAPIBuilderOptions{
		ConfigFilePaths: []string{primaryConfig},
	}).WaitFor(sql).WithReference(db)
	check(dab.Err())
	dabWithOptions := builder.AddDataAPIBuilder("dab-with-options", &aspire.AddDataAPIBuilderOptions{
		ConfigFilePaths: []string{primaryConfig, secondaryConfig}, HttpPort: aspire.Float64Ptr(5001),
	}).WaitFor(sql).WithReference(db)
	check(dabWithOptions.Err())

	check(dab.PrimaryEndpoint().Err())
	check(dab.Host().Err())
	check(dab.Port().Err())
	_ = dab.UriExpression()
	check(dabWithOptions.PrimaryEndpoint().Err())
	check(dabWithOptions.Host().Err())
	check(dabWithOptions.Port().Err())
	_ = dabWithOptions.UriExpression()

	app, err := builder.Build()
	check(err)
	check(app.Run())
}

func check(err error) {
	if err != nil {
		log.Fatal(aspire.FormatError(err))
	}
}
