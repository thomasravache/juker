# Changelog

Todas as mudanças relevantes deste projeto são documentadas aqui.

O formato segue [Keep a Changelog](https://keepachangelog.com/pt-BR/1.1.0/)
e o projeto adota [Versionamento Semântico](https://semver.org/lang/pt-BR/).
Cada entrada cita a spec de origem (`SPEC-NNNN`); gere-as com `spec_graph.py report SPEC-NNNN --changelog`.

## [Unreleased]

### Fixed
- Teste instável do `dotnet test` aninhado (teste de contrato do TestKit): os testes que rodam a suíte aninhada ficam numa coleção serializada; 10 execuções completas seguidas sem falha (SPEC-0027)
- Variantes `ConfigureAwait(...).GetAwaiter().GetResult()` de Task/ValueTask também proibidas no build (SPEC-0028)
- `GetAwaiter().GetResult()` (Task/ValueTask) passa a ser API proibida no build; `/_diag/modules` lista os módulos que o host de fato compôs; nota sobre o hook de pre-commit no `AGENTS.md` (SPEC-0026)

### Added
- Pipeline de CI no GitHub Actions (`ci.yml`): build, lint, testes unitários, de integração, de arquitetura e E2E (Chromium e WebKit, traces em falha), pacotes vulneráveis e gitleaks, cobertura das linhas alteradas ≥ 80% (diff-cover), job SDD (`validate` + `pr-check`) e imagem no GHCR com a tag do SHA em push na `main`; mutation testing semanal não bloqueante (`mutation.yml`, Stryker.NET); Dependabot agrupado; ferramentas de CI fixadas por hash; guia em `docs/ci.md` (SPEC-0005)
- Harness de E2E: `Juker.TestKit.E2E` com AppHost no ambiente `E2E`, Chromium e WebKit, verificação de acessibilidade (axe-core), diagnóstico de falha (trace, vídeo e screenshot) e modo smoke por `JUKER_E2E_BASE_URL`; API de teste do host (`/_test/clock/advance`, `/_test/ping`) só no ambiente `E2E`; comandos `test_e2e` e `smoke_test` (SPEC-0025)
- Regra de zero testes: toda execução aceita projeto vazio sob filtro e um teste de convenção protege contra projeto que perdeu os testes; cobertura do ARQ-08 para NSubstitute/Moq, das referências do `Juker.ArchitectureTests` e do banco-modelo com migrations reais (SPEC-0027)
- Building blocks dos módulos: `Result`/`Error`, handlers com decorators de validação, logging e unidade de trabalho, erros como `ProblemDetails`, `ModuleDbContext` com schema e filtro de tenant, outbox/inbox com entrega exatamente uma vez e dispatcher em segundo plano (`Juker.SharedKernel` e o novo `Juker.Infrastructure`) (SPEC-0004)
- Harness de testes de integração (`Juker.TestKit`): Postgres real com banco por classe clonado de um modelo migrado, `JukerApiFactory` com relógio controlado e outbox desligado, `MigrationsGuard` e `FakeHttpServer` (SPEC-0003)
- Suíte de arquitetura com as regras ARQ-01..ARQ-08, teste de convenções de testes e política de tokens do design (SPEC-0003)
- Solução .NET 10 com AppHost do Aspire, host único e os cinco módulos com seus contratos; `/health/live`, `/health/ready` e `/_diag/modules` (SPEC-0002)
- Analisadores de qualidade (Meziantou, APIs proibidas no build), gerenciamento central de pacotes e `nuget.config` só com nuget.org (SPEC-0002)
- Controle SDD no repositório: `tools/sdd`, hooks de git (não ativados), template de PR e convenções em `AGENTS.md`/`CLAUDE.md` (SPEC-0002)
