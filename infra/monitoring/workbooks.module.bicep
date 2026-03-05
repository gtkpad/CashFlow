@description('The location for the resource(s) to be deployed.')
param location string = resourceGroup().location

@description('Resource ID of the Application Insights instance.')
param applicationInsightsId string

param tags object = { }

var workbookId = guid(resourceGroup().id, 'cashflow-nfr-workbook')

resource workbook 'Microsoft.Insights/workbooks@2023-06-01' = {
  name: workbookId
  location: location
  kind: 'shared'
  properties: {
    displayName: 'CashFlow NFR Dashboard'
    category: 'workbook'
    sourceId: applicationInsightsId
    serializedData: string(loadJsonContent('workbook-definition.json'))
  }
  tags: tags
}
