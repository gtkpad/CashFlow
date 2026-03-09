@description('The location for the resource(s) to be deployed.')
param location string = resourceGroup().location

@secure()
@description('The gateway shared secret for service-to-service authentication.')
param gateway_secret string

@secure()
@description('The JWT signing key used by gateway and identity services.')
param jwt_signing_key string

@secure()
@description('The RabbitMQ password for messaging.')
param messaging_password string

@secure()
@description('The RabbitMQ username for messaging.')
param messaging_username string

@description('The principal ID of the managed identity that will read secrets.')
param secrets_identity_principal_id string

resource vault 'Microsoft.KeyVault/vaults@2023-07-01' = {
  name: take('kv-${uniqueString(resourceGroup().id)}', 24)
  location: location
  properties: {
    sku: {
      family: 'A'
      name: 'standard'
    }
    tenantId: subscription().tenantId
    enableRbacAuthorization: true
    enableSoftDelete: true
    softDeleteRetentionInDays: 7
  }
}

resource gateway_secret_entry 'Microsoft.KeyVault/vaults/secrets@2023-07-01' = {
  parent: vault
  name: 'gateway-secret'
  properties: {
    value: gateway_secret
  }
}

resource jwt_signing_key_entry 'Microsoft.KeyVault/vaults/secrets@2023-07-01' = {
  parent: vault
  name: 'jwt-signing-key'
  properties: {
    value: jwt_signing_key
  }
}

resource messaging_password_entry 'Microsoft.KeyVault/vaults/secrets@2023-07-01' = {
  parent: vault
  name: 'messaging-password'
  properties: {
    value: messaging_password
  }
}

resource messaging_username_entry 'Microsoft.KeyVault/vaults/secrets@2023-07-01' = {
  parent: vault
  name: 'messaging-username'
  properties: {
    value: messaging_username
  }
}

resource messaging_uri_entry 'Microsoft.KeyVault/vaults/secrets@2023-07-01' = {
  parent: vault
  name: 'messaging-uri'
  properties: {
    value: 'amqp://${uriComponent(messaging_username)}:${uriComponent(messaging_password)}@messaging:5672'
  }
}

@description('Key Vault Secrets User role for the secrets managed identity.')
resource secrets_role 'Microsoft.Authorization/roleAssignments@2022-04-01' = {
  name: guid(vault.id, secrets_identity_principal_id, '4633458b-17de-408a-b874-0445c86b69e6')
  scope: vault
  properties: {
    principalId: secrets_identity_principal_id
    principalType: 'ServicePrincipal'
    roleDefinitionId: subscriptionResourceId(
      'Microsoft.Authorization/roleDefinitions',
      '4633458b-17de-408a-b874-0445c86b69e6'
    )
  }
}

output vaultUri string = vault.properties.vaultUri

output name string = vault.name
