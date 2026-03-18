import {
    createBuilder,
    type ElasticsearchResource,
    type ElasticvueContainerResource
} from './.modules/aspire.js';

const builder = await createBuilder();

async function validateWithHostPort(elasticvue: ElasticvueContainerResource): Promise<ElasticvueContainerResource> {
    return await elasticvue.withHostPort({ port: 9280 });
}

// Compile-time validation for exported extension methods until the base Elasticsearch add* API is available in TS.
async function validateWithElasticvue(elasticsearch: ElasticsearchResource): Promise<ElasticsearchResource> {
    return await elasticsearch.withElasticvue({
        containerName: "elasticvue-ui",
        configureContainer: async (elasticvue) => {
            await validateWithHostPort(elasticvue);
        }
    });
}

void validateWithElasticvue;

await builder.build().run();
