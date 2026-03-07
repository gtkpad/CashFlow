targetScope = 'subscription'

@minLength(1)
@maxLength(64)
@description('Name of the environment that can be used as part of naming resource convention, the name of the resource group for your application will use this name, prefixed with rg-')
param environmentName string

@minLength(1)
@description('The location used for all deployed resources')
param location string

@description('Id of the user or app to assign application roles')
param principalId string = ''

@secure()
param gateway_secret string
@secure()
param jwt_signing_key string
@metadata({azd: {
  type: 'generate'
  config: {length:22,noSpecial:true}
  }
})
@secure()
param messaging_password string

param alert_email_address string = ''

var tags = {
  'azd-env-name': environmentName
}

resource rg 'Microsoft.Resources/resourceGroups@2022-09-01' = {
  name: 'rg-${environmentName}'
  location: location
  tags: tags
}

module secrets_identity 'secrets-identity/secrets-identity.module.bicep' = {
  name: 'secrets-identity'
  scope: rg
  params: {
    location: location
  }
}
module keyvault 'keyvault/keyvault.module.bicep' = {
  name: 'keyvault'
  scope: rg
  params: {
    location: location
    gateway_secret: gateway_secret
    jwt_signing_key: jwt_signing_key
    messaging_password: messaging_password
    secrets_identity_principal_id: secrets_identity.outputs.principalId
  }
}
module consolidation_identity 'consolidation-identity/consolidation-identity.module.bicep' = {
  name: 'consolidation-identity'
  scope: rg
  params: {
    location: location
  }
}
module consolidation_roles_postgres 'consolidation-roles-postgres/consolidation-roles-postgres.module.bicep' = {
  name: 'consolidation-roles-postgres'
  scope: rg
  params: {
    location: location
    postgres_outputs_name: postgres.outputs.name
    principalId: consolidation_identity.outputs.principalId
    principalName: consolidation_identity.outputs.principalName
  }
}
module env 'env/env.module.bicep' = {
  name: 'env'
  scope: rg
  params: {
    env_acr_outputs_name: env_acr.outputs.name
    location: location
    userPrincipalId: principalId
  }
}
module env_acr 'env-acr/env-acr.module.bicep' = {
  name: 'env-acr'
  scope: rg
  params: {
    location: location
  }
}
module identity_identity 'identity-identity/identity-identity.module.bicep' = {
  name: 'identity-identity'
  scope: rg
  params: {
    location: location
  }
}
module identity_roles_postgres 'identity-roles-postgres/identity-roles-postgres.module.bicep' = {
  name: 'identity-roles-postgres'
  scope: rg
  params: {
    location: location
    postgres_outputs_name: postgres.outputs.name
    principalId: identity_identity.outputs.principalId
    principalName: identity_identity.outputs.principalName
  }
}
module postgres 'postgres/postgres.module.bicep' = {
  name: 'postgres'
  scope: rg
  params: {
    location: location
  }
}
module transactions_identity 'transactions-identity/transactions-identity.module.bicep' = {
  name: 'transactions-identity'
  scope: rg
  params: {
    location: location
  }
}
module transactions_roles_postgres 'transactions-roles-postgres/transactions-roles-postgres.module.bicep' = {
  name: 'transactions-roles-postgres'
  scope: rg
  params: {
    location: location
    postgres_outputs_name: postgres.outputs.name
    principalId: transactions_identity.outputs.principalId
    principalName: transactions_identity.outputs.principalName
  }
}
output AZURE_CONTAINER_APPS_ENVIRONMENT_DEFAULT_DOMAIN string = env.outputs.AZURE_CONTAINER_APPS_ENVIRONMENT_DEFAULT_DOMAIN
output AZURE_CONTAINER_REGISTRY_ENDPOINT string = env.outputs.AZURE_CONTAINER_REGISTRY_ENDPOINT
output CONSOLIDATION_IDENTITY_CLIENTID string = consolidation_identity.outputs.clientId
output CONSOLIDATION_IDENTITY_ID string = consolidation_identity.outputs.id
output CONSOLIDATION_IDENTITY_PRINCIPALNAME string = consolidation_identity.outputs.principalName
output ENV_AZURE_CONTAINER_APPS_ENVIRONMENT_DEFAULT_DOMAIN string = env.outputs.AZURE_CONTAINER_APPS_ENVIRONMENT_DEFAULT_DOMAIN
output ENV_AZURE_CONTAINER_APPS_ENVIRONMENT_ID string = env.outputs.AZURE_CONTAINER_APPS_ENVIRONMENT_ID
output ENV_AZURE_CONTAINER_REGISTRY_ENDPOINT string = env.outputs.AZURE_CONTAINER_REGISTRY_ENDPOINT
output ENV_AZURE_CONTAINER_REGISTRY_MANAGED_IDENTITY_ID string = env.outputs.AZURE_CONTAINER_REGISTRY_MANAGED_IDENTITY_ID
output ENV_VOLUMES_MESSAGING_0 string = env.outputs.volumes_messaging_0
output IDENTITY_IDENTITY_CLIENTID string = identity_identity.outputs.clientId
output IDENTITY_IDENTITY_ID string = identity_identity.outputs.id
output IDENTITY_IDENTITY_PRINCIPALNAME string = identity_identity.outputs.principalName
output POSTGRES_CONNECTIONSTRING string = postgres.outputs.connectionString
output POSTGRES_HOSTNAME string = postgres.outputs.hostName
output TRANSACTIONS_IDENTITY_CLIENTID string = transactions_identity.outputs.clientId
output TRANSACTIONS_IDENTITY_ID string = transactions_identity.outputs.id
output TRANSACTIONS_IDENTITY_PRINCIPALNAME string = transactions_identity.outputs.principalName
output APPLICATIONINSIGHTS_CONNECTION_STRING string = env.outputs.APPLICATIONINSIGHTS_CONNECTION_STRING
output KEYVAULT_URI string = keyvault.outputs.vaultUri
output SECRETS_IDENTITY_ID string = secrets_identity.outputs.id

module monitoring 'monitoring/alerts.module.bicep' = if (!empty(alert_email_address)) {
  name: 'monitoring-alerts'
  scope: rg
  params: {
    location: location
    applicationInsightsId: env.outputs.APPLICATIONINSIGHTS_ID
    alertEmailAddress: alert_email_address
    tags: tags
  }
}

module workbooks 'monitoring/workbooks.module.bicep' = {
  name: 'monitoring-workbooks'
  scope: rg
  params: {
    location: location
    applicationInsightsId: env.outputs.APPLICATIONINSIGHTS_ID
    tags: tags
  }
}
