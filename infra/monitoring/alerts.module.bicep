@description('The location for the resource(s) to be deployed.')
param location string = resourceGroup().location

@description('Resource ID of the Application Insights instance.')
param applicationInsightsId string

@description('Email address for alert notifications.')
param alertEmailAddress string

param tags object = { }

resource actionGroup 'Microsoft.Insights/actionGroups@2023-01-01' = {
  name: 'cashflow-alerts-ag'
  location: 'global'
  properties: {
    groupShortName: 'cf-alerts'
    enabled: true
    emailReceivers: [
      {
        name: 'ops-email'
        emailAddress: alertEmailAddress
        useCommonAlertSchema: true
      }
    ]
  }
  tags: tags
}

// NFR-1: Health check unhealthy > 1min (Sev 1)
resource healthCheckAlert 'Microsoft.Insights/scheduledQueryRules@2023-03-15-preview' = {
  name: 'cashflow-healthcheck-failure'
  location: location
  properties: {
    displayName: 'Health Check Failure - Any Service > 2min'
    description: 'NFR-1: One or more CashFlow services are reporting unhealthy health checks for more than 2 minutes.'
    severity: 1
    enabled: true
    evaluationFrequency: 'PT1M'
    windowSize: 'PT5M'
    scopes: [
      applicationInsightsId
    ]
    criteria: {
      allOf: [
        {
          query: 'requests | where name has "health" | where success == false | summarize FailureCount = count() by cloud_RoleName, bin(timestamp, 1m) | where FailureCount > 0'
          timeAggregation: 'Count'
          operator: 'GreaterThan'
          threshold: 2
          failingPeriods: {
            numberOfEvaluationPeriods: 5
            minFailingPeriodsToAlert: 2
          }
        }
      ]
    }
    actions: {
      actionGroups: [
        actionGroup.id
      ]
    }
  }
  tags: tags
}

// NFR-2: p95 consolidation latency > 200ms for > 5min (Sev 2)
resource consolidationLatencyAlert 'Microsoft.Insights/scheduledQueryRules@2023-03-15-preview' = {
  name: 'cashflow-consolidation-latency-p95'
  location: location
  properties: {
    displayName: 'Consolidation Latency p95 > 200ms'
    description: 'NFR-2: Consolidation processing p95 latency exceeds 200ms for more than 5 minutes.'
    severity: 2
    enabled: true
    evaluationFrequency: 'PT1M'
    windowSize: 'PT5M'
    scopes: [
      applicationInsightsId
    ]
    criteria: {
      allOf: [
        {
          query: 'customMetrics | where name == "cashflow.consolidation.processing_duration_ms" | summarize p95 = percentile(value, 95) by bin(timestamp, 1m) | where p95 > 200'
          timeAggregation: 'Count'
          operator: 'GreaterThan'
          threshold: 5
          failingPeriods: {
            numberOfEvaluationPeriods: 5
            minFailingPeriodsToAlert: 5
          }
        }
      ]
    }
    actions: {
      actionGroups: [
        actionGroup.id
      ]
    }
  }
  tags: tags
}

// NFR-2: Consolidation throughput < 50 req/s (Sev 3, log only)
resource consolidationThroughputAlert 'Microsoft.Insights/scheduledQueryRules@2023-03-15-preview' = {
  name: 'cashflow-consolidation-throughput-low'
  location: location
  properties: {
    displayName: 'Consolidation Throughput < 50 req/s'
    description: 'NFR-2: Consolidation throughput has dropped below 50 requests per second.'
    severity: 3
    enabled: true
    evaluationFrequency: 'PT5M'
    windowSize: 'PT5M'
    scopes: [
      applicationInsightsId
    ]
    criteria: {
      allOf: [
        {
          query: 'requests | where cloud_RoleName == "consolidation" | where name !has "health" | summarize RequestCount = count() by bin(timestamp, 1m) | where RequestCount < 50'
          timeAggregation: 'Count'
          operator: 'GreaterThan'
          threshold: 3
          failingPeriods: {
            numberOfEvaluationPeriods: 5
            minFailingPeriodsToAlert: 3
          }
        }
      ]
    }
    actions: {
      actionGroups: []
    }
  }
  tags: tags
}

// NFR-3: DLQ depth > 0 (Sev 2)
resource dlqDepthAlert 'Microsoft.Insights/scheduledQueryRules@2023-03-15-preview' = {
  name: 'cashflow-dlq-depth'
  location: location
  properties: {
    displayName: 'Dead Letter Queue Depth > 0'
    description: 'NFR-3: Messages detected in the dead letter queue, indicating processing failures.'
    severity: 2
    enabled: true
    evaluationFrequency: 'PT1M'
    windowSize: 'PT5M'
    scopes: [
      applicationInsightsId
    ]
    criteria: {
      allOf: [
        {
          query: 'customMetrics | where name == "cashflow.messaging.dlq_faults" | summarize FaultCount = sum(valueSum) by bin(timestamp, 1m) | where FaultCount > 0'
          timeAggregation: 'Count'
          operator: 'GreaterThan'
          threshold: 0
          failingPeriods: {
            numberOfEvaluationPeriods: 5
            minFailingPeriodsToAlert: 1
          }
        }
      ]
    }
    actions: {
      actionGroups: [
        actionGroup.id
      ]
    }
  }
  tags: tags
}

