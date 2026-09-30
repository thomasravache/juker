# AGENTS.md — Juker

Instruções para agentes (Claude Code, Codex, Gemini e outros) e para quem contribui no Juker.

## Projeto
Jukebox colaborativa para bares: monólito modular em .NET 10 com vertical slices por módulo, um único host
(API + SignalR + Blazor na mesma origem) e Aspire para orquestração local (ADR-0001, ADR-0002, ADR-0003, ADR-0009).

## Estrutura
- `src/Aspire/Juker.AppHost` — orquestração local (Postgres 18 + host); `src/Aspire/Juker.ServiceDefaults` — OpenTelemetry e health checks.
- `src/Juker.Host` — único ponto de composição: registra os módulos pelo `ModuleCatalog`.
- `src/Modules/<Módulo>/Juker.Modules.<Módulo>` (interno) e `.Contracts` (público). Módulos: Venues, Sessions, Catalog, Queue, Playback.
  Um módulo só referencia o `.Contracts` de outro (ADR-0001, ARQ-01..05).
- `src/BuildingBlocks/Juker.SharedKernel`; `src/Web/Juker.Web` e `src/Web/Juker.Web.Client` (frontend).
- `tests/` — testes; `tests/fixtures/` — projetos fora da solução usados pelos testes.
- `docs/specs/` e `docs/adr/` — specs e decisões (só o Architect edita).

## Comandos (raiz do repositório)
| Objetivo | Comando |
|---|---|
| Build | `dotnet build Juker.slnx -c Release -warnaserror` |
| Testes (tudo) | `dotnet test --solution Juker.slnx -c Release` (Microsoft.Testing.Platform; Docker necessário) |
| Integração | `dotnet test --solution Juker.slnx -c Release --filter-trait "Category=Integration"` |
| Arquitetura | `dotnet test --project tests/Juker.ArchitectureTests -c Release` (regras ARQ-01..ARQ-08) |
| Guia do harness | `docs/testing.md` (fixtures, convenções, `JukerAllowZeroTests`) |
| Lint | `dotnet format Juker.slnx --verify-no-changes` (corrigir: `dotnet format Juker.slnx`) |
| Rodar localmente | `dotnet run --project src/Aspire/Juker.AppHost` |
| Estado das specs | `python3 tools/sdd/spec_graph.py status` |

## Convenções de código
- Configuração comum em `Directory.Build.props` (net10.0, `Nullable`, `TreatWarningsAsErrors`, `AnalysisLevel=latest-recommended`,
  `EnforceCodeStyleInBuild`, Meziantou.Analyzer e BannedApiAnalyzers). Não repita essas propriedades nos `.csproj`.
- Pacotes: versões só em `Directory.Packages.props` (gerenciamento central estrito); `PackageReference` sem `Version`.
  Fontes só pelo `nuget.config` da raiz (nuget.org com package source mapping).
- APIs proibidas (`BannedSymbols.txt`, RS0030): relógio do sistema (use `TimeProvider`), `new HttpClient()`
  (use `IHttpClientFactory`), `.Result`/`.Wait()` (use `await`), `Console` fora de `src/Juker.Host/Cli/**` (use `ILogger`);
  em projetos de teste também `Thread.Sleep`/`Task.Delay` (espere por condição).
- Regras de analisador só são desligadas no `.editorconfig`, com comentário do motivo. `#pragma warning disable` exige
  comentário de justificativa na mesma linha ou na anterior.
- Sem MediatR, MassTransit ou FluentAssertions (ADR-0002, ADR-0011); asserções com AwesomeAssertions.
- Logs estruturados (`logger.LogInformation("... {Campo}", valor)`), nunca interpolação.

## Commits, changelog e versões
- **Conventional Commits 1.0**: `<tipo>(<escopo>)?: <descrição>` com tipos `build|chore|ci|docs|feat|fix|perf|refactor|revert|style|test`.
  Todo commit de comportamento leva o rodapé `Refs: SPEC-NNNN`; mudança sem comportamento marca `[no-spec]` no assunto.
- Ordem TDD: o commit `test(...)` vem antes do `feat|fix|refactor(...)` da mesma spec.
- **CHANGELOG.md** no formato [Keep a Changelog](https://keepachangelog.com/pt-BR/1.1.0/), cada entrada citando a spec.
- **SemVer** ([Versionamento Semântico](https://semver.org/lang/pt-BR/)): quebra de compatibilidade = major (`!` no commit).
- Hooks em `.githooks/` (mensagem de commit, validação de specs e lint). A ativação é decisão do fundador:
  `git config core.hooksPath .githooks`.

<!-- sdd:start -->
## Processo de desenvolvimento (SDD)

Este projeto usa Spec-Driven Development. Vale para qualquer mudança de comportamento (feature, bug, refactor, migração), qualquer agente e qualquer modelo:

1. **Carregue a skill `sdd-management` antes de agir.** Sem ela disponível, siga `docs/specs/INDEX.md` e as regras abaixo.
2. **Não dependa da memória da conversa.** Antes de cada passo rode `python3 tools/sdd/spec_graph.py status` (ou `status SPEC-NNNN`) e execute o próximo passo indicado.
3. **Nada é implementado sem spec aprovada pelo humano (H1).** Dúvidas vão para "Questões em Aberto"; paradas, para o "Registro de Impedimentos" (`impede` / `resolve`).
4. **Testes antes do código.** Commit `test(...)` antes de `feat|fix|refactor(...)`. Mensagens em Conventional Commits com rodapé `Refs: SPEC-NNNN`.
5. **Arquitetura, padrões e bibliotecas existentes são mantidos.** Desvio só com spec `type: migration` ou ADR aprovado.
6. **Só o Architect edita `docs/specs/` e `docs/adr/`.** Subagentes reportam; não editam specs.
7. **PRs:** corpo gerado por `spec_graph.py pr SPEC-NNNN`; o CI roda `pr-check` e bloqueia o merge se algo faltar.
8. **Board:** definido em `tracker` no `docs/specs/sdd-config.yml` (`none` = só o repositório).
<!-- sdd:end -->
