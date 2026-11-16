package main

import (
	"apphost/modules/aspire"
	"log"
	"path/filepath"
)

func main() {
	builder, err := aspire.CreateBuilder()
	check(err)
	directory, err := builder.AppHostDirectory()
	check(err)
	config := filepath.Join(directory, "..", "CommunityToolkit.Aspire.Hosting.GoFeatureFlag.AppHost.TypeScript", "goff")

	goff := builder.AddGoFeatureFlag("goff", &aspire.AddGoFeatureFlagOptions{
		PathToConfigFile: aspire.StringPtr("/goff/goff-proxy.yaml"),
		Port:             aspire.Float64Ptr(11031),
	})
	goff.WithDataVolume(&aspire.WithDataVolumeOptions{Name: aspire.StringPtr("goff-data")})
	goff.WithGoffBindMount(config)
	goff.WithLogLevel(aspire.GoFeatureFlagLogLevelDebug)

	goff2 := builder.AddGoFeatureFlag("goff2")
	goff2.WithGoffBindMount(config)

	for _, resource := range []aspire.GoFeatureFlagResource{goff, goff2} {
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
