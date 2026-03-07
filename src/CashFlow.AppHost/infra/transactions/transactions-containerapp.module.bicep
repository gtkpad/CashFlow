@description('The location for the resource(s) to be deployed.')
param location string = resourceGroup().location

param env_outputs_azure_container_apps_environment_default_domain string

param env_outputs_azure_container_apps_environment_id string

param transactions_containerimage string

param transactions_identity_outputs_id string

param transactions_containerport string

param postgres_outputs_connectionstring string

param postgres_outputs_hostname string

param keyvault_uri string

param secrets_identity_id string

param transactions_identity_outputs_clientid string

param transactions_identity_outputs_principalname string

param env_outputs_azure_container_registry_endpoint string

param env_outputs_azure_container_registry_managed_identity_id string

param applicationinsights_connection_string string = ''

param otel_service_version string = '1.0.0'

resource transactions 'Microsoft.App/containerApps@2025-02-02-preview' = {
  name: 'transactions'
  location: location
  properties: {
    configuration: {
      secrets: [
        {
          name: 'connectionstrings--messaging'
          keyVaultUrl: '${keyvault_uri}secrets/messaging-uri'
          identity: secrets_identity_id
        }
        {
          name: 'messaging-password'
          keyVaultUrl: '${keyvault_uri}secrets/messaging-password'
          identity: secrets_identity_id
        }
        {
          name: 'messaging-uri'
          keyVaultUrl: '${keyvault_uri}secrets/messaging-uri'
          identity: secrets_identity_id
        }
        {
          name: 'gateway--secret'
          keyVaultUrl: '${keyvault_uri}secrets/gateway-secret'
          identity: secrets_identity_id
        }
      ]
      activeRevisionsMode: 'Single'
      ingress: {
        external: false
        targetPort: int(transactions_containerport)
        transport: 'http'
      }
      registries: [
        {
          server: env_outputs_azure_container_registry_endpoint
          identity: env_outputs_azure_container_registry_managed_identity_id
        }
      ]
      runtime: {
        dotnet: {
          autoConfigureDataProtection: true
        }
      }
    }
    environmentId: env_outputs_azure_container_apps_environment_id
    template: {
      containers: [
        {
          image: transactions_containerimage
          name: 'transactions'
          env: [
            {
              name: 'OTEL_DOTNET_EXPERIMENTAL_OTLP_EMIT_EXCEPTION_LOG_ATTRIBUTES'
              value: 'true'
            }
            {
              name: 'OTEL_DOTNET_EXPERIMENTAL_OTLP_EMIT_EVENT_LOG_ATTRIBUTES'
              value: 'true'
            }
            {
              name: 'OTEL_DOTNET_EXPERIMENTAL_OTLP_RETRY'
              value: 'in_memory'
            }
            {
              name: 'ASPNETCORE_FORWARDEDHEADERS_ENABLED'
              value: 'true'
            }
            {
              name: 'HTTP_PORTS'
              value: transactions_containerport
            }
            {
              name: 'ConnectionStrings__transactions-db'
              value: '${postgres_outputs_connectionstring};Database=transactions-db;Username=${transactions_identity_outputs_principalname}'
            }
            {
              name: 'TRANSACTIONS_DB_HOST'
              value: postgres_outputs_hostname
            }
            {
              name: 'TRANSACTIONS_DB_PORT'
              value: '5432'
            }
            {
              name: 'TRANSACTIONS_DB_URI'
              value: 'postgresql://${postgres_outputs_hostname}/transactions-db'
            }
            {
              name: 'TRANSACTIONS_DB_JDBCCONNECTIONSTRING'
              value: 'jdbc:postgresql://${postgres_outputs_hostname}/transactions-db?sslmode=require&authenticationPluginClassName=com.azure.identity.extensions.jdbc.postgresql.AzurePostgresqlAuthenticationPlugin'
            }
            {
              name: 'TRANSACTIONS_DB_DATABASENAME'
              value: 'transactions-db'
            }
            {
              name: 'ConnectionStrings__messaging'
              secretRef: 'connectionstrings--messaging'
            }
            {
              name: 'MESSAGING_HOST'
              value: 'messaging'
            }
            {
              name: 'MESSAGING_PORT'
              value: '5672'
            }
            {
              name: 'MESSAGING_USERNAME'
              value: 'guest'
            }
            {
              name: 'MESSAGING_PASSWORD'
              secretRef: 'messaging-password'
            }
            {
              name: 'MESSAGING_URI'
              secretRef: 'messaging-uri'
            }
            {
              name: 'Gateway__Secret'
              secretRef: 'gateway--secret'
            }
            {
              name: 'APPLICATIONINSIGHTS_CONNECTION_STRING'
              value: applicationinsights_connection_string
            }
            {
              name: 'OTEL_SERVICE_VERSION'
              value: otel_service_version
            }
            {
              name: 'AZURE_CLIENT_ID'
              value: transactions_identity_outputs_clientid
            }
            {
              name: 'AZURE_TOKEN_CREDENTIALS'
              value: 'ManagedIdentityCredential'
            }
          ]
          probes: [
            {
              type: 'liveness'
              httpGet: {
                path: '/alive'
                port: int(transactions_containerport)
              }
              initialDelaySeconds: 5
              periodSeconds: 10
              failureThreshold: 3
              successThreshold: 1
              timeoutSeconds: 5
            }
            {
              type: 'readiness'
              httpGet: {
                path: '/health'
                port: int(transactions_containerport)
              }
              initialDelaySeconds: 10
              periodSeconds: 15
              failureThreshold: 3
              successThreshold: 1
              timeoutSeconds: 5
            }
            {
              type: 'startup'
              httpGet: {
                path: '/alive'
                port: int(transactions_containerport)
              }
              initialDelaySeconds: 3
              periodSeconds: 5
              failureThreshold: 12
              timeoutSeconds: 5
            }
          ]
        }
      ]
      scale: {
        minReplicas: 1
      }
    }
  }
  identity: {
    type: 'UserAssigned'
    userAssignedIdentities: {
      '${transactions_identity_outputs_id}': { }
      '${env_outputs_azure_container_registry_managed_identity_id}': { }
      '${secrets_identity_id}': { }
    }
  }
}