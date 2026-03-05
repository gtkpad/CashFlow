@description('The location for the resource(s) to be deployed.')
param location string = resourceGroup().location

resource identity_identity 'Microsoft.ManagedIdentity/userAssignedIdentities@2024-11-30' = {
  name: take('identity_identity-${uniqueString(resourceGroup().id)}', 128)
  location: location
}

output id string = identity_identity.id

output clientId string = identity_identity.properties.clientId

output principalId string = identity_identity.properties.principalId

output principalName string = identity_identity.name

output name string = identity_identity.name