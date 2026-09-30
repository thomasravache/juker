---
id: ADR-0006
title: Comunicação entre módulos por interfaces síncronas nos Contracts e eventos de integração in-process com outbox
status: accepted
origin: decision
date: 2026-09-30
decision_makers: [thomas]
consulted: [claude]
informed: []
supersedes:
superseded_by:
enforced_by: ARQ-01/ARQ-02 (ADR-0001) e IT do outbox em SPEC-0004
---

# ADR-0006 — Comunicação entre módulos por interfaces síncronas nos Contracts e eventos de integração in-process com outbox

## Contexto e Problema
Os módulos precisam colaborar. A Queue pergunta à Sessions se a comanda está bloqueada; o bloqueio na Sessions precisa remover pedidos e anular votos na Queue; a Playback pergunta à Queue qual é a próxima música. Como fazer isso sem acoplar os módulos e sem perder eventos?

## Direcionadores da Decisão
- Fronteiras de módulo garantidas (ADR-0001).
- Não perder efeitos colaterais (ex.: "comanda bloqueada → pedidos removidos") se o processo cair no meio.
- Sem broker externo no MVP.

## Opções Consideradas
- Interfaces síncronas em `.Contracts` para consultas + eventos de integração publicados via **outbox** (tabela por módulo) e despachados in-process por um `BackgroundService`
- Apenas chamadas síncronas entre módulos
- Broker externo (RabbitMQ) desde o início

## Resultado da Decisão
**Opção escolhida:** "síncrono para consultas + eventos com outbox in-process", porque as consultas precisam de resposta imediata e os efeitos colaterais precisam de garantia de entrega (at-least-once) com handlers idempotentes. Cada módulo grava o evento na **sua** tabela `outbox_messages` na mesma transação da mudança. Um dispatcher lê, entrega aos consumidores registrados (via `.Contracts`) e marca como processado. Cada consumidor registra `inbox` por `(MessageId, Consumer)` para idempotência.

### Consequências
- **Boa**, porque os efeitos não se perdem e trocar por um broker depois só muda o transporte.
- **Ruim**, porque há latência do dispatcher (alvo: menos de 1 s) e código de infraestrutura próprio; mitigado por IT dedicado.

### Confirmação (G3)
ARQ-01/ARQ-02. IT do outbox (SPEC-0004): evento gravado na transação, entregue uma vez por consumidor mesmo com reprocessamento.

## Prós e Contras das Opções
| Critério (peso) | Síncrono + outbox | Só síncrono | Broker externo |
|---|---|---|---|
| Garantia de efeitos colaterais (5) | 5 | 2 | 5 |
| Operação simples (4) | 4 | 5 | 2 |
| Acoplamento baixo (4) | 5 | 2 | 5 |
| **Total ponderado** | **61** | **38** | **53** |

## Mais Informações
- Padrão Transactional Outbox — https://microservices.io/patterns/data/transactional-outbox.html (consultado em 2026-09-30).
