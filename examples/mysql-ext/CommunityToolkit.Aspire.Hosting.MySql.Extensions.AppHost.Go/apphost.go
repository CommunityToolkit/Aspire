package main

import (
	"apphost/modules/aspire"
	"log"
)

func main() {
	builder, err := aspire.CreateBuilder()
	check(err)

	mysql := builder.AddMySql("mysql")
	mysql.WithAdminer(&aspire.WithAdminerOptions{
		ConfigureContainer: func(adminer aspire.AdminerContainerResource) {},
		ContainerName:      aspire.StringPtr("mysql-adminer"),
	})
	mysql.WithDbGate(&aspire.WithDbGateOptions{
		ConfigureContainer: func(dbgate aspire.DbGateContainerResource) {},
		ContainerName:      aspire.StringPtr("mysql-dbgate"),
	})
	check(mysql.PrimaryEndpoint().Err())
	check(mysql.Host().Err())
	check(mysql.Port().Err())
	_ = mysql.ConnectionStringExpression()
	check(mysql.Err())

	app, err := builder.Build()
	check(err)
	check(app.Run())
}

func check(err error) {
	if err != nil {
		log.Fatal(aspire.FormatError(err))
	}
}
