# ADR-002: Mensageria — RabbitMQ + MassTransit (Bus Outbox + Consumer Outbox/Inbox)

| Campo | Valor |
|---|---|
| **Status** | Aceito |
| **Data** | Março 2026 |
| **Contexto** | A comunicação entre transactions e consolidation deve ser assíncrona, durável e tolerante a falhas. O dual-write problem (gravar no DB e publicar no broker como operações separadas) pode causar perda de eventos. |
| **Decisão** | **RabbitMQ** como message broker + **MassTransit** como framework de mensageria, usando nativamente o **Bus Outbox** (produtor) e o **Consumer Outbox** (que inclui Inbox para idempotência). |

## Detalhes

### Como o MassTransit resolve Outbox + Inbox nativamente

O MassTransit possui dois componentes no seu **Transactional Outbox** (pacote `MassTransit.EntityFrameworkCore`):

**1. Bus Outbox** (Produtor — no Transactions):
- Quando configurado com `UseBusOutbox()`, o MassTransit **substitui** as implementações de `ISendEndpointProvider` e `IPublishEndpoint` por versões que gravam na tabela `OutboxMessage` do banco — **não no broker**.
- As mensagens são gravadas **na mesma transação** do `DbContext.SaveChangesAsync()`.
- Um **Delivery Service** (`IHostedService` embutido na API) faz polling na tabela `OutboxMessage` e entrega ao broker.
- A tabela `OutboxState` garante **ordenação** e **lock distribuído** para múltiplas instâncias.

**2. Consumer Outbox** (Consumidor — na API do Consolidation, como `IHostedService` embutido):
- É uma **combinação de Inbox + Outbox** no consumer.
- **Inbox** (tabela `InboxState`): rastreia mensagens recebidas por `MessageId` por endpoint. Garante **exactly-once consumer behavior**.
- **Outbox do Consumer**: se o consumer publicar/enviar mensagens durante processamento, elas são armazenadas até o consumer completar com sucesso.

> **Impacto**: Isso elimina a necessidade de implementar Outbox e Inbox manualmente. O MassTransit gerencia as 3 tabelas (`InboxState`, `OutboxMessage`, `OutboxState`) automaticamente via EF Core.

### Outbox Pattern — O Problema que o MassTransit Resolve

```
Sem Outbox (PERIGOSO — dual-write):
1. INSERT Transaction    ← ✅ Sucesso
2. COMMIT
3. Publish("evento")   ← ❌ RabbitMQ fora? Evento PERDIDO.

Com MassTransit Bus Outbox (SEGURO — transação ACID):
1. BEGIN TRANSACTION
2.   INSERT Transaction                    ← Dados de negócio
3.   INSERT OutboxMessage (via MassTransit)← Evento na mesma TX (automático)
4. COMMIT                                 ← ACID: ambos ou nenhum
5. [Delivery Service] Poll + Publish      ← IHostedService embutido na API
```

### Inbox Pattern — Como o MassTransit Garante Idempotência

```
Sem Inbox (PERIGOSO):
1. Consumer recebe "TransactionCreated"
2. UPDATE balance += 100           ← ✅
3. ACK                           ← ❌ Timeout! RabbitMQ reenvia.
4. UPDATE balance += 100 (de novo) ← DUPLICADO! Balance errado.

Com MassTransit Consumer Outbox (InboxState — SEGURO):
1. Consumer recebe "TransactionCreated" (MessageId: abc-123)
2. MassTransit verifica InboxState: SELECT WHERE MessageId = 'abc-123'
   → Não existe → Prossegue
3. BEGIN TRANSACTION (gerenciado pelo MassTransit)
4.   Executa Consumer (ApplyTransaction no Consolidation)
5.   INSERT InboxState (MessageId: abc-123)
6.   INSERT OutboxMessage (se consumer publicar algo)
7. COMMIT
8. ACK ao RabbitMQ
--- Se reenviado (MessageId: abc-123): ---
9. MassTransit verifica InboxState → JÁ EXISTE → SKIP automático
```

