# ADR-005: Concorrência — Append-Only Writes + Optimistic Concurrency + Particionamento de Consumer

| Campo | Valor |
|---|---|
| **Status** | Aceito |
| **Data** | Março 2026 |
| **Contexto** | Duas transactions simultâneas no mesmo dia podem causar race conditions no consolidation. Com 2 ou mais consumers processando mensagens do mesmo `(MerchantId, ReferenceDate)` em paralelo, a taxa de `DbUpdateConcurrencyException` pode atingir 30-50% em pico — todos os eventos convergem para a mesma linha `daily_summary`. Mesmo com `UseMessageRetry`, esse nível de contenção eleva a latência e desperdiça ciclos de banco com retentativas desnecessárias (retry storm). |
| **Decisão** | Três camadas de proteção complementares: **(1)** Transactions são **append-only** (INSERT-only, nunca UPDATE). **(2)** **Particionamento do consumer por `(MerchantId, ReferenceDate)`** via `UsePartitioner` do MassTransit — mensagens do mesmo merchant e dia são processadas **sequencialmente** no mesmo slot de partição, eliminando concorrência no caso mais comum. **(3)** **Optimistic Concurrency** via `xmin` (PostgreSQL) como safety net para casos residuais (ex: failover entre instâncias). |

## Detalhes

### Análise de Race Conditions

**O problema sem particionamento (com 2 consumers):**
```
Consumer A: lê balance=100  → calcula 100+50=150  → grava 150  ← COMMIT OK
Consumer B: lê balance=100  → calcula 100-30=70   → grava 70
            xmin esperado: 0, atual: 1            → DbUpdateConcurrencyException!
            → UseMessageRetry reprocessa → lê balance=150 → calcula 150-30=120 → OK
            Taxa de exceção em pico: 30-50% quando muitas transactions convergem pro mesmo dia
```

**A solução (3 camadas de proteção):**

1. **Transactions são INSERT-only**: Dois INSERTs concorrentes **nunca conflitam**. Não existe "atualizar balance" no write-side.
2. **Particionamento por `(MerchantId, ReferenceDate)`**: Mensagens do mesmo merchant e dia caem no mesmo slot de partição, processadas sequencialmente — sem concorrência, sem `DbUpdateConcurrencyException`. Merchants e dias diferentes processam em paralelo (escalabilidade mantida).
3. **Optimistic Concurrency como safety net**: `xmin` (PostgreSQL system column) protege contra casos residuais (failover, mensagens fora de ordem entre partições distintas).

### Configuração do Particionamento (UsePartitioner)

```
Sem particionamento (PROBLEMA):
  Consumer 1 ──► TransactionCreated(ComA, 2026-03-03) ──► UPDATE consolidation(ComA, 03-03)
  Consumer 2 ──► TransactionCreated(ComA, 2026-03-03) ──► UPDATE consolidation(ComA, 03-03) ← CONFLITO!

Com UsePartitioner(8, key=(MerchantId:ReferenceDate)):
  Slot 3 ──► TransactionCreated(ComA, 2026-03-03) ──► processado SEQUENCIALMENTE
  Slot 3 ──► TransactionCreated(ComA, 2026-03-03) ──► aguarda o anterior → sem conflito
  Slot 7 ──► TransactionCreated(ComB, 2026-03-04) ──► processado em PARALELO com Slot 3 ← OK
```

A `TransactionCreatedConsumerDefinition` configura a pipeline de middlewares na ordem LIFO (último registrado = mais externo):

1. **`UsePartitioner`** (mais externo) — roteia para slot por `(MerchantId:ReferenceDate)` com 8 slots antes de qualquer retry. Elimina `DbUpdateConcurrencyException` de 30-50% para ~0%.
2. **`UseDelayedRedelivery`** — envia para DLQ com delay exponencial (5m, 15m, 60m) após esgotar retries imediatos. Requer plugin `rabbitmq_delayed_message_exchange` ([ADR-007](007-dlq.md)).
3. **`UseMessageRetry`** — retry exponencial com jitter de 50ms (5 tentativas, 100ms a 30s) para casos residuais.
4. **`UseEntityFrameworkOutbox`** (mais interno, último registrado) — gerencia a TX que envolve o consumer. DEVE ser o último para que InboxState check, lógica de negócio e COMMIT ocorram numa única transação.

### Consumer com Lógica de Negócio

O `TransactionCreatedConsumer` foca exclusivamente na lógica de negócio — a idempotência (InboxState) é gerenciada automaticamente pelo MassTransit via `UseEntityFrameworkOutbox`. O fluxo do consumer: (1) busca ou cria o `DailySummary` do dia para o merchant, (2) aplica a transaction via `DailySummary.ApplyTransaction()` (lógica de domínio), (3) salva — o MassTransit faz commit da TX que inclui InboxState + OutboxMessage, e (4) invalida o cache de output para o merchant+data via `EvictByTagAsync`. O `UsePartitioner` garante que nenhum outro consumer processa o mesmo `(MerchantId, ReferenceDate)` simultaneamente.

## Trade-offs

| Configuração | Throughput | Taxa DbUpdateConcurrencyException |
|---|---|---|
| 1 consumer, sem particionamento | ~33 msg/s | ~0% (sem concorrência) |
| 2 consumers, sem particionamento | ~50 msg/s (com retries) | **30-50% em pico** |
| **2 consumers + UsePartitioner(8) (padrão)** | **~66 msg/s** | **~0%** |
| 3 consumers + UsePartitioner(16) | ~99 msg/s | ~0% |

## Consequências

- **Impacto do particionamento**: O `UsePartitioner` reduz `DbUpdateConcurrencyException` de **30-50% para ~0%**. O throughput efetivo por instância aumenta porque menos ciclos são desperdiçados em retentativas.
- **Dimensionamento dos slots**: 8 slots suporta até 8 combinações `(MerchantId, ReferenceDate)` em paralelo por instância de consumer. Para sistemas com muitos merchants e períodos distintos, aumentar para 16 ou 32 slots via configuração de ambiente.
