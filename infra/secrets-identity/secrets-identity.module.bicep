@description('The location for the resource(s) to be deployed.')
param location string = resourceGroup().location

resource secrets_identity 'Microsoft.ManagedIdentity/userAssignedIdentities@2024-11-30' = {
  name: take('secrets_identity-${uniqueString(resourceGroup().id)}', 128)
  location: location
}

output id string = secrets_identity.id

output clientId string = secrets_identity.properties.clientId

output principalId string = secrets_identity.properties.principalId

output name string = secrets_identity.name
