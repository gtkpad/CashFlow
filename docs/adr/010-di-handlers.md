# ADR-010: Handlers via Injeção Direta de Dependência (sem MediatR/Mediator)

| Campo | Valor |
|---|---|
| **Status** | Aceito |
| **Data** | Março 2026 |
| **Contexto** | O sistema implementa CQRS com handlers dedicados para commands e queries: `CreateTransactionHandler` (Transactions API) e `GetDailyBalanceHandler` (Consolidation API), além do `GetTransactionHandler` (Transactions API). São **3 handlers** no total, distribuídos em 2 serviços. O MediatR v12+ (dezembro 2024) introduziu a exigência de `LicenseKey` para uso comercial em produção. Alternativas open-source existem: **Mediator** (martinothamar) usa source generators para zero-reflection dispatch, e **Wolverine** oferece mediator + messaging integrado. Ambas são MIT-licensed. O **MassTransit** já atua como mediator para integration events, cobrindo o cenário de comunicação cross-service. |
| **Decisão** | Handlers são classes POCO registradas via `AddScoped<T>()` no container DI nativo do ASP.NET Core. Cada handler expõe um método `HandleAsync` invocado diretamente pelo endpoint Minimal API. Não há interface `IRequestHandler<TRequest, TResponse>` nem pipeline de behaviors. O dispatch é feito por injeção de construtor — o endpoint recebe o handler como parâmetro e chama `HandleAsync`. |

## Detalhes

### Análise Quantitativa

| Métrica | DI Direto (atual) | MediatR v12+ | Mediator (source-gen) | Wolverine |
|---|---|---|---|---|
| **Overhead/request** | 0ns | ~91ns + 240B alloc | ~59ns + 64B alloc | ~70ns (estimado) |
| **Licença** | N/A | Comercial (LicenseKey) | MIT | MIT |
| **Dependências adicionais** | 0 | 1 pacote + reflexão | 1 pacote + source gen | 1 pacote + hosting |
| **Linhas de configuração** | 1 `AddScoped<T>()` por handler | `AddMediatR(cfg => ...)` + assembly scan | `AddMediator()` + source gen | `UseWolverine()` + conventions |
| **Pipeline behaviors** | Não disponível | Sim (IPipelineBehavior) | Sim (IPipelineBehavior) | Sim (middleware chain) |
| **Curva de aprendizado** | Nenhuma | Moderada | Baixa | Alta (conceitos de messaging) |

> **Fonte dos benchmarks**: [github.com/martinothamar/Mediator — Benchmarks](https://github.com/martinothamar/Mediator#benchmarks)

### Justificativa

O mediator pattern resolve três problemas: (1) desacoplamento entre caller e handler, (2) pipeline de cross-cutting concerns (logging, validation, caching), e (3) dispatch dinâmico baseado em tipo de request. No estado atual do CashFlow:

1. **Desacoplamento**: Com 3 handlers e endpoints Minimal API, o acoplamento direto é trivial de manter.
2. **Cross-cutting concerns**: Já estão resolvidos por mecanismos especializados — **FluentValidation** (validação de entrada), **GlobalExceptionHandler** (tratamento uniforme de erros), **DomainEventInterceptor** via EF Core (publicação de domain events), e **MassTransit Bus Outbox** (garantia de delivery). Um `IPipelineBehavior` adicionaria uma camada de indireção sobre o que já funciona.
3. **Dispatch dinâmico**: Não é necessário — cada endpoint sabe exatamente qual handler invocar.

## Trade-offs

| Dimensão | DI Direto ✅ | MediatR ❌ | Mediator (source-gen) ⚠️ |
|---|---|---|---|
| **Simplicidade** | Máxima — zero indireção | Moderada — abstração adicional | Boa — source gen reduz magia |
| **Testabilidade** | Direta — mock do handler | Direta — mock do handler | Direta — mock do handler |
| **Discoverability** | Explícita — Go-to-Definition funciona | Indireta — `Send()` resolve em runtime | Semi-explícita — source gen gera código navegável |
| **Escalabilidade de handlers** | Degrada com 10+ handlers (registro manual) | Excelente — assembly scanning | Excelente — source gen scanning |
| **Cross-cutting pipeline** | Manual (duplicação ou decorators) | Nativo (IPipelineBehavior) | Nativo (IPipelineBehavior) |
| **Custo de licenciamento** | Zero | Comercial (MediatR v12+) | Zero (MIT) |

## Consequências

### Threshold de Migração

Reavaliar esta decisão quando **qualquer** das condições abaixo for atingida:

- **8-10+ handlers por serviço** — o registro manual via `AddScoped<T>()` e a ausência de assembly scanning tornam-se friction significativa
- **Necessidade de pipeline behaviors** — ex: logging centralizado de performance, caching por query, ou audit trail transversal
- **Novo serviço com alta densidade de handlers** — um novo bounded context que nasça com 10+ handlers justifica adoção desde o início

**Alternativa recomendada para migração**: [Mediator (martinothamar)](https://github.com/martinothamar/Mediator) — MIT, source generators (zero reflection), API compatível com MediatR. A migração envolveria: (1) adicionar interface `IRequest<TResponse>` nos commands/queries, (2) implementar `IRequestHandler<TRequest, TResponse>` nos handlers existentes, (3) substituir `AddScoped<T>()` por `AddMediator()`, (4) nos endpoints, trocar a injeção direta por `ISender.Send()`.

### Impactos

- Cada novo handler exige registro explícito no `Program.cs` do respectivo serviço.
- Cross-cutting concerns continuam sendo resolvidos por mecanismos dedicados.
- A navegação de código é maximamente explícita: qualquer IDE resolve `handler.HandleAsync()` diretamente para a implementação.
- O pattern atual é idêntico ao que o .NET Minimal APIs recomenda.