### Configuração Concreta — Produtor (Transactions API)

No `Program.cs` da Transactions API, o MassTransit é configurado com `AddEntityFrameworkOutbox<TransactionsDbContext>` usando `UsePostgres()` (lock provider) e `UseBusOutbox()` (habilita o Bus Outbox com Delivery Service embutido). O transport é RabbitMQ configurado com `cfg.Host("rabbitmq://messaging")` e `cfg.ConfigureEndpoints(context)` para auto-discovery de consumers.

### Configuração Concreta — Consumer (Consolidation API)

No `Program.cs` da Consolidation API, o MassTransit registra o `TransactionCreatedConsumer` com sua `ConsumerDefinition`, configura o Consumer Outbox (que inclui Inbox) com EF Core via `AddEntityFrameworkOutbox<ConsolidationDbContext>` (sem `UseBusOutbox()` — consumer não é produtor primário), e usa RabbitMQ como transport. O consumer roda como `IHostedService` embutido na API.

A `TransactionCreatedConsumerDefinition` configura a pipeline canônica completa (documentada no [ADR-005](005-concurrency.md)): `UseCircuitBreaker` → `UseDelayedRedelivery` (5min, 15min, 60min) → `UseMessageRetry` com retry exponencial (5 tentativas, 100ms-30s, jitter 50ms, trata apenas `DbUpdateConcurrencyException`) → `UseEntityFrameworkOutbox` como middleware mais interno (gerencia a TX que envolve o consumer). O `UsePartitioner(8)` é configurado em nível de mensagem no consumer.

### DbContext com Tabelas do MassTransit

Ambos os DbContexts incluem as entidades do MassTransit em `OnModelCreating`: o `TransactionsDbContext` registra `OutboxMessage` e `OutboxState` (produtor — InboxState não é necessário). O `ConsolidationDbContext` registra `InboxState` (idempotência), `OutboxMessage` (Consumer Outbox) e `OutboxState` (estado de entrega).

## Trade-offs

### MassTransit vs Implementação Manual

| Aspecto | MassTransit ✅ | Implementação Manual ❌ |
|---|---|---|
| Tabelas | 3 tabelas auto-gerenciadas via EF Core Migrations | Tabelas manuais + SQL DDL manual |
| Outbox Publisher | `IHostedService` embutido (Delivery Service) | Worker process separado + polling loop manual (~100 linhas) |
| Inbox/Idempotência | `InboxState` automático por endpoint + `MessageId` | Tabela `inbox_messages` + lógica de dedup manual (~50 linhas) |
| Lock distribuído | `OutboxState` com lock nativo (PostgreSQL advisory locks) | Implementação manual de row-level lock |
| Retries | Declarativo (`UseMessageRetry`) + delayed redelivery | `try/catch` + loop manual |
| Processos necessários | **2** (API Transactions + API Consolidation) | **4** (API + Outbox Worker + Worker + API Consolidation) |
| Linhas de código | ~30 linhas de configuração | ~300+ linhas de infraestrutura |

### Comparativo de Mensageria (Broker)

| Critério | RabbitMQ ✅ | Kafka | Azure Service Bus |
|---|---|---|---|
| Throughput p/ 50 msg/s | Trivial (~30k msg/s) | Overkill (~1M msg/s) | Suficiente |
| Complexidade | Baixa (1 container) | Média (1 container em modo KRaft desde 3.3+, mas configuração mais complexa) | Nenhuma (PaaS) |
| Custo | Open-source | Open-source, mais infra | Pago (~$0.05/1k msgs) |
| Dead Letter Queue | Nativo | Requer implementação | Nativo |
| Ecossistema .NET | **MassTransit (maduro, built-in Outbox/Inbox)** | Confluent.Kafka (sem Outbox integrado) | Azure.Messaging |
| Vendor lock-in | Nenhum | Nenhum | Azure-only |
