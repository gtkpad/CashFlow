@description('The location for the resource(s) to be deployed.')
param location string = resourceGroup().location

resource consolidation_identity 'Microsoft.ManagedIdentity/userAssignedIdentities@2024-11-30' = {
  name: take('consolidation_identity-${uniqueString(resourceGroup().id)}', 128)
  location: location
}

output id string = consolidation_identity.id

output clientId string = consolidation_identity.properties.clientId

output principalId string = consolidation_identity.properties.principalId

output principalName string = consolidation_identity.name

output name string = consolidation_identity.name