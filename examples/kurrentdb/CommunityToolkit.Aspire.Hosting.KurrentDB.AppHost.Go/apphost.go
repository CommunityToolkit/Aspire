package main

import (
	"apphost/modules/aspire"
	"log"
	"os"
	"path/filepath"
)

func main() {
	builder, err := aspire.CreateBuilder()
	check(err)
	directory, err := builder.AppHostDirectory()
	check(err)
	mount := filepath.Join(directory, "..", "CommunityToolkit.Aspire.Hosting.KurrentDB.AppHost.TypeScript", "data", "kurrentdb")
	check(os.MkdirAll(mount, 0755))

	volume := builder.AddKurrentDB("kurrentdb", &aspire.AddKurrentDBOptions{Port: aspire.Float64Ptr(22113)})
	volume.WithDataVolume(&aspire.WithDataVolumeOptions{Name: aspire.StringPtr("kurrentdb-data")})
	mounted := builder.AddKurrentDB("kurrentdb2")
	mounted.WithDataBindMount(mount)
	defaultVolume := builder.AddKurrentDB("kurrentdb3")
	defaultVolume.WithDataVolume()

	for _, resource := range []aspire.KurrentDBResource{volume, mounted, defaultVolume} {
		check(resource.PrimaryEndpoint().Err())
		check(resource.Host().Err())
		check(resource.Port().Err())
		_ = resource.UriExpression()
		_ = resource.ConnectionStringExpression()
		check(resource.Err())
	}

	app, err := builder.Build()
	check(err)
	check(app.Run())
}

func check(err error) {
	if err != nil {
		log.Fatal(aspire.FormatError(err))
	}
}
