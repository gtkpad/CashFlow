# ADR-012: PostgreSQL Scaling: General Purpose SKU + PgBouncer Built-in

| Campo | Valor |
|---|---|
| **Status** | Aceito |
| **Data** | 2026-03-08 |
| **Contexto** | PostgreSQL Standard_B1ms saturou a 94.9% CPU sob 50 req/s, causando cascata de timeouts |
| **Decisão** | Upgrade para Standard_D2ds_v4 (General Purpose) + PgBouncer built-in do Azure |

## Detalhes

### Problema

Testes de carga k6 em produção (50 req/s, 2 min) com scaling rules HTTP (ADR-011) ativas revelaram que o **PostgreSQL é o bottleneck real** do sistema:

| Métrica | Valor Observado | Target NFR |
|---------|-----------------|------------|
| NFR-4 p95 (Transactions POST) | 14,685ms | < 500ms |
| NFR-2 p95 (Consolidation GET) | 9,779ms | < 200ms |
| NFR-2 error_rate | 38.97% | < 1% |
| PostgreSQL CPU | 94.9% | < 70% |
| Conexões ativas | 45 (de 50 max) | N/A |

O tier **Burstable (Standard_B1ms)** agrava o problema: após esgotar CPU credits (~60s de carga sustentada), a capacidade efetiva cai para ~0.2 vCPU (20% baseline). Container Apps escalaram para 10 réplicas corretamente, mas todas competiam pelo mesmo PostgreSQL saturado.

### Solução

#### 1. Upgrade PostgreSQL para Standard_D2ds_v4 (General Purpose)

| | B1ms (anterior) | D2ds_v4 (atual) |
|--|----------------|-----------------|
| vCPUs | 1 (burstable) | 2 (dedicados) |
| RAM | 2 GB | 8 GB |
| max_connections | 50 | 196 |
| IOPS baseline | 450 (burst 640) | 3,450 |
| Storage IOPS | burst only | provisionado |
| Custo estimado | ~$13/mês | ~$125/mês |

**Cálculo via Little's Law**: A 50 req/s com 2 writes/request (transaction + outbox) = 100 writes/s. Com latência média de 5ms por write em D2ds_v4 = 0.5 vCPU de trabalho sustentado. 2 vCPUs dedicados dão **4x headroom**.

#### 2. PgBouncer Built-in (Azure-native, custo zero)

O Azure PostgreSQL Flexible Server oferece PgBouncer integrado (porta 6432, modo `transaction`). Não requer sidecar container nem infraestrutura adicional.

**Configuração aplicada**:
- `pgbouncer.enabled = true`
- `pgbouncer.default_pool_size = 50`
- Connection string atualizada: `Host=...;Port=6432;Ssl Mode=Require`

**Benefícios**:
- Multiplexação: 45 conexões de app → ~5-10 conexões reais ao PostgreSQL
- Reduz overhead de connection setup (TLS handshake, auth)
- Proteção contra connection storms durante scale-out de Container Apps

#### 3. Decisão de NÃO usar Read Replicas

O output cache do Consolidation (5s TTL current date, 1h past dates) resolve leituras repetitivas. Com D2ds_v4, capacidade estimada de 2,000-5,000 queries/s — **40-100x acima** do target de 50 req/s.

Read replicas adicionariam complexidade (replication lag, routing logic, custo ~$125/mês extra) sem benefício proporcional no volume atual.

### Configuração Bicep

```bicep
// infra/postgres/postgres.module.bicep
param skuName string = 'Standard_D2ds_v4'
param skuTier string = 'GeneralPurpose'
param pgBouncerEnabled bool = true

resource pgBouncerEnabled_config '...' = if (pgBouncerEnabled) {
  name: 'pgbouncer.enabled'
  properties: { value: 'true', source: 'user-override' }
}

resource pgBouncerDefaultPoolSize '...' = if (pgBouncerEnabled) {
  name: 'pgbouncer.default_pool_size'
  properties: { value: '50', source: 'user-override' }
}

// Connection string com porta condicional
output connectionString string = pgBouncerEnabled
  ? 'Host=${...};Port=6432;Ssl Mode=Require'
  : 'Host=${...};Ssl Mode=Require'
```

## Trade-offs

| Aspecto | Burstable B1ms | General Purpose D2ds_v4 (escolhido) |
|---------|---------------|--------------------------------------|
| **Custo** | ~$13/mês | ~$125/mês (+$112) |
| **CPU** | 1 vCPU com credit depletion | 2 vCPUs dedicados, sustentados |
| **Conexões** | 50 max | 196 max |
| **IOPS** | 450 baseline (burst 640) | 3,450 baseline |
| **Adequação** | Dev/staging | Produção com carga sustentada |

| Aspecto | PgBouncer Built-in (escolhido) | PgBouncer Sidecar Container |
|---------|-------------------------------|----------------------------|
| **Custo** | $0 (incluso) | CPU/mem adicional no Container App |
| **Manutenção** | Zero — managed pelo Azure | Atualização de imagem, health checks |
| **Configuração** | Server parameters no Bicep | Container config + networking |
| **Limitações** | Modo transaction only | Modos transaction, session, statement |

| Aspecto | Com Read Replicas | Sem Read Replicas (escolhido) |
|---------|------------------|-------------------------------|
| **Capacidade leitura** | Ilimitada (escala horizontal) | 2,000-5,000 q/s (suficiente) |
| **Custo** | +~$125/mês por réplica | $0 |
| **Complexidade** | Routing logic, replication lag | Nenhuma |
| **Adequação** | > 1,000 req/s leituras | < 100 req/s (caso atual) |

## Consequências

### Positivas

- **NFR compliance**: PostgreSQL com headroom de 4x elimina o bottleneck que causava todos os timeouts
- **Zero downtime futuro**: vCPUs dedicados não sofrem credit depletion sob carga sustentada
- **Connection pooling**: PgBouncer protege contra storms durante auto-scaling dos Container Apps
- **Zero alteração em código**: Connection string flui via output Bicep → app configuration automaticamente

### Negativas

- **Custo incremental**: +$112/mês (87% de aumento no custo total de infra)
- **Downtime de upgrade**: ~2-5min durante restart do servidor PostgreSQL para mudança de SKU
- **Over-provisioning em idle**: 2 vCPUs dedicados mesmo sem carga (mitigado pelo custo baixo absoluto)

### Limites de Migração

- Se carga ultrapassar 200 req/s sustentados: considerar Standard_D4ds_v4 (4 vCPUs, ~$250/mês)
- Se leituras ultrapassarem 1,000 req/s: considerar read replica
- Se custo se tornar concern: considerar downgrade para Standard_D2s_v3 (~$100/mês, sem local SSD)
- Se PgBouncer mode `transaction` causar issues com prepared statements: configurar `pgbouncer.ignore_startup_parameters` ou desabilitar prepared statements no Npgsql
