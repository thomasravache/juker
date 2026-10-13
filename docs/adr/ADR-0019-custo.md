---
id: ADR-0019
title: Custo — piloto abaixo de €20/mês com duas VPS, Object Storage e planos gratuitos, revisão mensal da fatura
status: proposed
origin: decision
date: 2026-10-13
pillars: [custo]
baseline: custo@2026.09
decision_makers: [thomas]
consulted: [claude]
informed: []
supersedes:
superseded_by:
enforced_by: Estimativa neste ADR; alerta de gasto no console da Hetzner (SPEC-0006); revisão mensal registrada em docs/operations.md
---

# ADR-0019 — Custo — piloto abaixo de €20/mês com duas VPS, Object Storage e planos gratuitos, revisão mensal da fatura

## Contexto e Problema
O Juker é um produto de um fundador, ainda sem receita, que vai para um piloto num bar. Custos fixos altos inviabilizam o piloto; custos variáveis escondidos (cota do YouTube, minutos de CI, ingestão de telemetria) aparecem de surpresa.

## Direcionadores da Decisão
- Orçamento do piloto: até €20/mês (proposta, a confirmar pelo fundador).
- Planos gratuitos sempre que atendem ao piloto.
- Premissas explícitas, para saber quando o custo muda.

## Opções Consideradas
- VPS Hetzner (staging e produção) + Object Storage + planos gratuitos (GitHub, Grafana Cloud, YouTube Data API)
- PaaS gerenciado (Azure Container Apps ou Fly.io) com Postgres gerenciado

## Resultado da Decisão
**Opção escolhida:** VPS Hetzner + planos gratuitos, porque fica abaixo de €20/mês no piloto (ADR-0009) e o mesmo artefato de container permite migrar para um PaaS depois.

**Estimativa mensal (premissas: 1 bar, 3 noites por semana, até 100 clientes por noite):**
| Item | Atual (1 bar) | 2x (2 bares) | 10x (10 bares) |
|---|---|---|---|
| 2 VPS CX22 (staging e produção) + Object Storage | < €15 (SPEC-0006) | < €15 | VPS maior em produção: ~€25–35 |
| Domínio | ~€1 | ~€1 | ~€1 |
| GitHub (repo privado, Actions) | €0 (cota gratuita) | €0 | €0 |
| Grafana Cloud | €0 (plano gratuito) | €0 | plano pago possível |
| YouTube Data API | €0 (cota padrão: ~100 buscas/dia) | cota apertada | pedido de aumento de cota ao Google |

**Regras (verificáveis):**
- Estimativa revista antes de qualquer serviço novo pago, com cenários atual, 2x e 10x.
- Preços verificados na fonte oficial com data; os desta tabela **não foram verificados nesta data** (vêm das SPECs 0005 e 0006) e são confirmados na primeira fatura.
- Estourar o orçamento de €20/mês exige decisão do fundador.
- Alerta de gasto no console da Hetzner em €20 e revisão da fatura real todo mês, registrada em `docs/operations.md`.
- Recursos com rótulos `env=staging|production` e `service=juker`.

### Consequências
- **Boa**, porque o piloto cabe num orçamento pessoal e cada salto de custo tem gatilho conhecido (mais bares, cota do YouTube, telemetria).
- **Ruim**, porque os planos gratuitos limitam retenção de telemetria e buscas no YouTube, e mudar de plano exige nova decisão.

### Confirmação (G3)
Estimativa neste ADR; alerta de gasto na Hetzner (SPEC-0006); revisão mensal em `docs/operations.md`.

## Prós e Contras das Opções
| Critério (peso) | VPS + gratuitos | PaaS gerenciado |
|---|---|---|
| Custo no piloto (5) | 5 | 2 |
| Esforço de operação (3) | 2 | 5 |
| Migração futura (2) | 4 | 4 |

## Mais Informações
Referências neutras: FinOps Foundation. Preços **não verificados** nesta data: Hetzner Cloud (https://www.hetzner.com/cloud), Grafana Cloud (https://grafana.com/pricing), cota da YouTube Data API (https://developers.google.com/youtube/v3/getting-started#quota).
