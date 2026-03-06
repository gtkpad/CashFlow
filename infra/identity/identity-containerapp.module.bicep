@description('The location for the resource(s) to be deployed.')
param location string = resourceGroup().location

param env_outputs_azure_container_apps_environment_default_domain string

param env_outputs_azure_container_apps_environment_id string

param identity_containerimage string

param identity_identity_outputs_id string

param identity_containerport string

param postgres_outputs_connectionstring string

param postgres_outputs_hostname string

@secure()
param jwt_signing_key_value string

param identity_identity_outputs_clientid string

param identity_identity_outputs_principalname string

param env_outputs_azure_container_registry_endpoint string

param env_outputs_azure_container_registry_managed_identity_id string

param applicationinsights_connection_string string = ''

param otel_service_version string = '1.0.0'

resource identity 'Microsoft.App/containerApps@2025-02-02-preview' = {
  name: 'identity'
  location: location
  properties: {
    configuration: {
      secrets: [
        {
          name: 'jwt--signingkey'
          value: jwt_signing_key_value
        }
      ]
      activeRevisionsMode: 'Single'
      ingress: {
        external: false
        targetPort: int(identity_containerport)
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
          image: identity_containerimage
          name: 'identity'
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
              value: identity_containerport
            }
            {
              name: 'ConnectionStrings__identity-db'
              value: '${postgres_outputs_connectionstring};Database=identity-db;Username=${identity_identity_outputs_principalname}'
            }
            {
              name: 'IDENTITY_DB_HOST'
              value: postgres_outputs_hostname
            }
            {
              name: 'IDENTITY_DB_PORT'
              value: '5432'
            }
            {
              name: 'IDENTITY_DB_URI'
              value: 'postgresql://${postgres_outputs_hostname}/identity-db'
            }
            {
              name: 'IDENTITY_DB_JDBCCONNECTIONSTRING'
              value: 'jdbc:postgresql://${postgres_outputs_hostname}/identity-db?sslmode=require&authenticationPluginClassName=com.azure.identity.extensions.jdbc.postgresql.AzurePostgresqlAuthenticationPlugin'
            }
            {
              name: 'IDENTITY_DB_DATABASENAME'
              value: 'identity-db'
            }
            {
              name: 'Identity__Audience'
              value: 'cashflow-api'
            }
            {
              name: 'Jwt__SigningKey'
              secretRef: 'jwt--signingkey'
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
              value: identity_identity_outputs_clientid
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
      '${identity_identity_outputs_id}': { }
      '${env_outputs_azure_container_registry_managed_identity_id}': { }
    }
  }
}