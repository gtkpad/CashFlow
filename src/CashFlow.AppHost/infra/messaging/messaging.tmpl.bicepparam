using './messaging-containerapp.module.bicep'

param env_outputs_azure_container_apps_environment_default_domain = '{{ .Env.ENV_AZURE_CONTAINER_APPS_ENVIRONMENT_DEFAULT_DOMAIN }}'
param env_outputs_azure_container_apps_environment_id = '{{ .Env.ENV_AZURE_CONTAINER_APPS_ENVIRONMENT_ID }}'
param env_outputs_volumes_messaging_0 = '{{ .Env.ENV_VOLUMES_MESSAGING_0 }}'
param messaging_password_value = '{{ securedParameter "messaging_password" }}'
