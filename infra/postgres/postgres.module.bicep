@description('The location for the resource(s) to be deployed.')
param location string = resourceGroup().location

@description('PostgreSQL SKU name. Use Standard_B1ms for dev, Standard_D2ds_v4 for production.')
param skuName string = 'Standard_D2ds_v4'

@description('PostgreSQL SKU tier. Use Burstable for dev, GeneralPurpose for production.')
param skuTier string = 'GeneralPurpose'

@description('Enable PgBouncer connection pooling on port 6432.')
param pgBouncerEnabled bool = true

@description('Backup retention in days. Minimum 7 (dev), recommended 35 (production).')
param backupRetentionDays int = 35

@description('Enable geo-redundant backup for cross-region disaster recovery.')
param geoRedundantBackup string = 'Enabled'

@description('High availability mode. Disabled for dev/Burstable, ZoneRedundant for production with GeneralPurpose/MemoryOptimized tier.')
param highAvailabilityMode string = 'Disabled'

@description('Storage size in GB. Minimum 32 for dev.')
param storageSizeGB int = 32

resource postgres 'Microsoft.DBforPostgreSQL/flexibleServers@2024-08-01' = {
  name: take('postgres-${uniqueString(resourceGroup().id)}', 63)
  location: location
  properties: {
    authConfig: {
      activeDirectoryAuth: 'Enabled'
      passwordAuth: 'Disabled'
    }
    availabilityZone: '1'
    backup: {
      backupRetentionDays: backupRetentionDays
      geoRedundantBackup: geoRedundantBackup
    }
    highAvailability: {
      mode: highAvailabilityMode
    }
    storage: {
      storageSizeGB: storageSizeGB
      autoGrow: 'Enabled'
    }
    version: '16'
  }
  sku: {
    name: skuName
    tier: skuTier
  }
  tags: {
    'aspire-resource-name': 'postgres'
  }
}

resource postgreSqlFirewallRule_AllowAllAzureIps 'Microsoft.DBforPostgreSQL/flexibleServers/firewallRules@2024-08-01' = {
  name: 'AllowAllAzureIps'
  properties: {
    endIpAddress: '0.0.0.0'
    startIpAddress: '0.0.0.0'
  }
  parent: postgres
}

resource identity_db 'Microsoft.DBforPostgreSQL/flexibleServers/databases@2024-08-01' = {
  name: 'identity-db'
  parent: postgres
}

resource transactions_db 'Microsoft.DBforPostgreSQL/flexibleServers/databases@2024-08-01' = {
  name: 'transactions-db'
  parent: postgres
}

resource consolidation_db 'Microsoft.DBforPostgreSQL/flexibleServers/databases@2024-08-01' = {
  name: 'consolidation-db'
  parent: postgres
}

resource pgBouncerEnabled_config 'Microsoft.DBforPostgreSQL/flexibleServers/configurations@2024-08-01' = if (pgBouncerEnabled) {
  name: 'pgbouncer.enabled'
  parent: postgres
  properties: {
    value: 'true'
    source: 'user-override'
  }
}

resource pgBouncerDefaultPoolSize 'Microsoft.DBforPostgreSQL/flexibleServers/configurations@2024-08-01' = if (pgBouncerEnabled) {
  name: 'pgbouncer.default_pool_size'
  parent: postgres
  properties: {
    value: '50'
    source: 'user-override'
  }
  dependsOn: [pgBouncerEnabled_config]
}

resource pgBouncerMaxClientConn 'Microsoft.DBforPostgreSQL/flexibleServers/configurations@2024-08-01' = if (pgBouncerEnabled) {
  name: 'pgbouncer.max_client_conn'
  parent: postgres
  properties: {
    value: '150'
    source: 'user-override'
  }
  dependsOn: [pgBouncerDefaultPoolSize]
}

output connectionString string = pgBouncerEnabled
  ? 'Host=${postgres.properties.fullyQualifiedDomainName};Port=6432;Ssl Mode=Require'
  : 'Host=${postgres.properties.fullyQualifiedDomainName};Ssl Mode=Require'

output name string = postgres.name

output hostName string = postgres.properties.fullyQualifiedDomainName