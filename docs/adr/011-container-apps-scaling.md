# ADR-011: Auto-Scaling: HTTP Scaling Rules + Dimensionamento por Perfil de Carga

| Campo | Valor |
|---|---|
| **Status** | Aceito |
| **Data** | 2026-03-08 |
| **Contexto** | Testes de carga revelaram saturação de CPU em réplica única sob 50 req/s |
| **Decisão** | HTTP concurrent requests scaling com resources diferenciados por perfil de serviço |

## Detalhes

### Problema

Testes de carga k6 em produção (50 req/s, 2 min) revelaram dois problemas:

1. **NFR-4 (Transactions POST)**: p95 = 23.8s — réplica única (0.5 vCPU default) satura sob carga; sem scaling rules HTTP configuradas; apenas 27% do throughput alvo (1,607 de 6,000 requests)
2. **NFR-2 (Consolidation GET)**: error_rate = 99.98% — teste executava antes de dados existirem, endpoint retorna 404 corretamente, k6 contabilizava como falha

A infraestrutura tinha `scale: { minReplicas: 1 }` em todos os 5 Container Apps, sem `maxReplicas` nem `rules`.

### Solução

Configuração de scaling HTTP por serviço, calibrada via Little's Law:

> **Little's Law**: A 50 req/s com 200ms latência média = 10 requests concorrentes. Com threshold de 15, 1 réplica suporta até ~75 req/s antes de escalar.

| Serviço | CPU | Memory | Min | Max | HTTP Threshold | Justificativa |
|---------|-----|--------|-----|-----|----------------|---------------|
| **Transactions** | 1.0 | 2Gi | 1 | 5 | 15 concurrent | Cada POST = 2 DB writes (transaction + outbox). Dobro de CPU/mem |
| **Gateway** | 0.5 | 1Gi | 1 | 5 | 20 concurrent | Proxy YARP — I/O bound, CPU baixo. Mesma escala que transactions |
| **Consolidation** | 0.5 | 1Gi | 1 | 3 | 30 concurrent | Queries read-only com output cache. Menor pressão |
| **Identity** | 0.5 | 1Gi | 1 | 2 | 25 concurrent | Login/register — baixo tráfego comparado |
| **Messaging** | 1.0 | 2Gi | 1 | 1 | N/A | Stateful (Azure File volume). Não escala horizontalmente |

### Configuração Bicep

```bicep
// Exemplo: transactions-containerapp.module.bicep
scale: {
  minReplicas: 1
  maxReplicas: 5
  rules: [
    {
      name: 'http-scaling'
      http: {
        metadata: {
          concurrentRequests: '15'
        }
      }
    }
  ]
}
```

### Correções nos Testes de Carga

1. **Warm-up**: 10 transações de seed + 10s wait antes dos testes
2. **Ordem invertida**: NFR-4 (transactions) primeiro, depois NFR-2 (consolidation) — garante dados existirem
3. **ramping-arrival-rate**: Ramp-up de 30s (transactions) e 15s (consolidation) em vez de carga instantânea
4. **Check tolerante**: NFR-2 aceita 200 e 404 (sem dados = comportamento correto, não erro)

## Trade-offs

| Aspecto | HTTP Scaling | CPU/Memory Scaling |
|---------|-------------|-------------------|
| **Reatividade** | Proativo (escala antes de saturar) | Reativo (escala após saturação) |
| **Precisão** | Baseado em carga real de requests | Baseado em métricas genéricas |
| **Cold start** | Mitigado por ramp-up nos testes | Igual |
| **Custo** | Scale-out mais agressivo | Mais conservador |
| **Simplicidade** | 1 regra por serviço | Múltiplas métricas possíveis |

| Aspecto | Scaling Uniforme | Scaling por Perfil (escolhido) |
|---------|-----------------|-------------------------------|
| **Custo** | Sobre-provisionamento de serviços leves | Otimizado por perfil de carga |
| **Complexidade** | Config única para todos | Config diferenciada por serviço |
| **Eficiência** | Identity com 5 réplicas desnecessárias | Identity com max 2 (suficiente) |

## Consequências

### Positivas

- **NFR-4**: Transactions escala para 3-5 réplicas sob carga, distribuindo os 2 DB writes/request
- **NFR-2**: Consolidation com output cache + 3 réplicas suporta facilmente 50 req/s read-only
- **Custo otimizado**: Min replicas = 1 em todos os serviços; scale-out apenas sob demanda
- **Testes confiáveis**: Warm-up + ordem correta + ramping eliminam falsos negativos

### Negativas

- **Custo incremental**: Sob carga de pico, até 5 réplicas de transactions (5x custo desse serviço)
- **Cold start**: Novas réplicas levam ~30-45s para ficar prontas (mitigado pelo ramp-up)
- **Messaging fixo**: RabbitMQ não escala horizontalmente — gargalo potencial em cargas extremas

### Limites de Migração

- Se Messaging se tornar gargalo: considerar Azure Service Bus (managed, auto-scaling)
- Se cold start impactar p95: aumentar minReplicas para 2 nos serviços críticos (transactions, gateway)
- Se custo exceder budget: reduzir maxReplicas ou aumentar thresholds de concorrência
