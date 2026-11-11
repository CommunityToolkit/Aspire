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

	sqlite := builder.AddSqlite("sqlite", &aspire.AddSqliteOptions{
		DatabasePath:     aspire.StringPtr(".data/sqlite"),
		DatabaseFileName: aspire.StringPtr("app.db"),
	})
	sqlite.WithSqliteWeb(&aspire.WithSqliteWebOptions{ContainerName: aspire.StringPtr("sqlite-browser")})
	_ = sqlite.ConnectionStringExpression()
	if err := sqlite.Err(); err != nil {
		log.Fatal(aspire.FormatError(err))
	}

	sqliteDefault := builder.AddSqlite("sqlite-default")
	sqliteDefault.WithSqliteWeb()
	if err := sqliteDefault.Err(); err != nil {
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
