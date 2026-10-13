---
id: SPEC-0026
title: Pendências do review da SPEC-0002
tier: lite
type: fix
user_facing: false
status: implemented
created: 2026-09-30
parent: SPEC-0001
depends_on: [SPEC-0002]
consumes_contract: []
touches: [BannedSymbols.txt, Directory.Build.props, src/Juker.Host/Diagnostics/**, src/Juker.Host/ModuleCatalogExtensions.cs, src/Juker.Host/Program.cs, tests/Juker.IntegrationTests/Tooling/**, tests/fixtures/SyncOverAsyncViolation/**, AGENTS.md]
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
- `Directory.Build.props`: em projetos de teste (`IsTestProject=true`), definir `XUNIT_GENERATED_DISABLE_WARNINGS` para que o ponto de entrada gerado pelo xUnit v3 (código de terceiro que usa `GetAwaiter().GetResult()`) não dispare o RS0030, com comentário citando o ADR-0014.
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
- [x] Red: escrever IT-01, IT-02 e IT-03 com a tag `SPEC-0026:<ID>` e confirmar que IT-01 e IT-02 falham pelo motivo certo (IT-03 é guarda: passa antes da mudança)
- [x] Green: banir os awaiters, criar o registro de módulos compostos e a nota no AGENTS.md
- [x] Refactor mantendo tudo verde
- [x] Validar: build + suíte completa + lint (G2)

**Fase final: Integração, entrega e documentação**
- [x] Review independente (G4)
- [x] Integração (G5) e aprovação (H2)
- [x] Deploy via pipeline (G6) — N/A até a SPEC-0006
- [x] Relatório de Entrega e CHANGELOG (G7)

## 8. Registro de Gates
| Gate | Status | Evidência | Data |
|---|---|---|---|
| G0 Spec | PASS | `spec_graph.py validate SPEC-0026` → G0 [SPEC-0026]: PASS — 0 erro(s), 0 aviso(s); checklist de julgamento do G0 revisado pelo Architect | 2026-09-30 |
| G1 Red | PASS | `verify SPEC-0026 --base sdd/SPEC-0001` no worktree (HEAD 9a49e15): red antes do green PASS, commits red só alteram testes PASS, rastreabilidade 3/3 PASS; `dotnet test --solution` → IT-01 e IT-02 vermelhos pelo motivo certo, IT-03 verde (guarda) | 2026-10-01 |
| G2 Green | PASS | Re-executado pelo Architect no worktree (HEAD 182e5de): build `-warnaserror` 0/0; `dotnet test --solution` → 37/37 (Implementer: 2 execuções completas 37/37 após `dotnet clean`); lint ok | 2026-10-01 |
| G3 Arquitetura | PASS | `dotnet test --project tests/Juker.ArchitectureTests -c Release` → 20/20 | 2026-10-01 |
| G4 Review | PASS | Reviewer independente: APPROVED em 182e5de (verify PASS 0/0; build 0/0; 37/37; arquitetura 20/20; lint ok; red de 56eacd9 confirmado pelo motivo certo). 2 minor + 1 nit → SPEC-0028 (proposta): variantes `ConfigureAwait(...).GetAwaiter().GetResult()` não banidas; IT-02 prova a fonte do endpoint mas não o preenchimento por `AddJukerModules`; doc do `ComposedModules` | 2026-10-01 |
| G5 Integração & CI | PASS | Merge --no-ff em `sdd/SPEC-0001` (2492381). No resultado integrado: build 0/0; `dotnet test --solution` 37/37; integração exit 0; arquitetura sem falhas; lint ok; nenhum pacote vulnerável. CI: N/A até a SPEC-0005 | 2026-10-01 |
| H2 Integração aprovada | PASS | autorização prévia de thomas no chat (2026-09-30: "continue trabalhando… opte pela recomendação"), confirmada por thomas no chat em 2026-10-13 | 2026-10-01 |
| G6 Deploy | N/A | CD adiado por decisão do fundador (SPEC-0006 estacionada); entrega = merge na `main` | 2026-10-01 |
| G7 Pronto & Docs | PASS | Relatório de Entrega com as 3 linhas de Verificação e a prova de correção; DoD marcada; CHANGELOG na branch de integração; pendência SPEC-0028 | 2026-10-01 |

## 9. Registro de Impedimentos
| ID | Aberto em | Fase/Gate | Tipo | Descrição | Tentativas | Responsável | Resolução | Fechado em |
|---|---|---|---|---|---|---|---|---|
| IMP-01 | 2026-09-30 | G2 | spec | Banir os awaiters dispara RS0030 no ponto de entrada gerado pelo xUnit v3 (XunitAutoGeneratedEntryPoint.cs) em todo projeto de teste; o Directory.Build.props, onde a correção cabe, está fora do touches | Supressão por .editorconfig não cobre todos os projetos; XUNIT_GENERATED_DISABLE_WARNINGS resolve (testado via linha de comando) | Architect (emenda de escopo) + thomas (autorização prévia) | Emenda de escopo — Directory.Build.props no touches com XUNIT_GENERATED_DISABLE_WARNINGS só em projetos de teste (autorização prévia de thomas) | 2026-09-30 |

## 10. Relatório de Entrega

### O que foi entregue
`GetAwaiter().GetResult()` de `Task`/`Task<T>`/`ValueTask`/`ValueTask<T>` passou a ser API proibida no build (RS0030), sem afetar o ponto de entrada gerado pelo xUnit v3. `/_diag/modules` lista os módulos que o host registrou no `ComposedModules` (preenchido por `AddJukerModules`), lido do DI a cada requisição; continua só em Development/Testing, com o 404 em Production agora testado. O `AGENTS.md` documenta as limitações do hook de pre-commit.

### Como foi feito
- Commits: scaffold `45ff3ba`, red `56eacd9`, green `9b0c4a0`, docs `182e5de`; merge `2492381`; CHANGELOG na branch de integração.
- Emendas de escopo: redação da linha de caracterização (sem IDs de outra spec) e `Directory.Build.props` no `touches` para o `XUNIT_GENERATED_DISABLE_WARNINGS` só em projetos de teste (via target antes do `CoreCompile`, porque `IsTestProject` ainda não existe na avaliação do props).

### Prova de Correção
- IT-01: no commit red `56eacd9`, o fixture com `Task.CompletedTask.GetAwaiter().GetResult()` compilava (asserção `ExitCode != 0` falhou, confirmado pelo Reviewer numa cópia do commit); no green, o build falha com RS0030.
- IT-02: no red, a resposta era os cinco nomes do catálogo (`contains 4 item(s) too many`); no green, `["Venues"]`.

### Verificação
| Teste | Comportamento | Resultado | Evidência |
|---|---|---|---|
| IT-01 | Build do fixture com `GetAwaiter().GetResult()` falha com RS0030 | PASS | `SyncOverAsyncViolationBuildTests.cs`; `dotnet test --solution Juker.slnx -c Release` no resultado integrado 2492381 (37/37) |
| IT-02 | `/_diag/modules` reflete o registro de módulos compostos | PASS | `DiagnosticsModulesTests.cs`; `dotnet test --solution Juker.slnx -c Release` no resultado integrado 2492381 (37/37) |
| IT-03 | `/_diag/modules` em Production → 404 | PASS | `DiagnosticsModulesTests.cs`; `dotnet test --solution Juker.slnx -c Release` no resultado integrado 2492381 (37/37) |

### Definição de Pronto
- [x] Teste de regressão falhou antes e passa depois da correção
- [x] Suíte completa, arquitetura e CI verdes (G2, G3, G5) — CI: N/A até a SPEC-0005
- [x] Review independente sem achados blocker/major (G4)
- [x] Padrão existente mantido
- [x] Disponível no ambiente-alvo via pipeline (G6) — N/A: CD adiado; integrado na `main`
- [x] Documentação/CHANGELOG atualizados quando aplicável (G7)
- [x] Outras ocorrências registradas como novas specs (ou nenhuma) — SPEC-0028

### Deploy
N/A — CD adiado (SPEC-0006). Integrado em `sdd/SPEC-0001` e na `main`.

### Pendências
- SPEC-0028 — awaiters configurados não banidos; prova do preenchimento pelo `AddJukerModules`; doc do `ComposedModules`.

## 11. Emendas
| Versão | Data | Mudança | Motivo | Specs impactadas | Aprovado por |
|---|---|---|---|---|---|
| escopo | 2026-10-01 | `touches` inclui `Directory.Build.props`; objetivo novo: `XUNIT_GENERATED_DISABLE_WARNINGS` só em projetos de teste | IMP-01: o banimento dos awaiters dispara no ponto de entrada gerado pelo xUnit v3 | Nenhuma | thomas — autorização prévia no chat (2026-09-30), confirmada por thomas no chat em 2026-10-13 |
| escopo | 2026-10-01 | Redação da linha de caracterização sem IDs de teste de outra spec | O `verify` lia os IDs da SPEC-0002 como testes planejados da SPEC-0026 (apontado pelo Test-writer) | Nenhuma | thomas — autorização prévia no chat (2026-09-30), confirmada por thomas no chat em 2026-10-13 |
