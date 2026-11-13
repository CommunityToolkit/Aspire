package main

import (
	"apphost/modules/aspire"
	"log"
	"os"
)

func main() {
	bindMountPath, err := os.MkdirTemp("", "dbgate-")
	check(err)
	builder, err := aspire.CreateBuilder()
	check(err)

	dbgate := builder.AddDbGate(&aspire.AddDbGateOptions{Name: aspire.StringPtr("dbgate")}).
		WithHostPort(aspire.Float64Ptr(3310)).
		WithDataBindMount(bindMountPath, &aspire.WithDataBindMountOptions{IsReadOnly: aspire.BoolPtr(false)})
	check(dbgate.Err())
	// A bind mount and volume would otherwise overlap on the same target path.
	if os.Getenv("ASPIRE_COMPILE_ONLY") == "1" {
		dbgate.WithDataVolume(&aspire.WithDataVolumeOptions{Name: aspire.StringPtr("dbgate-data")})
		check(dbgate.Err())
	}
	check(dbgate.PrimaryEndpoint().Err())

	app, err := builder.Build()
	check(err)
	check(app.Run())
}

func check(err error) {
	if err != nil {
		log.Fatal(aspire.FormatError(err))
	}
}
