---
id: SPEC-0027
title: Pendências do review da SPEC-0003
tier: lite
type: refactor
user_facing: false
status: in-progress
created: 2026-10-01
parent: SPEC-0001
depends_on: [SPEC-0003]
consumes_contract: []
touches: [tests/Juker.ArchitectureTests/**, tests/Juker.TestKit/**, tests/Juker.IntegrationTests/Harness/**, tests/Juker.IntegrationTests/Fixtures/**, tests/Directory.Build.targets, tests/Juker.BuildingBlocks.UnitTests/Juker.BuildingBlocks.UnitTests.csproj, tests/Directory.Build.props, tests/Juker.Modules.Catalog.UnitTests/Juker.Modules.Catalog.UnitTests.csproj, tests/Juker.Modules.Playback.UnitTests/Juker.Modules.Playback.UnitTests.csproj, tests/Juker.Modules.Queue.UnitTests/Juker.Modules.Queue.UnitTests.csproj, tests/Juker.Modules.Sessions.UnitTests/Juker.Modules.Sessions.UnitTests.csproj, tests/Juker.Modules.Venues.UnitTests/Juker.Modules.Venues.UnitTests.csproj, tests/Juker.Web.UnitTests/Juker.Web.UnitTests.csproj, docs/testing.md]
adrs: [ADR-0012, ADR-0014]
external: []
size: S
approved_by: thomas (autorização prévia de 2026-09-30, confirmada por thomas no chat em 2026-10-13)
approved_at: 2026-09-30
---

# SPEC-0027 — Pendências do review da SPEC-0003

## 1. Problema
O review independente da SPEC-0003 (G4 aprovado em ef40d71) deixou quatro lacunas de cobertura no próprio harness:
1. O ramo do ARQ-08 que detecta mocks de `HttpMessageHandler` via NSubstitute/Moq (argumento genérico) funciona, mas não tem caso no fixture `Violations`: uma regressão nele passaria despercebida.
2. Um projeto de teste novo que não seja referenciado pelo `Juker.ArchitectureTests.csproj` (com `ReferenceOutputAssembly=false`) só é percebido num `arch_test` a partir de um build limpo.
3. O caminho do banco-modelo que aplica as migrations reais (`TemplateDatabase`) ainda não é exercitado, porque não existe nenhum `DbContext` na solução.
4. **Teste instável:** o CT-01 da SPEC-0003 (`Comando_test_integration_lista_somente_os_testes_Integration`) roda um `dotnet test --solution` aninhado dentro da suíte e falhou de forma intermitente em execuções completas (visto duas vezes em 2026-10-01), provavelmente por disputa de `bin`/`obj` com outros builds aninhados.
5. **Opt-in de zero testes com filtro:** projetos de teste só com testes `Unit` (ex.: `Juker.BuildingBlocks.UnitTests`) ficam vazios sob o filtro `Category=Integration` e precisam manter `JukerAllowZeroTests=true` mesmo tendo testes, o que enfraquece a proteção contra projeto que perde todos os testes (apontado na SPEC-0004).
6. A política de tokens do design aponta como cor qualquer trecho hexadecimal em string C# ou em atributo fora da lista de âncoras (ex.: `NavigateTo("#add")`, `id="#abc"`): é um falso positivo seguro, mas não documentado.

## 2. Causa Raiz
Casos não cobertos pelo plano de testes original da SPEC-0003 (achados minor do G4, aceitos como pendência).

## 3. Mudança Proposta
- Fixture `Violations`: tipos fake nos namespaces `NSubstitute` (`Substitute.For<T>()`) e `Moq` (`Mock<T>`), um infrator de cada que usa `HttpMessageHandler` e um controle que mocka outro tipo; os infratores entram no conjunto exato do ARQ-08.
- Teste de convenção: todo projeto `tests/**` do `Juker.slnx` (exceto fixtures) é `ProjectReference` do `Juker.ArchitectureTests.csproj`.
- Fixture de `DbContext` "limpo" (com migration) aplicado pelo `TemplateDatabase`: o banco da classe tem a tabela dessa migration.
- CT-01 da SPEC-0003 isolado: o `dotnet test` aninhado usa `--no-build` sobre artefatos próprios (`--artifacts-path` temporário) ou a coleção do teste é serializada em relação aos outros builds aninhados; aceitar a correção só com 10 execuções completas seguidas sem falha.
- Regra de zero testes que distingue "projeto vazio sob filtro" de "projeto sem testes" (emenda de 2026-10-13): toda execução aceita zero testes (`--ignore-exit-code 8` para todos os projetos de `tests/`, no `tests/Directory.Build.targets`, sem duplicar a opção — duplicada, a plataforma termina com o código 5), e a proteção contra projeto que perdeu os testes passa a ser estática: um teste de convenção exige pelo menos um teste em todo projeto de teste sem `JukerAllowZeroTests`, e zero testes em quem tem o opt-in. `Juker.BuildingBlocks.UnitTests` e `Juker.ArchitectureTests` deixam o opt-in. Os comandos `test`, `test_integration` e `arch_test` (contrato da SPEC-0003) não mudam.
- `docs/testing.md`: como escrever navegação por fragmento em `.razor` sem acionar a política de tokens.
- **Não muda:** contrato do `Juker.TestKit` (snapshot), regras ARQ-01..ARQ-08 em produção.

**Padrão seguido:** `tests/Juker.ArchitectureTests/Fixtures/Violations/**`, `tests/Juker.ArchitectureTests/Conventions/**`, `tests/Juker.IntegrationTests/Harness/**` (SPEC-0003).

**Rollback:** revert dos commits da spec.

**Outras ocorrências:** Nenhuma.

## 4. Plano de Testes (TDD)
- Caracterização: N/A — área já coberta pelos testes da SPEC-0003, que continuam valendo.
- **UT-01** — (guarda: passa antes da mudança) Dado o fixture com `Substitute.For<HttpMessageHandler>()` e `new Mock<HttpMessageHandler>()`, quando o ARQ-08 avalia, então os dois são apontados e o controle `Substitute.For<IDisposable>()` não é.
- **UT-02** — Dado o `Juker.slnx`, quando o teste de convenção compara os projetos `tests/**` com as `ProjectReference` do `Juker.ArchitectureTests.csproj`, então falha listando qualquer projeto ausente (hoje passa; com um projeto de exemplo ausente num fixture de solução, falha).
- **IT-02** — (regressão do teste instável) 10 execuções seguidas de `dotnet test --solution Juker.slnx -c Release` terminam sem nenhuma falha do teste de contrato do TestKit da SPEC-0003 (evidência: script de repetição e saídas).
- **UT-03** — Dado o `Juker.slnx`, quando o teste de convenção conta os métodos de teste (`[Fact]`/`[Theory]`) de cada projeto `tests/**` (exceto fixtures), então falha listando o projeto sem `JukerAllowZeroTests` que tem zero testes e o projeto com `JukerAllowZeroTests=true` que tem testes (hoje falha para `Juker.BuildingBlocks.UnitTests` e `Juker.ArchitectureTests`).
- **IT-03** — Com o opt-in removido dos projetos que têm testes, `dotnet test --solution Juker.slnx -c Release --filter-trait "Category=Integration"` e `... --filter-trait "Category=Unit"` (comando do job `unit` da SPEC-0005) terminam com sucesso (evidência: saídas dos comandos).
- **IT-01** — (guarda: passa antes da mudança — lacuna de cobertura, não defeito) Dado um `DbContext` de fixture com uma migration, quando o `TemplateDatabase` prepara o modelo, então o banco clonado da classe contém a tabela criada pela migration.

## 5. Questões em Aberto
Nenhuma.

## 6. Aprovação (H1)
Registrada no frontmatter (`approved_by`, `approved_at`) somente depois que o humano responder "Aprovado".

## 7. Checklist de Implementação

**Fase 1: Regressão**
- [ ] Red: escrever UT-01, UT-02, UT-03, IT-01, IT-02, IT-03 com a tag `SPEC-0027:<ID>`; confirmar que as regressões falham pelo motivo certo (guardas marcadas passam antes da mudança)
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
| G0 Spec | PASS | `spec_graph.py validate SPEC-0027` → 0 erros; aguardando H1 do fundador | 2026-10-01 |
| G1 Red | PASS | verify G1: PASS; `dotnet test --solution Juker.slnx -c Release` exit 2 (red: Test run completed with non-success exit code: 2 (see: https://aka.ms/testingplatform/exit) — 13426e8 | 2026-10-13 |
| G2 Green | PASS | build exit 0 (Time Elapsed 00:00:03.36); test exit 0 (duration: 1m 06s 884ms); lint exit 0 () — c9b658b | 2026-10-13 |
| G3 Arquitetura | PASS | arch_test exit 0 (duration: 1s 537ms) — c9b658b | 2026-10-13 |
| G4 Review | FAIL | Reviewer independente: CHANGES_REQUESTED em c9b658b — major: IT-03 (FilteredTestCommandsTests) roda o dotnet test aninhado fora da coleção serializada; minors: condição do --ignore-exit-code ampla demais (Directory.Build.targets:11), comentários do opt-in desatualizados fora do touches (Directory.Build.props e 6 .csproj vazios), comentário do NestedTestRun afirma a causa como fato | 2026-10-13 |
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
| escopo | 2026-10-13 | `touches` inclui `tests/Directory.Build.props` e os `.csproj` dos 6 projetos de UT vazios, só para corrigir os comentários sobre o `JukerAllowZeroTests` (sem mudar comportamento) | G4 (minor): os comentários dizem que o opt-in é "aplicado em tests/Directory.Build.targets", o que deixou de ser verdade com esta spec | Nenhuma | pendente — thomas |
| escopo | 2026-10-13 | IT-01 passa a ser guarda (passa antes da mudança); IT-02 tem evidência por repetição (script `tests/Juker.IntegrationTests/Harness/repeat-test-suite.sh`, 10 execuções completas), sem vermelho determinístico; redação do IT-02 sem o ID de teste de outra spec | Test-writer no G1: o caminho do `TemplateDatabase` com migrations reais já funciona (sem a migration o banco só tem `__EFMigrationsHistory`; com ela, a tabela aparece); 10 execuções completas seguidas passaram nesta máquina e nenhum escritor concorrente de `bin`/`obj` foi encontrado; o `verify` lia "CT-01 da SPEC-0003" como teste desta spec | Nenhuma | thomas — no chat em 2026-10-13 ("Aprovado") |
| escopo | 2026-10-13 | Item 5 (zero testes com filtro) ganha desenho e testes: toda execução aceita zero testes e a proteção vira o teste de convenção UT-03; IT-03 prova os comandos filtrados; `touches` inclui `tests/Directory.Build.targets` e o `.csproj` do `Juker.BuildingBlocks.UnitTests` | Revisão do Architect antes do despacho: o item 5 não tinha teste no plano e os arquivos que ele muda estavam fora do `touches`; o job `unit` da SPEC-0005 (`Category=Unit`) esvazia o `Juker.IntegrationTests` e o `Juker.ArchitectureTests` do mesmo jeito | SPEC-0005 (passa a depender desta spec) | thomas — no chat em 2026-10-13 ("Aprovado!") |
