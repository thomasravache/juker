---
id: SPEC-0026
title: Pendências do review da SPEC-0002
tier: lite
type: fix
user_facing: false
status: in-progress
created: 2026-09-30
parent: SPEC-0001
depends_on: [SPEC-0002]
consumes_contract: []
touches: [BannedSymbols.txt, src/Juker.Host/Diagnostics/**, src/Juker.Host/ModuleCatalogExtensions.cs, src/Juker.Host/Program.cs, tests/Juker.IntegrationTests/Tooling/**, tests/fixtures/SyncOverAsyncViolation/**, AGENTS.md]
adrs: [ADR-0014]
external: []
size: S
approved_by: thomas
approved_at: 2026-09-30
---

# SPEC-0026 — Pendências do review da SPEC-0002

## 1. Problema
O review independente da SPEC-0002 (G4 aprovado em d5ba5a4) deixou três lacunas de comportamento e uma de documentação:
1. O ADR-0014 (regra 3) proíbe `GetAwaiter().GetResult()` fora do `Program`/CLI, mas o `BannedSymbols.txt` não inclui esses símbolos: hoje o build aceita bloqueio síncrono via awaiter.
2. `/_diag/modules` lista os nomes do `ModuleCatalog.All`, e não os módulos que o host de fato compôs: se a chamada `AddJukerModules` sumisse do `Program.cs`, os testes continuariam passando.
3. O "404 fora de Development/Testing" de `/_diag/modules` está implementado, mas não é testado.
4. O hook de pre-commit roda `dotnet format --verify-no-changes` sobre a árvore de trabalho, e não sobre o que está no stage, e um `spec_graph.py vendor --hooks` futuro sobrescreveria a extensão. Isso não está documentado.

## 2. Causa Raiz
1. O contrato v2 da SPEC-0002 omitiu os awaiters da lista banida (lacuna do Architect entre a spec e o ADR-0014).
2. `DiagnosticsEndpoints` lê o catálogo estático em vez de um registro do que foi composto em `AddJukerModules`/`MapJukerModules`.
3. O plano de testes da SPEC-0002 não tinha uma linha para o 404.
4. A extensão do hook foi feita sem nota em `AGENTS.md`.

## 3. Mudança Proposta
- `BannedSymbols.txt`: banir `M:System.Runtime.CompilerServices.TaskAwaiter.GetResult`, `M:System.Runtime.CompilerServices.TaskAwaiter`1.GetResult`, `M:System.Runtime.CompilerServices.ValueTaskAwaiter.GetResult` e `M:System.Runtime.CompilerServices.ValueTaskAwaiter`1.GetResult`, com exceção apenas no `src/Juker.Host/Program.cs` e em `src/Juker.Host/Cli/**`, por supressão no nível do arquivo e com justificativa (ADR-0014).
- Host: um registro de módulos compostos (`ComposedModules`), preenchido por `AddJukerModules`/`MapJukerModules`, que passa a ser a fonte de `/_diag/modules`. O contrato da resposta **não muda**: o mesmo array JSON com os cinco nomes quando o host compõe os cinco.
- `AGENTS.md`: nota de que o pre-commit verifica a árvore de trabalho (não o stage), é opcional e precisa ser reaplicado se o `vendor --hooks` for executado de novo.
- **Não muda:** contrato público, rotas, comandos e o fixture `QualityViolations` da SPEC-0002.

**Padrão seguido:** `src/Juker.Host/ModuleCatalog.cs`, `src/Juker.Host/Diagnostics/DiagnosticsEndpoints.cs` e `tests/Juker.IntegrationTests/Tooling/*` (SPEC-0002).

**Rollback:** revert dos commits da spec.

**Outras ocorrências:** Nenhuma. O ajuste de texto no ADR-0014 sobre supressões de aviso do MSBuild (`NoWarn` no `.csproj` com comentário, quando o `.editorconfig` não controla o aviso, caso do ASPIRE010) é feito pelo Architect junto com esta spec.

## 4. Plano de Testes (TDD)
- Caracterização: N/A — área já coberta pelos testes unitários e de integração da SPEC-0002, que continuam valendo.
- **IT-01** — Dado o fixture `tests/fixtures/SyncOverAsyncViolation` (fora da solução, importa o `Directory.Build.props`) com `Task.CompletedTask.GetAwaiter().GetResult()`, quando `dotnet build` roda, então termina com código ≠ 0 e a saída contém `RS0030`; hoje o build passa (regressão).
- **IT-02** — Dado um host de teste (WebApplicationFactory em `Development`) em que a composição de módulos é substituída para registrar só `Venues`, quando `GET /_diag/modules` é chamado, então a resposta é `["Venues"]`; hoje retorna os cinco nomes do catálogo (regressão).
- **IT-03** — (guarda: passa antes da mudança) Dado o host em `Production`, quando `GET /_diag/modules` é chamado, então a resposta é 404.

## 5. Questões em Aberto
Nenhuma.

## 6. Aprovação (H1)
Registrada no frontmatter (`approved_by`, `approved_at`) somente depois que o humano responder "Aprovado".

## 7. Checklist de Implementação

**Fase 1: Regressão**
- [ ] Red: escrever IT-01, IT-02 e IT-03 com a tag `SPEC-0026:<ID>` e confirmar que IT-01 e IT-02 falham pelo motivo certo (IT-03 é guarda: passa antes da mudança)
- [ ] Green: banir os awaiters, criar o registro de módulos compostos e a nota no AGENTS.md
- [ ] Refactor mantendo tudo verde
- [ ] Validar: build + suíte completa + lint (G2)

**Fase final: Integração, entrega e documentação**
- [ ] Review independente (G4)
- [ ] Integração (G5) e aprovação (H2)
- [ ] Deploy via pipeline (G6) — N/A até a SPEC-0006
- [ ] Relatório de Entrega e CHANGELOG (G7)

## 8. Registro de Gates
| Gate | Status | Evidência | Data |
|---|---|---|---|
| G0 Spec | PASS | `spec_graph.py validate SPEC-0026` → G0 [SPEC-0026]: PASS — 0 erro(s), 0 aviso(s); checklist de julgamento do G0 revisado pelo Architect | 2026-09-30 |
| G1 Red | PENDING | | |
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
| escopo | 2026-10-01 | Redação da linha de caracterização sem IDs de teste de outra spec | O `verify` lia os IDs da SPEC-0002 como testes planejados da SPEC-0026 (apontado pelo Test-writer) | Nenhuma | thomas — autorização prévia no chat (2026-09-30), a confirmar no overview |