// NFR-3: Delta transactions.created vs events_processed > 100 for > 10min (Sev 1)
resource eventualConsistencyDeltaAlert 'Microsoft.Insights/scheduledQueryRules@2023-03-15-preview' = {
  name: 'cashflow-consistency-delta'
  location: location
  properties: {
    displayName: 'Eventual Consistency Delta > 100 Events'
    description: 'NFR-3: The delta between transactions created and events processed exceeds 100 for more than 10 minutes, indicating a consistency gap.'
    severity: 1
    enabled: true
    evaluationFrequency: 'PT5M'
    windowSize: 'PT10M'
    scopes: [
      applicationInsightsId
    ]
    criteria: {
      allOf: [
        {
          query: 'let created = customMetrics | where name == "cashflow.transactions.created" | summarize Created = sum(valueSum) by bin(timestamp, 5m); let processed = customMetrics | where name == "cashflow.consolidation.events_processed" | summarize Processed = sum(valueSum) by bin(timestamp, 5m); created | join kind=leftouter processed on timestamp | extend Delta = Created - coalesce(Processed, 0) | where Delta > 100'
          timeAggregation: 'Count'
          operator: 'GreaterThan'
          threshold: 0
          failingPeriods: {
            numberOfEvaluationPeriods: 2
            minFailingPeriodsToAlert: 2
          }
        }
      ]
    }
    actions: {
      actionGroups: [
        actionGroup.id
      ]
    }
  }
  tags: tags
}

// NFR-4: Consolidation ingestion < 50 msg/s (Sev 2)
resource ingestionRateAlert 'Microsoft.Insights/scheduledQueryRules@2023-03-15-preview' = {
  name: 'cashflow-ingestion-rate-low'
  location: location
  properties: {
    displayName: 'Consolidation Ingestion Rate < 50 msg/s'
    description: 'NFR-4: Event ingestion rate has dropped below 50 messages per second.'
    severity: 2
    enabled: true
    evaluationFrequency: 'PT5M'
    windowSize: 'PT5M'
    scopes: [
      applicationInsightsId
    ]
    criteria: {
      allOf: [
        {
          query: 'customMetrics | where name == "cashflow.consolidation.events_processed" | summarize EventsPerMin = sum(valueSum) by bin(timestamp, 1m) | extend EventsPerSec = EventsPerMin / 60.0 | where EventsPerSec < 50'
          timeAggregation: 'Count'
          operator: 'GreaterThan'
          threshold: 3
          failingPeriods: {
            numberOfEvaluationPeriods: 5
            minFailingPeriodsToAlert: 3
          }
        }
      ]
    }
    actions: {
      actionGroups: [
        actionGroup.id
      ]
    }
  }
  tags: tags
}

// NFR-4: Eventual consistency p95 > 5000ms for > 5min (Sev 2)
resource eventualConsistencyLatencyAlert 'Microsoft.Insights/scheduledQueryRules@2023-03-15-preview' = {
  name: 'cashflow-eventual-consistency-p95'
  location: location
  properties: {
    displayName: 'Eventual Consistency p95 > 5000ms'
    description: 'NFR-4: Eventual consistency latency p95 exceeds 5000ms for more than 5 minutes.'
    severity: 2
    enabled: true
    evaluationFrequency: 'PT1M'
    windowSize: 'PT5M'
    scopes: [
      applicationInsightsId
    ]
    criteria: {
      allOf: [
        {
          query: 'customMetrics | where name == "cashflow.consolidation.eventual_consistency_ms" | summarize p95 = percentile(value, 95) by bin(timestamp, 1m) | where p95 > 5000'
          timeAggregation: 'Count'
          operator: 'GreaterThan'
          threshold: 5
          failingPeriods: {
            numberOfEvaluationPeriods: 5
            minFailingPeriodsToAlert: 5
          }
        }
      ]
    }
    actions: {
      actionGroups: [
        actionGroup.id
      ]
    }
  }
  tags: tags
}

// Health check failure any service > 2min (Sev 1) — covered by healthCheckAlert above

// HTTP 5xx rate > 5% for > 5min (Sev 2)
resource http5xxRateAlert 'Microsoft.Insights/scheduledQueryRules@2023-03-15-preview' = {
  name: 'cashflow-http-5xx-rate'
  location: location
  properties: {
    displayName: 'HTTP 5xx Error Rate > 5%'
    description: 'HTTP 5xx error rate exceeds 5% of total requests for more than 5 minutes.'
    severity: 2
    enabled: true
    evaluationFrequency: 'PT1M'
    windowSize: 'PT5M'
    scopes: [
      applicationInsightsId
    ]
    criteria: {
      allOf: [
        {
          query: 'requests | summarize Total = count(), Errors = countif(toint(resultCode) >= 500) by bin(timestamp, 1m) | extend ErrorRate = (Errors * 100.0) / Total | where ErrorRate > 5 and Total > 10'
          timeAggregation: 'Count'
          operator: 'GreaterThan'
          threshold: 5
          failingPeriods: {
            numberOfEvaluationPeriods: 5
            minFailingPeriodsToAlert: 5
          }
        }
      ]
    }
    actions: {
      actionGroups: [
        actionGroup.id
      ]
    }
  }
  tags: tags
}
