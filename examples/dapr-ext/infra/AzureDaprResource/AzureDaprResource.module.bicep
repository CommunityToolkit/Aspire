@description('The location for the resource(s) to be deployed.')
param location string = resourceGroup().location

param daprConnectionString string

param keyVaultName string

var resourceToken = uniqueString(resourceGroup().id)

resource containerAppEnvironment 'Microsoft.App/managedEnvironments@2024-03-01' existing = {
  name: 'cae-${resourceToken}'
}

resource daprComponent 'Microsoft.App/managedEnvironments/daprComponents@2024-03-01' = {
  name: take('daprComponent${resourceToken}', 24)
  properties: {
    componentType: 'state.redis'
    version: 'v1'
  }
  parent: containerAppEnvironment
}

resource keyVault 'Microsoft.KeyVault/vaults@2023-07-01' existing = {
  name: keyVaultName
}

resource mysecret 'Microsoft.KeyVault/vaults/secrets@2023-07-01' = {
  name: 'mysecret'
  properties: {
    value: 'secretValue'
  }
  parent: keyVault
}