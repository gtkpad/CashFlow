# ADR-012: PostgreSQL Scaling — Standard_D2ds_v4 + PgBouncer Built-in

| Campo | Valor |
|---|---|
| **Status** | Aceito |
| **Data** | 2026-03-08 |
| **Contexto** | Testes de carga k6 com scaling rules HTTP ativas ([ADR-011](011-container-apps-scaling.md)) revelaram que o PostgreSQL era o bottleneck real: CPU em 94.9%, p95 de 14.685ms para transactions e 9.779ms para consolidation, com error_rate de 38.97% no NFR-2. O tier Burstable (Standard_B1ms) agrava o problema: após esgotar CPU credits (~60s de carga sustentada), a capacidade cai para ~0.2 vCPU. |
| **Decisão** | Upgrade para Standard_D2ds_v4 (General Purpose, 2 vCPUs dedicados) + PgBouncer built-in do Azure PostgreSQL Flexible Server. |

## Detalhes

### Métricas observadas com Standard_B1ms

| Métrica | Observado | Target NFR |
|---------|-----------|------------|
| NFR-4 p95 (Transactions POST) | 14.685ms | < 500ms |
| NFR-2 p95 (Consolidation GET) | 9.779ms | < 200ms |
| NFR-2 error_rate | 38.97% | < 1% |
| PostgreSQL CPU | 94.9% | < 70% |
| Conexões ativas | 45 de 50 max | — |

### Upgrade: Standard_B1ms → Standard_D2ds_v4

| | B1ms (anterior) | D2ds_v4 (atual) |
|--|----------------|-----------------|
| vCPUs | 1 (burstable, credit-based) | 2 (dedicados, sustentados) |
| RAM | 2 GB | 8 GB |
| max_connections | 50 | 196 |
| IOPS baseline | 450 (burst 640) | 3.450 |
| Custo estimado | ~$13/mês | ~$125/mês |

**Cálculo via Little's Law:** a 50 req/s com 2 writes/request = 100 writes/s. Com latência média de 5ms por write em D2ds_v4 = 0.5 vCPU de trabalho sustentado. 2 vCPUs dedicados = 4x headroom.

### PgBouncer Built-in (custo zero)

O Azure PostgreSQL Flexible Server oferece PgBouncer integrado (porta 6432, modo `transaction`). Não requer sidecar container.

**Configuração:**
- `pgbouncer.enabled = true`
- `pgbouncer.default_pool_size = 50`
- `pgbouncer.max_client_conn = 150` (mitigação de connection exhaustion — veja abaixo)
- Connection string: `Host=...;Port=6432;Ssl Mode=Require`

**Benefícios:** 150 conexões de app (5 réplicas × 30 pool Npgsql) → ~10–50 conexões reais ao PostgreSQL. Protege contra connection storms durante auto-scaling.

### Observações pós-deploy: custo do Log Analytics

Após 3 dias em produção, **Log Analytics consumiu 93.6% do custo total** por ingestão de 23.3 GB sem daily cap:

| Tabela | Volume | Causa |
|--------|--------|-------|
| `ContainerAppConsoleLogs_CL` | 10.2 GB (44%) | Logs verbose de EF Core, ASP.NET, HttpClient |
| `AppDependencies` | 6.5 GB (28%) | Traces completos com SQL statements por query |
| `AppTraces` | 6.2 GB (26%) | Traces de cada request × 5 serviços |

**Mitigações aplicadas:**
1. Daily cap 1 GB no Log Analytics workspace (`workspaceCapping.dailyQuotaGb: 1`)
2. Trace sampling 10% via `TraceIdRatioBasedSampler`
3. Log filters em produção — EF Core, ASP.NET Hosting/Routing, HttpClient filtrados para `Warning`
4. `SetDbStatementForText = false` no EF Core instrumentation

Redução estimada: 23.3 GB/3 dias → ~5.7 GB/3 dias (-75%).

## Trade-offs

| Aspecto | B1ms (Burstable) | D2ds_v4 (General Purpose) |
|---------|-----------------|---------------------------|
| Custo | ~$13/mês | ~$125/mês |
| CPU | Credit-based (throttle após 60s) | Dedicado, sustentado |
| Conexões | 50 max | 196 max |
| IOPS | 450 baseline | 3.450 |
| Adequação | Dev/staging | Produção com carga sustentada |

| Aspecto | PgBouncer Built-in | PgBouncer Sidecar |
|---------|-------------------|-------------------|
| Custo | $0 (incluso) | CPU/mem adicional |
| Manutenção | Zero — managed pelo Azure | Atualização de imagem |
| Modo suportado | Transaction only | Transaction, session, statement |

## Consequências

- **NFR compliance:** PostgreSQL com headroom de 4x elimina o bottleneck que causava todos os timeouts.
- **Connection pooling:** PgBouncer protege contra connection storms durante auto-scaling dos Container Apps.
- **Zero alteração em código:** connection string flui via Bicep output → app configuration.
- **Custo incremental:** +$112/mês (+87% do custo total de infra).
- **Limites de migração:**
  - > 200 req/s sustentados: Standard_D4ds_v4 (4 vCPUs, ~$250/mês)
  - > 1.000 req/s leituras: adicionar read replica
  - PgBouncer mode `transaction` com prepared statements: configurar `pgbouncer.ignore_startup_parameters` ou desabilitar prepared statements no Npgsql
