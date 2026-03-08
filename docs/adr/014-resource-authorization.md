# ADR-014: Autorização Baseada em Recurso via MerchantId (Tenant Isolation)

| Campo | Valor |
|---|---|
| **Status** | Aceito |
| **Data** | Março 2026 |
| **Contexto** | O sistema é multi-tenant onde cada usuário autenticado (merchant) deve acessar apenas seus próprios dados. A questão é como garantir isolamento de recursos sem RBAC formal. |
| **Decisão** | Isolamento de recursos implementado por filtragem obrigatória de `MerchantId` em todas as camadas, desde o Gateway até os handlers de consulta e consumidores de eventos. |

## Detalhes

### Fluxo de Isolamento (3 camadas)

```
Request → [1] Gateway AuthMiddleware → [2] GatewaySecretMiddleware → [3] MerchantIdFilter → Handler
```

**Camada 1 — Gateway (`AuthMiddleware`):**
- Valida JWT Bearer token
- Extrai claim `sub` (user ID) do token
- Injeta header `X-User-Id` no request repassado aos serviços internos
- Remove qualquer `X-User-Id` enviado pelo cliente (previne spoofing)

**Camada 2 — Serviço (`GatewaySecretMiddleware`):**
- Valida header `X-Gateway-Secret` para garantir que o request veio do Gateway
- Rejeita requests diretos aos serviços internos (defense-in-depth)

**Camada 3 — Endpoint (`MerchantIdFilter`):**
- Extrai `X-User-Id` e converte em `MerchantId` (value object)
- Disponibiliza via `HttpContext.GetMerchantId()` para os handlers
- Retorna 401 se o header estiver ausente ou inválido

### Enforcement nos Handlers

Todos os handlers filtram dados por `MerchantId`:

```csharp
// Query — GetTransactionHandler
db.Transactions.FirstOrDefaultAsync(t => t.Id == txId && t.MerchantId == mId, ct);

// Query — GetDailyBalanceHandler (compiled query)
ctx.DailySummaries.FirstOrDefault(d => d.MerchantId == merchantId && d.Date == date);

// Command — CreateTransactionHandler
Transaction.Create(new MerchantId(merchantId), ...);
```

### Cache Isolation

O output cache do Consolidation API varia por `X-User-Id`:
```csharp
context.CacheVaryByRules.HeaderNames = new StringValues("X-User-Id");
context.Tags.Add($"balance-{merchantId}-{dateStr}");
```

Merchants diferentes recebem respostas cacheadas independentes.

## Trade-offs

| Dimensão | Resource-Based (atual) ✅ | RBAC Formal ❌ |
|---|---|---|
| **Complexidade** | Mínima — filtragem por campo | Alta — roles, policies, claims |
| **Segurança de dados** | Completa para single-role | Requer para multi-role |
| **Performance** | Zero overhead | Middleware de autorização adicional |
| **Auditoria** | Via logs existentes | Requer audit trail dedicado |

## Consequências

- Qualquer novo handler ou query **deve** incluir filtro por `MerchantId` — esta é uma invariante arquitetural.
- O teste E2E `MerchantA_ShouldNotSeeDataFromMerchantB` valida o isolamento end-to-end.
- Quando houver necessidade de papéis distintos (Admin, ReadOnly), evoluir para ASP.NET Core Authorization Policies com claims-based roles.
- A ausência de RBAC é uma limitação conhecida documentada em `architecture.md`, não um gap de segurança para o cenário atual (single-role merchant).
