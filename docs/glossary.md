# Glossário — CashFlow

Termos específicos do domínio e padrões arquiteturais que precisam de esclarecimento contextual.

> Convenção: código em inglês, comunicação e documentação em português. Este glossário mapeia os termos do código para o domínio de negócio.

## Domínio

| Termo (Código) | Termo (Negócio) | Definição |
|---|---|---|
| `Transaction` | Lançamento | Registro financeiro individual de uma movimentação. Imutável após criação (append-only). |
| `DailySummary` | Consolidado Diário | Resumo materializado do fluxo de caixa de um dia. Atualizado via eventos assíncronos. |
| `Balance` | Saldo | `TotalCredits - TotalDebits`. Propriedade derivada, não persistida no banco. |
| `TransactionType.Credit` | Crédito | Entrada de caixa (venda, recebimento). |
| `TransactionType.Debit` | Débito | Saída de caixa (despesa, pagamento). |
| `ReferenceDate` | Data de Referência | Data do lançamento para fins de consolidação. Pode diferir da data de criação. |
| `MerchantId` | ID do Comerciante | Identificador único do tenant (comerciante). Derivado do claim `sub` do JWT. `readonly record struct` que rejeita `Guid.Empty`. |
| `Money` | Valor Monetário | Quantia com moeda ISO 4217. Imutável, sempre não-negativa. Validação de moeda compatível nas operações `+` e `-`. |

## Padrões Arquiteturais

| Termo | Definição no Contexto do Projeto |
|---|---|
| **Command-Side** | Transactions API — recebe e persiste lançamentos (escrita). Usa Repository + Unit of Work para proteger invariantes. |
| **Query-Side** | Consolidation API e GetTransaction — materializa e serve dados para leitura. Acessa `DbContext` diretamente via LINQ sem Repository. |
| **Bus Outbox** | MassTransit: ao chamar `Publish()`, grava o evento na tabela `OutboxMessage` na mesma transação ACID do `SaveChangesAsync()`. Um `IHostedService` embutido (Delivery Service) faz polling e entrega ao broker. |
| **Consumer Inbox** | MassTransit: antes de invocar o consumer, verifica a tabela `InboxState` pelo `MessageId`. Se já existe, descarta silenciosamente. Garante exactly-once processing. |
| **Tenant Isolation** | Isolamento de dados por comerciante via `MerchantId` em todas as queries. Implementado em 3 camadas: Gateway (injeta header), GatewaySecretMiddleware (valida origem), MerchantIdFilter (converte header em Value Object). |
| **Eventual Consistency** | O saldo consolidado (`DailySummary`) é atualizado de forma assíncrona após o lançamento ser criado. Gap típico: < 200ms com `QueryDelay = 100ms`. |
| **Optimistic Concurrency (xmin)** | O PostgreSQL mantém uma coluna de sistema `xmin` que muda a cada UPDATE. O EF Core usa essa coluna como row version para detectar conflitos sem lock pessimista. |
| **Particionamento (UsePartitioner)** | Mensagens do mesmo `(MerchantId, ReferenceDate)` são roteadas para o mesmo slot de partição e processadas sequencialmente, eliminando conflitos de concorrência entre consumers paralelos. |
| **Auth Offloading** | O YARP valida o JWT Bearer Token e injeta `X-User-Id` nos requests. Serviços internos confiam no header sem validar o token diretamente. |
| **Vertical Slice** | Unidade de código que contém tudo necessário para um caso de uso: endpoint, command/query, handler, validator e response. Código organizado por feature, não por camada técnica. |

## Eventos de Integração

| Termo (Código) | Descrição |
|---|---|
| `ITransactionCreated` | Contrato de integração publicado no RabbitMQ após um lançamento ser persistido. Consumido pela Consolidation API. |
| `Fault<ITransactionCreated>` | Evento gerado automaticamente pelo MassTransit quando um consumer falha após esgotar todos os retries. Consumido pelo `TransactionFaultConsumer` para registro de métricas e alertas. |

## Serviços

| Serviço | Responsabilidade |
|---|---|
| **Gateway** | Ponto único de entrada. Proxy reverso (YARP), validação JWT, rate limiting (3600 req/min por IP), injeção de `X-User-Id`. |
| **Identity API** | Registro de comerciantes e autenticação (ASP.NET Core Identity). Emite Bearer Tokens JWT. |
| **Transactions API** | Criação e consulta de lançamentos (Command-Side). MassTransit Bus Outbox para publicação garantida de eventos. |
| **Consolidation API** | Consumo de eventos e consulta de saldos consolidados (Query-Side). Output Cache com TTL variável por data. |
