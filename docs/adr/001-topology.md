# ADR-001: Topologia — Serviços Independentes com API Gateway sobre Monolito Modular

| Campo | Valor |
|---|---|
| **Status** | Aceito |
| **Data** | Março 2026 |
| **Contexto** | O NFR exige que o serviço de transactions continue disponível mesmo que o consolidation caia. Um monolito modular (processo único) não pode atender: um crash não-tratado em qualquer módulo derruba o processo inteiro. Adicionalmente, com múltiplos serviços independentes expostos, é necessário um ponto único de entrada para centralizar autenticação, roteamento e observabilidade — evitando que cada serviço backend precise gerenciar validation de tokens e exposição direta à rede pública. |
| **Decisão** | Adotar **quatro processos independentes**: dois serviços de negócio (Transactions e Consolidation), um serviço de identidade (Identity.API) e um API Gateway (YARP) como ponto único de entrada. Repositório compartilhado, comunicação assíncrona via RabbitMQ e organização interna via **Vertical Slice Architecture + DDD** (Domain Core como núcleo invariante, Feature Slices como orquestradoras). |
| **Justificativa** | O isolamento de falhas real exige isolamento de processo. Um crash na API do consolidation não afeta a API de transactions. O API Gateway (YARP) centraliza a validation do Bearer Token e propaga a identidade via headers (`X-User-Id`), removendo essa responsabilidade dos serviços de domínio — ver [ADR-006](006-gateway-auth.md). O Identity.API encapsula o serviço de autenticação em seu próprio processo, garantindo que uma eventual queda de Transactions.API não impeça novos logins. Cada API embute seus respectivos `IHostedService` do MassTransit (Delivery Service e Consumer), criando simetria arquitetural. O repositório compartilhado mantém a simplicidade operacional. A adoção de **Vertical Slices + DDD** permite criar funcionalidades coesas (Endpoint, Command/Query, Handler e DTOs juntos na feature) enquanto mantém as invariantes de negócio encapsuladas no **Domain Core** (Aggregates, Value Objects, Domain Events) — com interfaces de Repository como **ports** no Domain e **adapters** em `Persistence/`, seguindo o modelo Ports & Adapters. Carter gerencia as Minimal APIs focadas na feature. |

## Trade-offs

| Serviços Independentes c/ Gateway ✅ | Monolito Modular ❌ | Microsserviços Full ❌ |
|---|---|---|
| Isolamento de falhas real | Falha compartilhada | Isolamento máximo |
| 4 artefatos de deploy (Gateway, Identity, Transactions, Consolidation) | 1 artefato | N artefatos + K8s |
| 1 repo, CI/CD unificado | 1 repo | N repos, N pipelines |
| **YARP** como API Gateway leve (processo .NET, sem infra extra) | Desnecessário | API Gateway necessário (Kong, Envoy, etc.) |
| Auth centralizado no Gateway ([ADR-006](006-gateway-auth.md)) | Auth em cada módulo | Auth distribuído ou IDP externo |
| **Custo: Baixo** | **Custo: Mínimo** | **Custo: Alto** |

## Consequências

- **Threshold de migração**: Quando a equipe superar 5 devs ou a carga ultrapassar 500 req/s, avaliar extração para microsserviços com repos separados e substituição do YARP por um API Gateway dedicado (Kong, Envoy ou Azure API Management).

> **Risco identificado — YARP como SPOF (R1 — ALTO)**: O YARP, como ponto único de entrada, é um *Single Point of Failure* (SPOF) mais crítico do que os serviços de domínio que ele protege. Uma queda do processo do Gateway torna todo o sistema inacessível externamente, mesmo que Transactions e Consolidation estejam saudáveis. **Mitigação documentada no [ADR-008](008-gateway-ha.md)**: o YARP roda como **Azure Container App com múltiplas réplicas e ingress externo**. O ACA gerencia automaticamente o balanceamento de tráfego, health checks e reinicializações, eliminando o SPOF — sem necessidade de Nginx, HAProxy ou gestão manual de Kubernetes. O YARP é **stateless por design** — não mantém estado de sessão próprio —, permitindo escala horizontal direta sem sincronização de estado entre instâncias.
