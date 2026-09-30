---
id: ADR-0001
title: Monólito modular com vertical slices por módulo, em um único host
status: accepted
origin: decision
date: 2026-09-30
decision_makers: [thomas]
consulted: [claude]
informed: []
supersedes:
superseded_by:
enforced_by: tests/Juker.ArchitectureTests (ArchUnitNET) — regras ARQ-01..ARQ-05
---

# ADR-0001 — Monólito modular com vertical slices por módulo, em um único host

## Contexto e Problema
O Juker é construído por um fundador sozinho, com carga pequena por bar (dezenas a centenas de conexões) e necessidade de validar rápido num piloto. Ao mesmo tempo, a fonte de mídia (YouTube) pode mudar ou ser restringida, então as partes do domínio precisam de fronteiras claras para trocar peças sem reescrever o sistema. Como organizar o backend e quantos deployables ter?

## Direcionadores da Decisão
- Um desenvolvedor: custo operacional mínimo, um deploy só.
- Fronteiras explícitas entre domínios (Venues, Sessions, Catalog, Queue, Playback) para isolar a dependência do YouTube.
- Preferência explícita do fundador: monólito modular no estilo Milan Jovanović, com vertical slices por módulo.
- Extração futura de módulos deve ser barata.

## Opções Consideradas
- Monólito modular com vertical slices por módulo, um host (API + SignalR + Blazor na mesma origem)
- Monólito modular com Clean Architecture por módulo (Domain/Application/Infrastructure/Presentation, como o Evently)
- Microsserviços por domínio

## Resultado da Decisão
**Opção escolhida:** "Monólito modular com vertical slices por módulo, um host", porque atende ao pedido do fundador, reduz projetos e cerimônia por feature, mantém a fronteira entre módulos garantida por teste e entrega tudo em um único container.

Estrutura:
- `src/Modules/<Módulo>/Juker.Modules.<Módulo>` (interno: `Domain/`, `Features/<Slice>/`, `Infrastructure/`, `<Módulo>Module.cs`)
- `src/Modules/<Módulo>/Juker.Modules.<Módulo>.Contracts` (público: eventos de integração, DTOs e a interface síncrona do módulo)
- `src/BuildingBlocks/Juker.SharedKernel` e `src/BuildingBlocks/Juker.Infrastructure`
- `src/Juker.Host` compõe os módulos; `src/Web/*` é o frontend.

Regras (G3):
- **ARQ-01** — um módulo só referencia o `.Contracts` de outro módulo, nunca o projeto interno.
- **ARQ-02** — `.Contracts` não referencia nenhum projeto interno de módulo.
- **ARQ-03** — cada módulo tem o próprio `DbContext` e schema; nenhum `DbContext` de um módulo é usado por outro.
- **ARQ-04** — `Domain/` de um módulo não depende de `Infrastructure/`, EF Core nem ASP.NET Core.
- **ARQ-05** — `src/Web/*` não referencia projetos internos de módulos (só `.Contracts`).

### Consequências
- **Boa**, porque um único container, uma única origem (sem CORS) e um pipeline simples.
- **Boa**, porque cada feature fica num slice só (endpoint + handler + validação + UI), fácil de dar a um agente.
- **Ruim**, porque o slice pode duplicar pequenas lógicas que a Clean Architecture centralizaria; mitigado pelo `Domain/` rico por módulo.
- **Ruim**, porque escalar um módulo isolado exige extraí-lo; aceitável para a carga prevista.

### Confirmação (G3)
Suíte `tests/Juker.ArchitectureTests` com ArchUnitNET (ADR-0011), uma regra por item ARQ-01..ARQ-05, rodando no `arch_test` e no CI (SPEC-0003, SPEC-0005).

## Prós e Contras das Opções
| Critério (peso) | Monólito modular + slices | Monólito modular + Clean por módulo | Microsserviços |
|---|---|---|---|
| Custo operacional para 1 dev (5) | 5 | 5 | 1 |
| Velocidade de entrega por feature (4) | 5 | 3 | 2 |
| Isolamento de domínio (4) | 4 | 5 | 5 |
| Aderência ao pedido do fundador (3) | 5 | 4 | 1 |
| Facilidade de extrair depois (2) | 4 | 4 | 5 |
| **Total ponderado** | **84** | **78** | **48** |

## Mais Informações
- Guia da skill SDD (references/greenfield.md) — padrão monólito modular com fronteiras testadas.
- Milan Jovanović, "Architecture Tests in .NET With ArchUnitNET" — https://milanjovanovic.tech/blog/5-architecture-tests-you-should-add-to-your-dotnet-projects (consultado em 2026-09-30).
- Specs relacionadas: SPEC-0001, SPEC-0003, SPEC-0010.
