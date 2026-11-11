# Go AppHost parity gaps

These TypeScript examples cannot currently be translated faithfully using the
repository's pinned Aspire SDK (`17.1.0-preview.1.26559.8`):

- **RustFs:** `AddRustFsOptions.AccessKey` and `SecretKey` are generated as
  `*aspire.ParameterResource` (pointers to interfaces). Passing addresses of
  the two parameter resources compiles, but the generated `serializeValue`
  does not recognize those pointers as resource handles. During AppHost model
  construction, `addRustFs` fails with `INVALID_ARGUMENT`: `Parameter type
  'IResourceBuilder\`1' must have [AspireDto] attribute to be deserialized from
  JSON` for `accessKey`. The TypeScript example explicitly supplies both
  credentials, so omitting them would not preserve its resource graph.
- **Umami:** `AddUmamiOptions.Secret` is likewise a `*aspire.ParameterResource`.
  Passing the address of the explicitly configured `umami-secret` compiles,
  but `aspire publish --apphost apphost.go --list-steps --non-interactive`
  fails while constructing the model: `CommunityToolkit.Aspire.Hosting.Umami/addUmami`
  reports `INVALID_ARGUMENT` for `secret`, with the same `IResourceBuilder\`1`
  deserialization error as RustFs. Omitting the secret would not preserve the
  TypeScript example.
- **Meilisearch:** `AddMeilisearchOptions.MasterKey` is also a pointer to a
  parameter interface. An explicit `search-master-key` compiles but
  `CommunityToolkit.Aspire.Hosting.Meilisearch/addMeilisearch` fails during
  publish-model construction with `INVALID_ARGUMENT` for `masterKey` and
  the same `IResourceBuilder\`1` deserialization error. The TypeScript
  example explicitly supplies the key.

The Python extensions Go example runs, but its publish-mode model fails:
`AddStreamlitApp` removes the Python resource before adding its Streamlit
replacement, while the publish-mode executable container with the same name
remains in the model. The resulting duplicate `dashboard` resource causes
`AddStreamlitApp` to fail. Its Go compilation test therefore does not run the
publish-model check; fixing the integration is outside this example-only work.
