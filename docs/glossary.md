# Glossário — Linguagem Ubíqua do CashFlow

> **Convenção:** Código em inglês, documentação e comunicação em português. Este glossário mapeia os termos do código para o domínio de negócio.

## Entidades e Agregados

| Termo (Código) | Termo (Negócio) | Descrição |
|---|---|---|
| `Transaction` | Lançamento | Registro financeiro individual (crédito ou débito) de um comerciante |
| `DailySummary` | Consolidado Diário | Resumo financeiro de um dia, com totais de créditos, débitos e saldo |
| `MerchantId` | ID do Comerciante | Identificador único do comerciante (tenant). Derivado do `sub` claim do JWT |
| `TransactionId` | ID do Lançamento | Identificador único de um lançamento financeiro |
| `DailySummaryId` | ID do Consolidado | Identificador único de um consolidado diário |

## Value Objects

| Termo (Código) | Termo (Negócio) | Descrição |
|---|---|---|
| `Money` | Valor Monetário | Quantia com moeda (ex: R$ 150,00 BRL). Imutável, sempre não-negativo |
| `TransactionType` | Tipo de Lançamento | `Credit` (Crédito = entrada) ou `Debit` (Débito = saída) |
| `ReferenceDate` | Data de Referência | Data do lançamento para fins de consolidação (não necessariamente data de criação) |

## Eventos

| Termo (Código) | Termo (Negócio) | Descrição |
|---|---|---|
| `TransactionCreated` | Lançamento Criado | Evento de domínio emitido quando um lançamento é persistido |
| `ITransactionCreated` | Contrato de Integração | Evento publicado no RabbitMQ para consumo pelo Consolidation API |

## Conceitos Arquiteturais

| Termo | Descrição |
|---|---|
| **Command-Side** | Transactions API — recebe e persiste lançamentos (escrita) |
| **Query-Side** | Consolidation API — materializa e serve saldos consolidados (leitura) |
| **Outbox** | Padrão que garante publicação atômica de eventos junto com a transação de banco |
| **Inbox** | Padrão que garante processamento idempotente de eventos recebidos |
| **Tenant Isolation** | Isolamento de dados por comerciante via `MerchantId` em todas as queries |
| **Balance** | Saldo — diferença entre total de créditos e total de débitos |
| **Eventual Consistency** | Consistência eventual entre o lançamento criado e o saldo consolidado (tipicamente < 1s) |

## Serviços

| Serviço | Responsabilidade |
|---|---|
| **Gateway** | Proxy reverso (YARP), autenticação JWT, rate limiting, injeção de `X-User-Id` |
| **Identity API** | Registro de comerciantes e autenticação (ASP.NET Core Identity) |
| **Transactions API** | Criação e consulta de lançamentos (Command-Side) |
| **Consolidation API** | Consumo de eventos e consulta de saldos consolidados (Query-Side) |
