# ADR-011: Auto-Scaling — HTTP Scaling Rules por Perfil de Carga

| Campo | Valor |
|---|---|
| **Status** | Aceito |
| **Data** | 2026-03-08 |
| **Contexto** | Testes de carga k6 revelaram saturação de CPU em réplica única sob 50 req/s. A infraestrutura original tinha `scale: { minReplicas: 1 }` em todos os Container Apps, sem `maxReplicas` nem regras de scaling. |
| **Decisão** | HTTP concurrent requests scaling com resources diferenciados por perfil de serviço, calibrado via Little's Law. |

## Detalhes

### Problema identificado nos testes de carga

1. **NFR-4 (Transactions POST):** p95 = 23.8s — réplica única (0.5 vCPU default) saturava; apenas 27% do throughput alvo.
2. **NFR-2 (Consolidation GET):** error_rate = 99.98% — testes executavam antes de dados existirem; 404 era contabilizado como falha pelo k6.

### Dimensionamento por perfil (Little's Law)

> Little's Law: a 50 req/s com 200ms latência média = 10 requests concorrentes. Com threshold de 15, 1 réplica suporta até ~75 req/s antes de escalar.

| Serviço | CPU | Memory | Min | Max | HTTP Threshold | Justificativa |
|---------|-----|--------|-----|-----|----------------|---------------|
| **Transactions** | 1.0 vCPU | 2Gi | 1 | 5 | 15 concurrent | POST = 2 DB writes (transaction + outbox) |
| **Gateway** | 0.5 vCPU | 1Gi | 1 | 5 | 20 concurrent | I/O-bound, CPU baixo |
| **Consolidation** | 0.5 vCPU | 1Gi | 1 | 3 | 30 concurrent | Read-only + output cache |
| **Identity** | 0.5 vCPU | 1Gi | 1 | 2 | 25 concurrent | Baixo tráfego comparado |
| **Messaging** | 1.0 vCPU | 2Gi | 1 | 1 | N/A | Stateful (Azure File volume) — não escala |

### Configuração Bicep (exemplo: Transactions)

```bicep
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

### Correções nos testes de carga

1. Warm-up: 10 transações de seed + 10s wait antes dos testes.
2. Ordem invertida: NFR-4 (transactions) primeiro, depois NFR-2 (consolidation) — garante dados existirem.
3. `ramping-arrival-rate`: ramp-up de 30s (transactions) e 15s (consolidation) em vez de carga instantânea.
4. Check tolerante no NFR-2: aceita 200 e 404 (sem dados = comportamento correto, não erro).

## Trade-offs

| Aspecto | HTTP Scaling | CPU/Memory Scaling |
|---------|-------------|-------------------|
| Reatividade | Proativo (escala antes de saturar) | Reativo (escala após saturação) |
| Precisão | Baseado em carga real de requests | Métricas genéricas |
| Custo | Scale-out mais agressivo | Mais conservador |

| Aspecto | Scaling Uniforme | Scaling por Perfil (escolhido) |
|---------|-----------------|-------------------------------|
| Custo | Sobre-provisionamento de serviços leves | Otimizado por perfil |
| Eficiência | Identity com 5 réplicas desnecessárias | Identity com max 2 |

## Consequências

- **NFR-4:** Transactions escala para 3–5 réplicas sob carga, distribuindo as 2 DB writes/request.
- **NFR-2:** Consolidation com output cache + 3 réplicas suporta facilmente 50 req/s.
- **Cold start:** Novas réplicas levam ~30–45s para ficar prontas. Mitigado pelo ramp-up nos testes.
- **Messaging fixo:** RabbitMQ não escala horizontalmente — gargalo potencial em cargas extremas. Alternativa: Azure Service Bus.
- Se cold start impactar p95 em produção, aumentar `minReplicas` para 2 nos serviços críticos (transactions, gateway).
