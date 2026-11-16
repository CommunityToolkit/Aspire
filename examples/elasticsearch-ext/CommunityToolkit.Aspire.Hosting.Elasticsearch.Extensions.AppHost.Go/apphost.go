package main

import (
	"apphost/modules/aspire"
	"log"
)

func validateWithHostPort(elasticvue aspire.ElasticvueContainerResource) aspire.ElasticvueContainerResource {
	return elasticvue.WithHostPort(aspire.Float64Ptr(9280))
}

// Keep extension coverage compile-only until the base Elasticsearch add API is exported.
func validateWithElasticvue(elasticsearch aspire.ElasticsearchResource) aspire.ElasticsearchResource {
	return elasticsearch.WithElasticvue(&aspire.WithElasticvueOptions{
		ContainerName: aspire.StringPtr("elasticvue-ui"),
		ConfigureContainer: func(elasticvue aspire.ElasticvueContainerResource) {
			check(validateWithHostPort(elasticvue).Err())
		},
	})
}

func main() {
	builder, err := aspire.CreateBuilder()
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
