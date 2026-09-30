---
id: SPEC-0028
title: Pendências do review da SPEC-0026
tier: lite
type: fix
user_facing: false
status: in-progress
created: 2026-10-01
parent: SPEC-0001
depends_on: [SPEC-0026]
consumes_contract: []
touches: [BannedSymbols.txt, src/Juker.Host/Diagnostics/ComposedModules.cs, tests/Juker.IntegrationTests/Tooling/**, tests/fixtures/SyncOverAsyncViolation/**]
adrs: [ADR-0014]
external: []
size: S
approved_by: thomas (autorização prévia de 2026-09-30, a confirmar no overview)
approved_at: 2026-09-30
---

# SPEC-0028 — Pendências do review da SPEC-0026

## 1. Problema
O review independente da SPEC-0026 (G4 aprovado em 182e5de) apontou:
1. `Task.CompletedTask.ConfigureAwait(false).GetAwaiter().GetResult()` ainda compila sem RS0030: os awaiters "configurados" (`ConfiguredTaskAwaitable.ConfiguredTaskAwaiter`, `ConfiguredTaskAwaitable<T>.ConfiguredTaskAwaiter`, `ConfiguredValueTaskAwaitable.ConfiguredValueTaskAwaiter`, `ConfiguredValueTaskAwaitable<T>.ConfiguredValueTaskAwaiter`) não estão na lista banida, contrariando a intenção do ADR-0014 (regra 3).
2. O IT-02 da SPEC-0026 prova que `/_diag/modules` lê o registro de módulos compostos do DI, mas não que `AddJukerModules` o preenche com os módulos que de fato compôs.
3. A documentação XML do `ComposedModules` diz que ele é preenchido por `AddJukerModules`/`MapJukerModules`; só o `AddJukerModules` o preenche.

## 2. Causa Raiz
Lista banida da SPEC-0026 cobria só os awaiters simples; plano de testes da SPEC-0026 não previa o preenchimento; texto da doc desatualizado.

## 3. Mudança Proposta
- `BannedSymbols.txt`: banir o `GetResult` dos quatro awaiters configurados, com o mesmo comentário do ADR-0014.
- Teste que prova que o host real (sem substituir o registro) registra `ComposedModules` com exatamente os módulos que `AddJukerModules` compôs.
- Corrigir a doc XML do `ComposedModules`.
- **Não muda:** contrato do `/_diag/modules` e o resto da lista banida.

**Padrão seguido:** `tests/Juker.IntegrationTests/Tooling/SyncOverAsyncViolationBuildTests.cs` e `DiagnosticsModulesTests.cs` (SPEC-0026).

**Rollback:** revert dos commits da spec.

**Outras ocorrências:** Nenhuma.

## 4. Plano de Testes (TDD)
- Caracterização: N/A — área já coberta pelos testes de integração da SPEC-0026, que continuam valendo.
- **IT-01** — Dado o fixture `SyncOverAsyncViolation` com `Task.CompletedTask.ConfigureAwait(false).GetAwaiter().GetResult()` (e a forma genérica e as de `ValueTask`), quando `dotnet build` roda, então termina com código ≠ 0 e RS0030 para cada forma; hoje compila (regressão).
- **IT-02** — Dado o host de teste sem substituir o registro, quando o `ComposedModules` é resolvido do DI, então contém exatamente os nomes dos módulos adicionados por `AddJukerModules`, na ordem do `ModuleCatalog`.

## 5. Questões em Aberto
Nenhuma.

## 6. Aprovação (H1)
Registrada no frontmatter (`approved_by`, `approved_at`) somente depois que o humano responder "Aprovado".

## 7. Checklist de Implementação

**Fase 1: Regressão**
- [ ] Red: escrever IT-01, IT-02 com a tag `SPEC-0028:<ID>`; confirmar que as regressões falham pelo motivo certo (guardas marcadas passam antes da mudança)
- [ ] Green: implementar a mudança proposta
- [ ] Refactor mantendo tudo verde
- [ ] Validar: build + suíte completa + arquitetura + lint (G2/G3)

**Fase final: Integração, entrega e documentação**
- [ ] Review independente (G4)
- [ ] Integração (G5) e aprovação (H2)
- [ ] Deploy via pipeline (G6) — N/A enquanto o CD estiver adiado
- [ ] Relatório de Entrega e CHANGELOG (G7)

## 8. Registro de Gates
| Gate | Status | Evidência | Data |
|---|---|---|---|
| G0 Spec | PASS | `spec_graph.py validate SPEC-0028` → 0 erros; aguardando H1 do fundador | 2026-10-01 |
| G1 Red | PASS | `verify SPEC-0028 --base sdd/SPEC-0001` no worktree (HEAD bc010b7): red antes do green PASS, commits red só alteram testes PASS, rastreabilidade 2/2 PASS; `dotnet test --solution` → 39 total, 1 falha (IT-01, fixture dos awaiters configurados compila hoje); IT-02 verde como guarda | 2026-10-01 |
| G2 Green | PENDING | | |
| G3 Arquitetura | PENDING | | |
| G4 Review | PENDING | | |
| G5 Integração & CI | PENDING | | |
| H2 Integração aprovada | PENDING | | |
| G6 Deploy | PENDING | | |
| G7 Pronto & Docs | PENDING | | |

## 9. Registro de Impedimentos
| ID | Aberto em | Fase/Gate | Tipo | Descrição | Tentativas | Responsável | Resolução | Fechado em |
|---|---|---|---|---|---|---|---|---|

## 10. Relatório de Entrega

### O que foi entregue

### Como foi feito

### Prova de Correção

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
| Versão | Data | Mudança | Motivo | Specs impactadas | Aprovado por |
|---|---|---|---|---|---|
