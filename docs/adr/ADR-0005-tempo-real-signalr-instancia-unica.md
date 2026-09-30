---
id: ADR-0005
title: Tempo real com SignalR, grupos por bar e instância única no MVP (sem Valkey)
status: accepted
origin: decision
date: 2026-09-30
decision_makers: [thomas]
consulted: [claude]
informed: []
supersedes:
superseded_by:
enforced_by: SPEC-0012:CT-01 (contrato dos eventos do hub) e revisão G4
---

# ADR-0005 — Tempo real com SignalR, grupos por bar e instância única no MVP (sem Valkey)

## Contexto e Problema
Fila, votos, "tocando agora" e o estado da TV precisam aparecer ao vivo em todos os celulares, na TV e no painel. Qual tecnologia usar e como escalar?

## Direcionadores da Decisão
- Latência percebida baixa (voto aparece em menos de 1 s).
- Reconexão automática em rede instável.
- Menos peças para operar no piloto.

## Opções Consideradas
- SignalR (WebSockets com fallback), grupos por `VenueId`, instância única; backplane Valkey só ao escalar
- SignalR com backplane Valkey desde o início
- Server-Sent Events + polling de comandos

## Resultado da Decisão
**Opção escolhida:** "SignalR, instância única, sem backplane", porque atende à carga de um piloto e de dezenas de bares numa única instância, e o backplane pode ser adicionado sem mudar o contrato. **Gatilho para adicionar Valkey (BSD-3):** mais de uma réplica do host, ou mais de ~2.000 conexões simultâneas. O servidor só **notifica** mudanças. O cliente relê o estado por HTTP ao reconectar (o servidor é a fonte da verdade).

### Consequências
- **Boa**, porque um recurso a menos (Valkey) para operar e fazer backup.
- **Ruim**, porque sem alta disponibilidade no MVP: um reinício do host derruba as conexões por alguns segundos. Mitigado pela reconexão automática e pela releitura do estado.

### Confirmação (G3)
Contrato de eventos versionado em `Juker.Modules.*.Contracts` e verificado por SPEC-0012:CT-01.

## Prós e Contras das Opções
| Critério (peso) | SignalR único | SignalR + Valkey | SSE + polling |
|---|---|---|---|
| Operação simples (5) | 5 | 3 | 5 |
| Escala horizontal (2) | 2 | 5 | 3 |
| Reconexão/robustez (4) | 5 | 5 | 3 |
| Integração com Blazor (4) | 5 | 5 | 2 |
| **Total ponderado** | **69** | **66** | **53** |

## Mais Informações
- Valkey (BSD-3) vs Redis 8 (RSAL/SSPL/AGPL) — https://dev.to/synsun/redis-vs-valkey-in-2026-what-the-license-fork-actually-changed-1kni (2026-09-30): Valkey é a escolha padrão para um backplane futuro.
