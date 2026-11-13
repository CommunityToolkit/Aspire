package main

import (
	"apphost/modules/aspire"
	"log"
	"os"
	"path/filepath"
	"runtime"
)

func main() {
	builder, err := aspire.CreateBuilder()
	check(err)
	directory, err := builder.AppHostDirectory()
	check(err)

	tempo := builder.AddTempo("tempo")
	tempo.WithHttpHostPort(nil)
	tempo.WithGrpcHostPort(nil)
	tempo.WithOtlpGrpcHostPort(nil)
	tempo.WithOtlpHttpHostPort(nil)
	tempo.WithDataVolume()
	check(tempo.HttpEndpoint().Err())
	check(tempo.GrpcEndpoint().Err())
	check(tempo.OtlpGrpcEndpoint().Err())
	check(tempo.OtlpHttpEndpoint().Err())
	check(tempo.Err())

	mount, err := os.MkdirTemp("", "tempo-")
	check(err)
	if runtime.GOOS != "windows" {
		check(os.Chmod(mount, 0777))
	}
	mounted := builder.AddTempo("tempo-mounted")
	mounted.WithDataBindMount(mount)
	mounted.WithConfigFile(filepath.Join(directory, "..", "CommunityToolkit.Aspire.Hosting.Tempo.AppHost.TypeScript", "tempo.yaml"))
	check(mounted.Err())

	app, err := builder.Build()
	check(err)
	check(app.Run())
}

func check(err error) {
	if err != nil {
		log.Fatal(aspire.FormatError(err))
	}
}
