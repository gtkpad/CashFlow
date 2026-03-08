# ADR-016: API Versioning com Asp.Versioning e Política de Deprecation

| Campo | Valor |
|---|---|
| **Status** | Aceito |
| **Data** | Março 2026 |
| **Contexto** | As APIs do CashFlow usam versionamento por URL path (`/api/v1/...`) via Carter `MapGroup`. Não há tooling formal para reportar versões suportadas, deprecar versões antigas, ou comunicar ciclo de vida de API aos consumidores. |
| **Decisão** | Adotar `Asp.Versioning.Http` para gestão formal de versões com reporting automático via headers HTTP. Manter URL path como mecanismo primário de seleção de versão. |

## Detalhes

### Configuração

```csharp
builder.Services.AddApiVersioning(options =>
{
    options.DefaultApiVersion = new ApiVersion(1, 0);
    options.AssumeDefaultVersionWhenUnspecified = true;
    options.ReportApiVersions = true; // Headers automáticos
});
```

### Headers de Resposta

Com `ReportApiVersions = true`, todas as respostas incluem:
- `api-supported-versions: 1.0` — versões atualmente ativas
- `api-deprecated-versions: (vazio)` — versões marcadas para remoção

### Integração com Carter

Carter registra rotas via `ICarterModule.AddRoutes()` dentro de um `MapGroup("api/v1")`. O `Asp.Versioning` é registrado globalmente e opera via middleware, sem necessidade de alterar os módulos Carter.

### Alternativa Avaliada — `NewVersionedApi()`

A API `NewVersionedApi()` do `Asp.Versioning` cria grupos de rotas nativamente versionados. Porém, a integração com `MapCarter()` não é garantida (Carter descobre e registra módulos automaticamente). A abordagem híbrida (versioning global + URL path manual) é mais segura e mantém a simplicidade do Carter.

## Política de Deprecation

### Ciclo de Vida

```
Active (atual) → Deprecated (6 meses de aviso) → Sunset (removido)
```

| Estado | Duração | Headers | Comportamento |
|---|---|---|---|
| **Active** | Indefinido | `api-supported-versions: 1.0` | Totalmente suportado |
| **Deprecated** | 6 meses mínimo | `api-deprecated-versions: 1.0`, `Sunset: <data>` | Funcional com aviso |
| **Sunset** | — | N/A | Removido, retorna 410 Gone |

### Comunicação

1. **Headers HTTP** — `api-deprecated-versions` e `Sunset` (RFC 8594)
2. **Changelog** — Entrada no `CHANGELOG.md` com data de sunset
3. **OpenAPI** — Schema com `deprecated: true` no endpoint

### Procedimento de Deprecation

1. Adicionar nova versão (ex: `v2`) com mudanças breaking
2. Marcar versão antiga como deprecated no `Asp.Versioning`
3. Adicionar header `Sunset` com data (mínimo 6 meses no futuro)
4. Documentar no changelog e OpenAPI
5. Após data de sunset, remover a versão e retornar 410 Gone

## Trade-offs

| Dimensão | Asp.Versioning ✅ | Versionamento Manual ❌ |
|---|---|---|
| **Headers automáticos** | Sim (`api-supported-versions`) | Requer middleware customizado |
| **Deprecation workflow** | Nativo | Manual |
| **Integração Carter** | Parcial (global, sem `NewVersionedApi`) | Completa (MapGroup simples) |
| **Dependência adicional** | 1 pacote NuGet | Nenhuma |

## Consequências

- Consumidores de API podem inspecionar headers para detectar versões suportadas e deprecadas.
- Novas versões da API seguem o ciclo Active → Deprecated → Sunset.
- O URL path permanece como mecanismo primário (`/api/v1/`, `/api/v2/`).
- A integração com Carter é limitada ao versionamento global — se necessário, futura migração para `NewVersionedApi()` com módulos Carter adaptados.
