# ADR-001: Topologia — Serviços Independentes com API Gateway

| Campo | Valor |
|---|---|
| **Status** | Aceito |
| **Data** | Março 2026 |
| **Contexto** | O NFR mais crítico exige que o serviço de transactions continue disponível mesmo que o consolidation caia. Um Monolito Modular (processo único) não pode atender: um crash não-tratado em qualquer módulo derruba o processo inteiro. Com múltiplos serviços independentes, é necessário um ponto único de entrada para centralizar autenticação, roteamento e observabilidade. |
| **Decisão** | Adotar **quatro processos independentes**: Transactions API, Consolidation API, Identity API e Gateway (YARP). Repositório compartilhado (monorepo), comunicação assíncrona via RabbitMQ, organização interna via Vertical Slice Architecture + DDD. |

## Justificativa

Isolamento de falhas real exige isolamento de processo. Um crash na Consolidation API não afeta a Transactions API. O YARP centraliza a validação do Bearer Token e propaga a identidade via headers (`X-User-Id`), removendo essa responsabilidade dos serviços de domínio — ver [ADR-006](006-gateway-auth.md). O Identity API encapsula autenticação em processo próprio: queda da Transactions API não impede login. Repositório compartilhado mantém simplicidade operacional.

## Trade-offs

| Aspecto | Monolito Modular | Serviços Independentes c/ Gateway | Microsserviços Full |
|---|---|---|---|
| Isolamento de falhas | Não garante | Garantido (processos separados) | Garantido |
| Complexidade operacional | Baixa | Moderada (4 processos) | Alta (K8s, service mesh) |
| Repositório | 1 repo | 1 repo | N repos, N pipelines |
| API Gateway | Desnecessário | YARP (processo .NET, sem infra extra) | Gateway dedicado (Kong, Envoy) |
| Auth | Em cada módulo | Centralizado no Gateway | Distribuído ou IDP externo |
| Custo | Mínimo | Baixo | Alto |

## Consequências

- O YARP é um SPOF mais crítico do que os serviços que protege. Mitigado por múltiplas réplicas no Azure Container Apps — ver [ADR-008](008-gateway-ha.md).
- O YARP deve ser stateless (sem sticky sessions) para escala horizontal direta.
- **Threshold de migração:** quando a equipe superar 5 devs ou a carga ultrapassar 500 req/s, avaliar extração para microsserviços com repos separados.
