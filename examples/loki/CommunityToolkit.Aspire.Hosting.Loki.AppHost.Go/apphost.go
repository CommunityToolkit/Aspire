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

	loki := builder.AddLoki("loki")
	loki.WithHttpHostPort(nil)
	loki.WithGrpcHostPort(nil)
	loki.WithDataVolume()
	check(loki.HttpEndpoint().Err())
	check(loki.GrpcEndpoint().Err())
	check(loki.Err())

	mount, err := os.MkdirTemp("", "loki-")
	check(err)
	if runtime.GOOS != "windows" {
		check(os.Chmod(mount, 0777))
	}
	mounted := builder.AddLoki("loki-mounted")
	mounted.WithDataBindMount(mount)
	mounted.WithConfigFile(filepath.Join(directory, "..", "CommunityToolkit.Aspire.Hosting.Loki.AppHost.TypeScript", "loki.yaml"))
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
