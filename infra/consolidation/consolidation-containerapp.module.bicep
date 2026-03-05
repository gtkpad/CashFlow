@description('The location for the resource(s) to be deployed.')
param location string = resourceGroup().location

param env_outputs_azure_container_apps_environment_default_domain string

param env_outputs_azure_container_apps_environment_id string

param consolidation_containerimage string

param consolidation_identity_outputs_id string

param consolidation_containerport string

param postgres_outputs_connectionstring string

param postgres_outputs_hostname string

@secure()
param messaging_password_value string

@secure()
param gateway_secret_value string

param consolidation_identity_outputs_clientid string

param env_outputs_azure_container_registry_endpoint string

param env_outputs_azure_container_registry_managed_identity_id string

resource consolidation 'Microsoft.App/containerApps@2025-02-02-preview' = {
  name: 'consolidation'
  location: location
  properties: {
    configuration: {
      secrets: [
        {
          name: 'connectionstrings--messaging'
          value: 'amqp://guest:${uriComponent(messaging_password_value)}@messaging:5672'
        }
        {
          name: 'messaging-password'
          value: messaging_password_value
        }
        {
          name: 'messaging-uri'
          value: 'amqp://guest:${uriComponent(messaging_password_value)}@messaging:5672'
        }
        {
          name: 'gateway--secret'
          value: gateway_secret_value
        }
      ]
      activeRevisionsMode: 'Single'
      ingress: {
        external: false
        targetPort: int(consolidation_containerport)
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
          image: consolidation_containerimage
          name: 'consolidation'
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
              value: consolidation_containerport
            }
            {
              name: 'ConnectionStrings__consolidation-db'
              value: '${postgres_outputs_connectionstring};Database=consolidation-db'
            }
            {
              name: 'CONSOLIDATION_DB_HOST'
              value: postgres_outputs_hostname
            }
            {
              name: 'CONSOLIDATION_DB_PORT'
              value: '5432'
            }
            {
              name: 'CONSOLIDATION_DB_URI'
              value: 'postgresql://${postgres_outputs_hostname}/consolidation-db'
            }
            {
              name: 'CONSOLIDATION_DB_JDBCCONNECTIONSTRING'
              value: 'jdbc:postgresql://${postgres_outputs_hostname}/consolidation-db?sslmode=require&authenticationPluginClassName=com.azure.identity.extensions.jdbc.postgresql.AzurePostgresqlAuthenticationPlugin'
            }
            {
              name: 'CONSOLIDATION_DB_DATABASENAME'
              value: 'consolidation-db'
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
              name: 'AZURE_CLIENT_ID'
              value: consolidation_identity_outputs_clientid
            }
            {
              name: 'AZURE_TOKEN_CREDENTIALS'
              value: 'ManagedIdentityCredential'
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
      '${consolidation_identity_outputs_id}': { }
      '${env_outputs_azure_container_registry_managed_identity_id}': { }
    }
  }
}