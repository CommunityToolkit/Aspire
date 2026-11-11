package main

import (
	"apphost/modules/aspire"
	"log"
	"path/filepath"
)

func main() {
	builder, err := aspire.CreateBuilder()
	if err != nil {
		log.Fatal(aspire.FormatError(err))
	}
	nxRoot, err := filepath.Abs("../nx-demo")
	if err != nil {
		log.Fatal(err)
	}
	turboRoot, err := filepath.Abs("../turborepo-demo")
	if err != nil {
		log.Fatal(err)
	}

	nx := builder.AddNxApp("nx-demo", &aspire.AddNxAppOptions{WorkingDirectory: aspire.StringPtr(nxRoot)}).
		WithNpm().WithPackageManagerLaunch().WithExplicitStart()
	nxBlog := nx.AddApp("blog-monorepo").
		WithHttpEndpoint(&aspire.WithHttpEndpointOptions{Env: aspire.StringPtr("PORT")}).
		WithMappedEndpointPort().WithHttpHealthCheck().WithExplicitStart()
	nxYarn := builder.AddNxApp("nx-yarn", &aspire.AddNxAppOptions{WorkingDirectory: aspire.StringPtr(nxRoot)}).
		WithYarn().WithPackageManagerLaunch(&aspire.WithPackageManagerLaunchOptions{PackageManager: aspire.StringPtr("yarn")}).
		WithExplicitStart()
	nxYarnBlog := nxYarn.AddApp("blog-monorepo-yarn", &aspire.AddAppOptions{AppName: aspire.StringPtr("blog-monorepo")}).
		WithHttpEndpoint(&aspire.WithHttpEndpointOptions{Env: aspire.StringPtr("PORT")}).
		WithMappedEndpointPort(&aspire.WithMappedEndpointPortOptions{EndpointName: aspire.StringPtr("http")}).
		WithExplicitStart()
	nxPnpm := builder.AddNxApp("nx-pnpm", &aspire.AddNxAppOptions{WorkingDirectory: aspire.StringPtr(nxRoot)}).
		WithPnpm().WithPackageManagerLaunch(&aspire.WithPackageManagerLaunchOptions{PackageManager: aspire.StringPtr("pnpm")}).
		WithExplicitStart()

	turbo := builder.AddTurborepoApp("turborepo-demo", &aspire.AddTurborepoAppOptions{WorkingDirectory: aspire.StringPtr(turboRoot)}).
		WithNpm(&aspire.WithNpmOptions{Install: aspire.BoolPtr(true)}).WithPackageManagerLaunch()
	turboWeb := turbo.AddApp("turbo-web", &aspire.TurborepoResourceAddAppOptions{Filter: aspire.StringPtr("web")}).
		WithHttpEndpoint(&aspire.WithHttpEndpointOptions{Env: aspire.StringPtr("PORT")}).
		WithMappedEndpointPort().WithHttpHealthCheck()
	turboYarn := builder.AddTurborepoApp("turborepo-yarn", &aspire.AddTurborepoAppOptions{WorkingDirectory: aspire.StringPtr(turboRoot)}).
		WithYarn().WithPackageManagerLaunch(&aspire.WithPackageManagerLaunchOptions{PackageManager: aspire.StringPtr("yarn")}).
		WithExplicitStart()
	turboDocs := turboYarn.AddApp("docs").
		WithHttpEndpoint(&aspire.WithHttpEndpointOptions{Env: aspire.StringPtr("PORT")}).
		WithMappedEndpointPort(&aspire.WithMappedEndpointPortOptions{EndpointName: aspire.StringPtr("http")}).
		WithExplicitStart()
	turboPnpm := builder.AddTurborepoApp("turborepo-pnpm", &aspire.AddTurborepoAppOptions{WorkingDirectory: aspire.StringPtr(turboRoot)}).
		WithPnpm().WithPackageManagerLaunch(&aspire.WithPackageManagerLaunchOptions{PackageManager: aspire.StringPtr("pnpm")}).
		WithExplicitStart()

	for _, resource := range []interface{ Err() error }{nx, nxBlog, nxYarn, nxYarnBlog, nxPnpm, turbo, turboWeb, turboYarn, turboDocs, turboPnpm} {
		if err := resource.Err(); err != nil {
			log.Fatal(aspire.FormatError(err))
		}
	}
	app, err := builder.Build()
	if err != nil {
		log.Fatal(aspire.FormatError(err))
	}
	if err := app.Run(); err != nil {
		log.Fatal(aspire.FormatError(err))
	}
}
