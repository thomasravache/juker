---
id: ADR-0010
title: Observabilidade com OpenTelemetry (ServiceDefaults do Aspire) exportando OTLP para um backend gerenciado com plano gratuito
status: accepted
origin: decision
date: 2026-09-30
decision_makers: [thomas]
consulted: [claude]
informed: []
supersedes:
superseded_by:
enforced_by: SPEC-0008:IT-01..IT-03
---

# ADR-0010 — Observabilidade com OpenTelemetry exportando OTLP para backend gerenciado

## Contexto e Problema
Precisamos saber, durante uma sexta-feira no bar, se a TV está online, se a fila avança, se a busca externa está estourando a cota e se há erros, sem operar uma stack de observabilidade no VPS.

## Direcionadores da Decisão
- Custo zero ou quase zero no piloto.
- Padrão aberto (OTLP) para trocar de fornecedor sem mudar código.
- Alertas no celular do fundador.

## Opções Consideradas
- OpenTelemetry via `ServiceDefaults` → OTLP para Grafana Cloud (plano gratuito): logs, métricas, traces e alertas; uptime check externo
- Aspire Dashboard standalone no VPS (sem persistência nem alertas)
- Seq/Sentry autogerenciados

## Resultado da Decisão
**Opção escolhida:** "OTel → OTLP gerenciado", porque o `ServiceDefaults` já instrumenta ASP.NET Core, HttpClient e EF Core, e o backend gerenciado dá persistência e alertas sem operação. Em desenvolvimento, o painel do Aspire (local). Logs estruturados em JSON, **sem dados pessoais** (comanda sim, IP não). Métricas de negócio: `juker.tv.online`, `juker.queue.length`, `juker.requests.added`, `juker.youtube.quota.used`, `juker.playback.errors`. O endpoint e a chave OTLP vêm do ambiente. O fornecedor concreto é confirmado na SPEC-0008, verificando o plano gratuito vigente.

### Consequências
- **Boa**, porque os alertas "TV offline há mais de 2 min em horário de funcionamento" e "cota > 80%" saem sem infraestrutura própria.
- **Ruim**, porque os limites do plano gratuito exigem amostragem de traces (10% em produção, 100% dos erros).

### Confirmação (G3)
SPEC-0008:IT-01 (health checks), IT-02 (métricas de negócio emitidas), IT-03 (logs sem IP nem token).

## Prós e Contras das Opções
| Critério (peso) | OTel → gerenciado | Dashboard standalone | Autogerenciado |
|---|---|---|---|
| Custo (5) | 4 | 5 | 3 |
| Alertas e persistência (5) | 5 | 1 | 4 |
| Operação (4) | 5 | 4 | 2 |
| **Total ponderado** | **65** | **46** | **43** |

## Mais Informações
- Os limites vigentes do plano gratuito do Grafana Cloud **não foram verificados** nesta decisão. A verificação faz parte da SPEC-0008 antes da configuração (registrar a data e a fonte). Alternativas equivalentes via OTLP: Honeycomb, New Relic ou Axiom (planos gratuitos).
