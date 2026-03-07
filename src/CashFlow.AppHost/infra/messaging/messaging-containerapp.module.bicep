@description('The location for the resource(s) to be deployed.')
param location string = resourceGroup().location

param env_outputs_azure_container_apps_environment_default_domain string

param env_outputs_azure_container_apps_environment_id string

param keyvault_uri string

param secrets_identity_id string

param env_outputs_volumes_messaging_0 string

resource messaging 'Microsoft.App/containerApps@2025-01-01' = {
  name: 'messaging'
  location: location
  properties: {
    configuration: {
      secrets: [
        {
          name: 'rabbitmq-default-pass'
          keyVaultUrl: '${keyvault_uri}secrets/messaging-password'
          identity: secrets_identity_id
        }
      ]
      activeRevisionsMode: 'Single'
      ingress: {
        external: false
        targetPort: 15672
        transport: 'http'
        additionalPortMappings: [
          {
            external: false
            targetPort: 5672
          }
        ]
      }
    }
    environmentId: env_outputs_azure_container_apps_environment_id
    template: {
      containers: [
        {
          image: 'docker.io/library/rabbitmq:4.2-management'
          name: 'messaging'
          env: [
            {
              name: 'RABBITMQ_DEFAULT_USER'
              value: 'guest'
            }
            {
              name: 'RABBITMQ_DEFAULT_PASS'
              secretRef: 'rabbitmq-default-pass'
            }
          ]
          volumeMounts: [
            {
              volumeName: 'v0'
              mountPath: '/var/lib/rabbitmq/mnesia'
            }
          ]
        }
      ]
      scale: {
        minReplicas: 1
      }
      volumes: [
        {
          name: 'v0'
          storageType: 'AzureFile'
          storageName: env_outputs_volumes_messaging_0
        }
      ]
    }
  }
  identity: {
    type: 'UserAssigned'
    userAssignedIdentities: {
      '${secrets_identity_id}': { }
    }
  }
}