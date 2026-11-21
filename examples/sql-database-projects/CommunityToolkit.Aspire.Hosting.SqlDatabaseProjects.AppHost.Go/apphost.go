package main

import (
	"apphost/modules/aspire"
	"log"
	"path/filepath"
)

func main() {
	builder, err := aspire.CreateBuilder()
	check(err)

	appHostDirectory, err := builder.AppHostDirectory()
	check(err)
	projectPath := filepath.Join(appHostDirectory, "..", "SdkProject", "SdkProject.csproj")
	profilePath := filepath.Join(appHostDirectory, "..", "SdkProject", "Database.publish.xml")

	sql := builder.AddSqlServer("sql")
	database := sql.AddDatabase("TargetDatabase")
	existingConnection := builder.AddConnectionString("Aspire")
	check(sql.Err())
	check(database.Err())
	check(existingConnection.Err())

	project := builder.AddSqlProject("database-project")
	project.WithDacpac(projectPath)
	project.WithDacDeployOptions(profilePath)
	project.WithSkipWhenDeployed()
	project.WithReference(database)
	check(project.Err())

	packageDatabase := sql.AddDatabase("PackageDatabase")
	packageProject := builder.AddSqlPackage("package-project", "ErikEJ.Dacpac.Chinook", "1.0.0")
	check(packageDatabase.Err())
	packageProject.WithReference(packageDatabase)
	check(packageProject.Err())

	connectionProject := builder.AddSqlProject("connection-project")
	connectionProject.WithDacpac(projectPath)
	connectionProject.WithConnectionReference(existingConnection)
	includeInPublish := true
	includeInDeploy := true
	connectionProject.WithPipelineOptions(&aspire.DacpacPipelineOptions{
		IncludeInPublish: &includeInPublish,
		IncludeInDeploy:  &includeInDeploy,
	})
	check(connectionProject.Err())

	_, err = project.DacpacPath()
	check(err)
	_, err = project.DacDeployOptionsPath()
	check(err)
	_, err = project.SkipWhenDeployed()
	check(err)
	_, err = connectionProject.DacpacPath()
	check(err)

	app, err := builder.Build()
	check(err)
	check(app.Run())
}

func check(err error) {
	if err != nil {
		log.Fatal(aspire.FormatError(err))
	}
}
