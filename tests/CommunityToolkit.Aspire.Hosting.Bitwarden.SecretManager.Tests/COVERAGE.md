# Provisioner test coverage

Matrix dimensions: **access token** (present / missing), **org ID** (present / missing / invalid), **project** (name / GUID ID), **managed secrets** (0 / 1+), **unmanaged secrets** (0 / 1+).

## `AuthenticateAsync` · `ProvisionProjectAsync` · `ProvisionSecretsAsync` pipeline

| Test | Token | Org | Project | Managed | Unmanaged | File |
|---|---|---|---|---|---|---|
| `ProvisionAsync_CreatesProjectAndManagedSecret` | ✓ | ✓ | name | 1 | 0 | Provisioner |
| `ProvisionAsync_UsesParameterBackedProjectName` | ✓ | ✓ | name | 0 | 0 | Provisioner |
| `ProvisionAsync_WhenProjectNameOrIdIsGuid_AdoptsExistingProjectById` | ✓ | ✓ | GUID | 0 | 0 | Provisioner |
| `ProvisionAsync_WhenManagedSecretIsAlsoReferencedByName_TreatsItAsSingleSecret` | ✓ | ✓ | name | 1 (also referenced by name) | 0 | Provisioner |

## `SyncMissingManagedSecretValuesAsync`

| Test | Token | Org | Project | Managed | Notes | File |
|---|---|---|---|---|---|---|
| `SyncMissingManagedSecretValuesAsync_UsesExistingUpstreamValueWhenParameterIsMissing` | ✓ | ✓ | GUID | 1 (missing param) | upstream value adopted | Provisioner |
| `SyncMissingManagedSecretValuesAsync_DoesNotOverrideConfiguredParameterValue` | ✓ | ✓ | GUID | 1 (value in config) | upstream value ignored | Provisioner |

## `ProvisionSecretsAsync` — duplicate managed-secret resolution

| Test | Token | Org | Project | Managed | Scenario | File |
|---|---|---|---|---|---|---|
| `ProvisionSecretsAsync_DuplicateManagedSecretNames_NonInteractive_Throws` | ✓ | ✓ | GUID | 1 (two remote dupes) | no interaction → throws | Provisioner |
| `ProvisionSecretsAsync_DuplicateManagedSecretNames_Interactive_UserPicksCandidate_SyncsSelected` | ✓ | ✓ | GUID | 1 | user selects one → syncs | Provisioner |
| `ProvisionSecretsAsync_DuplicateManagedSecretNames_Interactive_UserCancels_Throws` | ✓ | ✓ | GUID | 1 | user cancels → throws | Provisioner |
| `ProvisionSecretsAsync_DuplicateManagedSecretNames_Interactive_InvalidInput_Throws` | ✓ | ✓ | GUID | 1 | user enters invalid GUID → throws | Provisioner |

## `SyncReferenceSecretValuesAsync`

| Test | Token | Org | Project | Managed | Unmanaged | Scenario | File |
|---|---|---|---|---|---|---|---|
| `SyncReferenceSecretsAsync_NoUnmanagedSecrets_CompletesWithoutQueryingProvider` | — | — | — | 1 | 0 | early return before ProjectId check | ReferenceSecret |
| `SyncReferenceSecretsAsync_ByName_SingleMatch_SyncsValue` | ✓ | ✓ | GUID | 0 | 1 (by name) | single match → value bound | ReferenceSecret |
| `SyncReferenceSecretsAsync_ByName_NotFound_Throws` | ✓ | ✓ | GUID | 0 | 1 (by name) | no match → throws | ReferenceSecret |
| `SyncReferenceSecretsAsync_ByName_DuplicateNames_Throws` | ✓ | ✓ | GUID | 0 | 1 (by name) | two same-name secrets → throws | ReferenceSecret |
| `SyncReferenceSecretsAsync_ById_Found_SyncsValue` | ✓ | ✓ | GUID | 0 | 1 (by ID) | found in correct project → value bound | ReferenceSecret |
| `SyncReferenceSecretsAsync_ById_NotFound_Throws` | ✓ | ✓ | GUID | 0 | 1 (by ID) | ID unknown → throws | ReferenceSecret |
| `SyncReferenceSecretsAsync_ById_WrongProject_Throws` | ✓ | ✓ | GUID | 0 | 1 (by ID) | ID found in wrong project → throws | ReferenceSecret |
| `FullPipeline_ManagedAndUnmanagedSecrets_BothResolved` | ✓ | ✓ | GUID | 1 | 1 (by name) | full pipeline, both resolved | ReferenceSecret |

## `PreSyncManagedSecretValuesAsync`

| Test | Token | Org | Project | Managed | Scenario | File |
|---|---|---|---|---|---|---|
| `PreSyncManagedSecretValuesAsync_NoManagedSecrets_ReturnsBeforeAccessingDeploymentState` | — | — | — | 0 | early return, no IDeploymentStateManager needed | PreSync |
| `PreSyncManagedSecretValuesAsync_TokenMissing_NoInteraction_ReturnsWithoutSaving` | missing | ✓ | name | 1 | no IInteractionService → return early | PreSync |
| `PreSyncManagedSecretValuesAsync_TokenMissing_InteractionCanceled_ReturnsWithoutSaving` | missing | ✓ | name | 1 | interaction cancels → return early | PreSync |
| `PreSyncManagedSecretValuesAsync_OrgMissing_NoInteraction_ReturnsWithoutSaving` | ✓ | missing | name | 1 | no IInteractionService → return early | PreSync |
| `PreSyncManagedSecretValuesAsync_OrgPresentButInvalidGuid_ReturnsWithoutSaving` | ✓ | invalid | name | 1 | org not parseable as GUID → return early | PreSync |
| `PreSyncManagedSecretValuesAsync_SecretAlreadyInConfig_SkipsSave` | ✓ | ✓ | GUID | 1 (value in config) | value already present → no deployment-state save | PreSync |
| `PreSyncManagedSecretValuesAsync_SecretNotInConfig_UpstreamFound_SavesValue` | ✓ | ✓ | GUID | 1 (missing) | upstream value found → saved to deployment state | PreSync |

## Known gaps

- `PreSyncManagedSecretValuesAsync`: token missing but interaction provides a valid token (requires a multi-prompt fake).
- `SyncReferenceSecretValuesAsync`: duplicate names with interactive resolution (the method always throws; no interactive path exists).
