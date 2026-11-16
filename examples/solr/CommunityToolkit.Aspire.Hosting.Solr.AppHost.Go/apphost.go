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
	data := filepath.Join(directory, "..", "CommunityToolkit.Aspire.Hosting.Solr.AppHost.TypeScript", "solr-data")
	check(os.MkdirAll(data, 0777))
	check(os.Chmod(data, 0777))

	solr := builder.AddSolr("solr")
	core, err := solr.CoreName()
	check(err)
	solr.SetCoreName(core)
	solr.WithDataVolume(&aspire.WithDataVolumeOptions{Name: aspire.StringPtr("solr-data")})
	check(solr.PrimaryEndpoint().Err())
	check(solr.Host().Err())
	check(solr.Port().Err())
	_, err = solr.CoreName()
	check(err)
	_ = solr.ConnectionStringExpression()
	_ = solr.UriExpression()
	check(solr.Err())

	mounted := builder.AddSolr("solr-bind", &aspire.AddSolrOptions{
		Port:     aspire.Float64Ptr(8984),
		CoreName: aspire.StringPtr("bindcore"),
	})
	mountedCore, err := mounted.CoreName()
	check(err)
	mounted.SetCoreName(mountedCore)
	mounted.WithDataBindMount(data)
	check(mounted.Err())

	if false {
		configset := builder.AddSolr("solr-configset", &aspire.AddSolrOptions{CoreName: aspire.StringPtr("configset-core")})
		configset.WithConfigset("sample-configset", filepath.Join(directory, "..", "CommunityToolkit.Aspire.Hosting.Solr.AppHost.TypeScript", "configsets", "sample-configset"))
		check(configset.Err())
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
