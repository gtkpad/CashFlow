# ADR-009: Testes E2E com .NET Aspire Testing

| Campo | Valor |
|---|---|
| **Status** | Aceito |
| **Data** | Março 2026 |
| **Contexto** | Os testes de integração com `WebApplicationFactory` testam uma única API em isolamento — não exercitam a comunicação assíncrona real entre serviços via RabbitMQ. O `CashFlow.AppHost` já orquestra exatamente essa topologia para desenvolvimento local. O .NET Aspire oferece a API `DistributedApplicationTestingBuilder` que reutiliza o AppHost como motor de testes E2E. |
| **Decisão** | Criar `CashFlow.E2ETests` usando `DistributedApplicationTestingBuilder` referenciando `CashFlow.AppHost`. Responsável por validar fluxos de negócio completos que cruzam fronteiras de serviço, especialmente os que dependem de consistência eventual via MassTransit + RabbitMQ. |

## Detalhes

### Diferença entre Integration Tests e E2E Tests

| Dimensão | `CashFlow.IntegrationTests` | `CashFlow.E2ETests` |
|---|---|---|
| **Motor** | `WebApplicationFactory<Program>` | `DistributedApplicationTestingBuilder` |
| **Escopo** | Uma única API | Todos os 4 processos + PostgreSQL + RabbitMQ |
| **Comunicação entre serviços** | Mockada ou não testada | Real (HTTP via Gateway + AMQP via RabbitMQ) |
| **Consistência eventual** | Não testada diretamente | Testada com polling + timeout |
| **Velocidade** | Rápida (~5–30s/suite) | Lenta (~60–120s/suite — boot da topologia completa) |
| **Uso recomendado** | CI em cada commit/PR | CI em merge para `main` ou staging gate |

### Quando usar cada abordagem

- **Use `IntegrationTests`** para: validar regras de negócio de uma API, testar handlers, verificar retorno 401 sem token, testar consumer MassTransit em isolamento.
- **Use `E2ETests`** para: validar que `POST /api/v1/transactions` → RabbitMQ → Consumer → `GET /api/v1/consolidation/{date}` produz o resultado esperado; validar que o Gateway roteia e injeta `X-User-Id` corretamente; smoke tests antes de deploy em staging.

### Fixture Compartilhada

O boot do AppHost leva 30–90s. A `CashFlowAppFixture` compartilha uma única instância do `DistributedApplication` por suite via `IAsyncLifetime` do xUnit:

| Timeout | Valor | Justificativa |
|---|---|---|
| `StartupTimeout` | 5 minutos | Cold start de todos os containers no CI |
| `DefaultTimeout` | 30 segundos | Timeout por operação HTTP |
| `EventualConsistencyTimeout` | 15 segundos | Margem 71x sobre latência máxima esperada de ~140ms |

### Cálculo do EventualConsistencyTimeout

```
QueryDelay do Bus Outbox         = 100ms
Processamento do consumer        =  30ms
Latência de rede Docker/Aspire   =  10ms
────────────────────────────────────────
Latência máxima esperada         = 140ms

Margem de segurança (15s / 140ms) = ~107x
```

A margem é deliberadamente conservadora para absorver: cold start de containers no CI, variações do scheduler Docker em ambientes com recursos limitados, polling do teste a cada 500ms.

### Fluxos cobertos pelos E2E Tests

1. **Fluxo completo:** `POST /api/v1/transactions` → INSERT + OutboxMessage → Delivery Service → RabbitMQ → Consumer → `GET /api/v1/consolidation/{date}` retorna balance atualizado.
2. **Isolamento de falhas (NFR-1):** Consolidation indisponível não impede criação de transactions.
3. **Isolamento de dados (NFR de segurança):** `MerchantA_ShouldNotSeeDataFromMerchantB`.

## Trade-offs

| Aspecto | Valor |
|---|---|
| **Cobertura** | Máxima — testa o fluxo real com todos os componentes |
| **Velocidade** | Lenta — boot do AppHost 60–120s; recomendado em gates de merge |
| **Confiabilidade** | Alta — testa comportamento exato de produção |
| **Paralelismo** | Não recomendado — `MaxParallelThreads=1` (dados compartilhados no DB) |
| **Diagnóstico** | Excelente — Aspire Dashboard disponível durante execução local |

## Consequências

- Os testes E2E dependem do `CashFlow.AppHost`: mudanças nos nomes de recursos Aspire devem ser refletidas na fixture.
- `EventualConsistencyTimeout = 15s` pressupõe `QueryDelay = 100ms` no Bus Outbox ([ADR-002](002-messaging.md)). Se o valor padrão de 1.000ms for mantido, aumentar para 20s.
- Ao adicionar novos serviços ao AppHost, adicionar `WaitForResourceHealthyAsync` correspondente na fixture.
- Testes E2E rodam via GitHub Actions apenas em merge para `main` ou com label `run-e2e` na PR. Timeout do job: 15 minutos.
