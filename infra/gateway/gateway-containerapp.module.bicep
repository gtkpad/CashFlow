@description('The location for the resource(s) to be deployed.')
param location string = resourceGroup().location

param env_outputs_azure_container_apps_environment_default_domain string

param env_outputs_azure_container_apps_environment_id string

param gateway_containerimage string

param gateway_containerport string

@secure()
param jwt_signing_key_value string

@secure()
param gateway_secret_value string

param env_outputs_azure_container_registry_endpoint string

param env_outputs_azure_container_registry_managed_identity_id string

resource gateway 'Microsoft.App/containerApps@2025-02-02-preview' = {
  name: 'gateway'
  location: location
  properties: {
    configuration: {
      secrets: [
        {
          name: 'jwt--signingkey'
          value: jwt_signing_key_value
        }
        {
          name: 'gateway--secret'
          value: gateway_secret_value
        }
      ]
      activeRevisionsMode: 'Single'
      ingress: {
        external: true
        targetPort: int(gateway_containerport)
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
          image: gateway_containerimage
          name: 'gateway'
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
              value: gateway_containerport
            }
            {
              name: 'IDENTITY_HTTP'
              value: 'http://identity.internal.${env_outputs_azure_container_apps_environment_default_domain}'
            }
            {
              name: 'services__identity__http__0'
              value: 'http://identity.internal.${env_outputs_azure_container_apps_environment_default_domain}'
            }
            {
              name: 'IDENTITY_HTTPS'
              value: 'https://identity.internal.${env_outputs_azure_container_apps_environment_default_domain}'
            }
            {
              name: 'services__identity__https__0'
              value: 'https://identity.internal.${env_outputs_azure_container_apps_environment_default_domain}'
            }
            {
              name: 'TRANSACTIONS_HTTP'
              value: 'http://transactions.internal.${env_outputs_azure_container_apps_environment_default_domain}'
            }
            {
              name: 'services__transactions__http__0'
              value: 'http://transactions.internal.${env_outputs_azure_container_apps_environment_default_domain}'
            }
            {
              name: 'TRANSACTIONS_HTTPS'
              value: 'https://transactions.internal.${env_outputs_azure_container_apps_environment_default_domain}'
            }
            {
              name: 'services__transactions__https__0'
              value: 'https://transactions.internal.${env_outputs_azure_container_apps_environment_default_domain}'
            }
            {
              name: 'CONSOLIDATION_HTTP'
              value: 'http://consolidation.internal.${env_outputs_azure_container_apps_environment_default_domain}'
            }
            {
              name: 'services__consolidation__http__0'
              value: 'http://consolidation.internal.${env_outputs_azure_container_apps_environment_default_domain}'
            }
            {
              name: 'CONSOLIDATION_HTTPS'
              value: 'https://consolidation.internal.${env_outputs_azure_container_apps_environment_default_domain}'
            }
            {
              name: 'services__consolidation__https__0'
              value: 'https://consolidation.internal.${env_outputs_azure_container_apps_environment_default_domain}'
            }
            {
              name: 'Identity__ValidAudiences__0'
              value: 'cashflow-api'
            }
            {
              name: 'Jwt__SigningKey'
              secretRef: 'jwt--signingkey'
            }
            {
              name: 'Gateway__Secret'
              secretRef: 'gateway--secret'
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
      '${env_outputs_azure_container_registry_managed_identity_id}': { }
    }
  }
}