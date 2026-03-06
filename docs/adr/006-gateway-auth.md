# ADR-006: API Gateway e Autenticação Centralizada (YARP + ASP.NET Core Identity)

| Campo | Valor |
|---|---|
| **Status** | Aceito |
| **Data** | Março 2026 |
| **Contexto** | Temos múltiplas APIs independentes e ocultas na rede interna. Propagar tokens para os serviços backend exigiria que todas as APIs precisassem saber como validar tokens, consultar chaves ativas do STS ou possuir um middleware robusto de JWT/Cookie. Adicionalmente, se a emissão do token e a validação dele rodassem em `Transactions.API`, uma queda nesse serviço isolado impediria o próprio acesso de leitura ao `Consolidation.API`. |
| **Decisão** | **1.** Extrair a emissão de Token para o `CashFlow.Identity.API`. **2.** Adotar o padrão **Gateway Auth Offloading** no **YARP**. O Gateway será o **único** serviço exposto à rede, encarregado de validar o Token Bearer. Em caso de sucesso, o YARP decodifica os Claims, os extrai e repassa a identidade aos serviços downstream através de Headers HTTP limpos (ex: `X-User-Id`). **3.** Os serviços backend (Transactions e Consolidation) não validam tokens, simplesmente abrem portas HTTP(s) em binding local (rede Docker restrita) e confiam completamente na Identidade propagada pelo Gateway. |
| **Justificativa** | Fazer o *Auth Offloading* centraliza a segurança e remove o peso e risco estrutural dos microsserviços do Core Domain. **O token emitido pelo Identity.API é um Bearer JWT** (configurado via `AddBearerToken()` no ASP.NET Core Identity), permitindo que o YARP valide e decodifique os claims diretamente — sem chamadas síncronas adicionais ao Identity.API por requisição. Os microsserviços downstream são agnósticos ao mecanismo de autenticação e apenas observam os headers de contexto (`X-User-Id`) injetados pelo Gateway. |

## Trade-offs

| Auth Offloading no YARP ✅ | Validação Distribuída ❌ | Identity Server Externo (Keycloak) |
|---|---|---|
| **Segurança**: Focada no Perímetro | Fragmentada em cada serviço | Padrão OIDC Seguro |
| **Backend**: Extremamente Leve (Headers) | Exige middleware Auth em todos | Extremamente Robusto mas pesado |
| **Isolamento de Falhas**: Máximo Desacoplamento | Key sharing required | Single Point of Failure no IDP |
| **Network Trust**: Assume "Zero Trust" no Perimeter e "Full Trust" no Backend | Zero Trust Everywhere | Zero Trust Everywhere |

## Consequências

- O Gateway é o único ponto que conhece o mecanismo de autenticação (JWT).
- Serviços backend confiam nos headers injetados pelo YARP — não validam tokens.
- O `GatewaySecretMiddleware` nos backends valida header `X-Gateway-Secret` para impedir acesso direto contornando o Gateway.
- Cada serviço possui banco dedicado (Identity, Transactions, Consolidation), eliminando acoplamento de schema entre contextos.
