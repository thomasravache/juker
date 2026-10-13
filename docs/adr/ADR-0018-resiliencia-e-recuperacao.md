---
id: ADR-0018
title: Resiliência e recuperação — backup diário com restauração testada, RPO 24 h, RTO 4 h e degradação das integrações externas
status: accepted
origin: decision
date: 2026-10-13
pillars: [resiliencia]
baseline: resiliencia@2026.09
decision_makers: [thomas]
consulted: [claude]
informed: []
supersedes:
superseded_by:
enforced_by: SPEC-0006:IT-03 (backup e restauração), SPEC-0014:IT-04 (timeout e retry do YouTube), SPEC-0004:IT-02 (outbox exatamente uma vez)
---

# ADR-0018 — Resiliência e recuperação — backup diário com restauração testada, RPO 24 h, RTO 4 h e degradação das integrações externas

## Contexto e Problema
O Juker roda numa VPS única por ambiente (ADR-0009, ADR-0005), com o Postgres no mesmo servidor. Uma falha de disco ou um erro de deploy numa noite de bar interrompe a jukebox; perder dados de configuração do bar seria pior do que perder a fila de uma noite.

## Direcionadores da Decisão
- Custo mínimo no piloto (uma VPS por ambiente).
- A noite do bar precisa continuar mesmo com o YouTube ou a observabilidade fora.
- Dev solo: recuperação por runbook simples.

## Opções Consideradas
- `pg_dump` diário para Object Storage, restauração testada, RPO 24 h / RTO 4 h
- Postgres gerenciado com PITR (RPO de minutos)

## Resultado da Decisão
**Opção escolhida:** `pg_dump` diário para Object Storage, porque cabe no custo do piloto e protege o que importa (configuração do bar e catálogo); a fila de uma noite é efêmera.

**Regras (verificáveis):**
- Backup automático diário (04:30 America/Sao_Paulo) com `pg_dump -Fc` para o Object Storage, retenção de 14 dias; restauração testada no IT-03 da SPEC-0006 e repetida a cada 3 meses (registrada em `docs/operations.md`).
- RPO 24 h e RTO 4 h no piloto (reprovisionar a VPS por cloud-init, deploy da última imagem e restauração do backup).
- Chamadas externas com timeout e retry com backoff: YouTube com timeout de 5 s e 1 retry com jitter em 5xx (SPEC-0014); exportação OTLP não bloqueia requisições.
- Operações repetíveis são idempotentes: consumidores de eventos com inbox (exatamente uma vez por consumidor, SPEC-0004); comandos que podem ser reenviados pelo celular usam chave natural ou de idempotência na spec que os cria.
- Falha de dependência não crítica degrada em vez de derrubar: sem YouTube, a busca local e a fila continuam (SPEC-0014); sem backend de observabilidade, o app segue.

### Consequências
- **Boa**, porque o custo fica no orçamento do piloto e a recuperação é reproduzível por script.
- **Ruim**, porque uma falha da VPS derruba a noite do bar até a recuperação (até 4 h) e pode perder até 24 h de dados; revisitar com o primeiro bar pagante (Postgres gerenciado ou réplica).

### Confirmação (G3)
IT-03 da SPEC-0006 (backup e restauração); IT-04 da SPEC-0014 (timeout e retry); IT-02 da SPEC-0004 (idempotência dos consumidores).

## Prós e Contras das Opções
| Critério (peso) | pg_dump + Object Storage | Postgres gerenciado com PITR |
|---|---|---|
| Custo no piloto (5) | 5 | 2 |
| RPO (3) | 2 | 5 |
| Esforço de operação (3) | 3 | 5 |

## Mais Informações
Referências neutras: Google SRE; Well-Architected (pilar de confiabilidade). PostgreSQL `pg_dump`: https://www.postgresql.org/docs/18/app-pgdump.html (não verificado nesta data).
