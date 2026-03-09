# ADR-010: Handlers via Injeção Direta de Dependência (sem MediatR)

| Campo | Valor |
|---|---|
| **Status** | Aceito |
| **Data** | Março 2026 |
| **Contexto** | O sistema tem 3 handlers no total: `CreateTransactionHandler`, `GetTransactionHandler` (Transactions API) e `GetDailyBalanceHandler` (Consolidation API). O MediatR v12+ (dezembro 2024) exige `LicenseKey` para uso comercial. Alternativas open-source existem: Mediator (martinothamar, MIT, source generators) e Wolverine (MIT, mediator + messaging). O MassTransit já cobre comunicação cross-service. |
| **Decisão** | Handlers são classes POCO registradas via `AddScoped<T>()` no container DI nativo. Cada handler expõe um método `HandleAsync` invocado diretamente pelo endpoint Minimal API via injeção de construtor. Sem interface `IRequestHandler<TRequest, TResponse>` nem pipeline de behaviors. |

## Detalhes

### O mediator pattern resolve 3 problemas

1. **Desacoplamento** entre caller e handler — com 3 handlers e endpoints Minimal API, o acoplamento direto é trivial.
2. **Pipeline de cross-cutting concerns** — já resolvidos por mecanismos dedicados: FluentValidation (entrada), GlobalExceptionHandler (erros), DomainEventInterceptor via EF Core (domain events), MassTransit Bus Outbox (delivery).
3. **Dispatch dinâmico** — não necessário; cada endpoint sabe exatamente qual handler invocar.

### Análise quantitativa

| Métrica | DI Direto | MediatR v12+ | Mediator (source-gen) |
|---|---|---|---|
| Overhead/request | 0ns | ~91ns + 240B alloc | ~59ns + 64B alloc |
| Licença | N/A | Comercial (LicenseKey) | MIT |
| Dependências adicionais | 0 | 1 pacote + reflexão | 1 pacote + source gen |
| Registro por handler | 1 linha `AddScoped<T>()` | Assembly scan | Assembly scan |
| Pipeline behaviors | Manual (decorators) | Nativo | Nativo |

## Trade-offs

| Dimensão | DI Direto | MediatR | Mediator (source-gen) |
|---|---|---|---|
| Simplicidade | Máxima — zero indireção | Moderada | Boa |
| Discoverability | Explícita — Go-to-Definition funciona | Indireta (resolve em runtime) | Semi-explícita |
| Escalabilidade | Degrada com 10+ handlers (registro manual) | Excelente | Excelente |
| Cross-cutting pipeline | Manual | Nativo (IPipelineBehavior) | Nativo |
| Custo de licenciamento | Zero | Comercial | Zero (MIT) |

## Consequências

- Cada novo handler exige registro explícito no `Program.cs` do respectivo serviço.
- A navegação de código é maximamente explícita: qualquer IDE resolve `handler.HandleAsync()` diretamente para a implementação.
- **Threshold de migração:** reavaliar quando qualquer condição for atingida:
  - 8–10+ handlers por serviço (registro manual torna-se friction significativa)
  - Necessidade de pipeline behaviors transversais (logging de performance, caching por query, audit trail)
  - Novo bounded context que nasça com 10+ handlers
- **Alternativa recomendada para migração:** [Mediator (martinothamar)](https://github.com/martinothamar/Mediator) — MIT, source generators, API compatível com MediatR. Migração: adicionar `IRequest<T>` nos commands/queries, implementar `IRequestHandler<T>`, substituir `AddScoped<T>()` por `AddMediator()`, trocar injeção direta por `ISender.Send()`.
