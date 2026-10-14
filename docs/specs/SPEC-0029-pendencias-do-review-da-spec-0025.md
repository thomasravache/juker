---
id: SPEC-0029
title: Pendências do review da SPEC-0025
tier: lite
type: fix
user_facing: false
status: proposed
created: 2026-10-13
parent: SPEC-0001
depends_on: [SPEC-0025]
consumes_contract: []
touches: [tests/Juker.TestKit.E2E/**, tests/Juker.E2ETests/**, src/Juker.Host/Testing/**, tests/Juker.IntegrationTests/Testing/**, docs/testing.md]
adrs: [ADR-0012, ADR-0013]
external: []
size: S
approved_by:
approved_at:
---

# SPEC-0029 — Pendências do review da SPEC-0025

## 1. Problema
O review independente da SPEC-0025 (G4 aprovado em 3476a61) deixou quatro pendências no harness de E2E:
1. O `AspireAppFixture` é compartilhado por contagem de referências e usado como `IClassFixture`: se duas classes de teste não se sobrepõem no tempo, o AppHost sobe de novo, contra o "uma vez por execução" da SPEC-0025 (só fica mais lento conforme a suíte cresce).
2. O `ControllableTimeProvider` só desloca o "agora": `CreateTimer`/`GetTimestamp` delegam ao relógio do sistema, então avançar o relógio por `/_test/clock/advance` não dispara timers nem serviços periódicos do servidor. Specs futuras que dependem de timer no servidor (ex.: TV offline, rotação do PIN) não conseguiriam conduzi-los pelos E2E.
3. `POST /_test/clock/advance` com `seconds` acima de `TimeSpan.MaxValue.TotalSeconds` responde 500 (`OverflowException`) em vez de 400.
4. O UT-01 da SPEC-0025 só segue chamadas dentro do `Juker.E2ETests`; uma espera fixa (`Thread.Sleep`/`Task.Delay`) num helper do `Juker.TestKit.E2E`, que não é projeto de teste (sem o RS0030 de testes), passaria despercebida.

## 2. Causa Raiz
Achados minor do G4 da SPEC-0025, aceitos como pendência: o desenho do fixture (classe em vez de assembly), o provedor de tempo controlável que só implementa `GetUtcNow`, a validação do corpo que não limita o máximo e o escopo do scanner limitado ao assembly de testes.

## 3. Mudança Proposta
- `Juker.E2ETests` registra o `AspireAppFixture` uma vez por assembly (`[assembly: AssemblyFixture(typeof(AspireAppFixture))]`, xUnit v3); `docs/testing.md` orienta os próximos projetos de E2E a fazer o mesmo.
- `ControllableTimeProvider` passa a controlar também timers e `GetTimestamp`: timers criados pelo `TimeProvider` do host disparam quando o relógio é avançado pelo teste (no padrão do `FakeTimeProvider`).
- `POST /_test/clock/advance` rejeita `seconds` maior que o máximo representável com `ValidationProblem` (400).
- O scanner do UT-01 da SPEC-0025 segue também as chamadas para o `Juker.TestKit.E2E`.
- **Não muda:** a API pública do `Juker.TestKit.E2E` (snapshot do CT-01 da SPEC-0025), o registro só no ambiente `E2E` e o `InternalsVisibleTo` do diagnóstico (achado do review aceito como está: documentado e com falha rápida).

**Padrão seguido:** `tests/Juker.TestKit.E2E/**` e `src/Juker.Host/Testing/**` (SPEC-0025); `FakeTimeProvider` (Microsoft.Extensions.TimeProvider.Testing) como referência de timers controláveis; `tests/Juker.IntegrationTests/Testing/**`.

**Rollback:** revert dos commits da spec.

**Outras ocorrências:** Nenhuma.

## 4. Plano de Testes (TDD)
- Caracterização: N/A — área coberta pelos testes da SPEC-0025, que continuam valendo.
- **UT-01** — Dado um helper de exemplo no `Juker.TestKit.E2E` que chama `Task.Delay` e um método de teste de exemplo que o usa, quando o scanner analisa o `Juker.E2ETests`, então o método de teste é listado; hoje não é.
- **IT-01** — Com o host em `E2E`, `POST /_test/clock/advance` com `seconds` acima de `TimeSpan.MaxValue.TotalSeconds` responde 400 com `ValidationProblem`; hoje responde 500.
- **IT-02** — Com o host em `E2E`, um timer criado pelo `TimeProvider` do host com vencimento em 30 s não dispara antes do avanço e dispara depois de `POST /_test/clock/advance` com 30 s; hoje não dispara.
- **IT-03** — Duas classes de teste do `Juker.E2ETests` que usam o `AspireAppFixture` recebem a mesma instância numa execução (o AppHost sobe uma vez); hoje cada classe recebe a sua.

## 5. Questões em Aberto
Nenhuma.

## 6. Aprovação (H1)
Registrada no frontmatter (`approved_by`, `approved_at`) somente depois que o humano responder "Aprovado".

## 7. Checklist de Implementação
<!-- Preenchido na fase PLAN, após a aprovação. -->

## 8. Registro de Gates
<!-- Status: PENDING | PASS | FAIL | N/A. PASS e N/A exigem evidência. -->
| Gate | Status | Evidência | Data |
|---|---|---|---|
| G0 Spec | PASS | validate: 0 erro(s) — 6ae32bc (árvore suja) | 2026-10-13 |
| G1 Red | PENDING | | |
| G2 Green | PENDING | | |
| G3 Arquitetura | PENDING | | |
| G4 Review | PENDING | | |
| G5 Integração & CI | PENDING | | |
| H2 Integração aprovada | PENDING | | |
| G6 Deploy | PENDING | | |
| G7 Pronto & Docs | PENDING | | |

## 9. Registro de Impedimentos
<!-- Toda parada é registrada pelo Architect com `spec_graph.py impede` e fechada com `resolve` — não edite à mão. Tipos: spec (spec errada/incompleta → resolve com Emenda) | decisão (só o humano decide → resposta ou ADR) | trabalho (falta algo que exige código → SPEC-NNNN nova) | externo (acesso, ambiente, terceiro → ação tomada) | falha (3 FAILs seguidos no mesmo gate → diagnóstico e decisão). Com impedimento aberto a spec aparece como parada no INDEX e não pode ser fechada. -->
| ID | Aberto em | Fase/Gate | Tipo | Descrição | Tentativas | Responsável | Resolução | Fechado em |
|---|---|---|---|---|---|---|---|---|

## 10. Relatório de Entrega
<!-- Preenchido no CLOSE (G7). Para status implemented o validate exige tudo preenchido, todo teste do plano com PASS + evidência e a Definição de Pronto marcada. -->

### O que foi entregue

### Como foi feito

### Prova de Correção
<!-- type fix: teste de regressão falhou antes (commit red + saída) e passa depois (commit green + execução). Outros tipos: "N/A". -->

### Verificação
| Teste | Comportamento | Resultado | Evidência |
|---|---|---|---|

### Definição de Pronto
- [ ] Teste de regressão falhou antes e passa depois da correção
- [ ] Suíte completa, arquitetura e CI verdes (G2, G3, G5)
- [ ] Review independente sem achados blocker/major (G4)
- [ ] Padrão existente mantido
- [ ] Disponível no ambiente-alvo via pipeline (G6)
- [ ] Documentação/CHANGELOG atualizados quando aplicável (G7)
- [ ] Outras ocorrências registradas como novas specs (ou nenhuma)

### Deploy

### Pendências

## 11. Emendas
<!-- Mudança em spec aprovada: uma linha por emenda, aprovada pelo humano. -->
| Versão | Data | Mudança | Motivo | Specs impactadas | Aprovado por |
|---|---|---|---|---|---|
