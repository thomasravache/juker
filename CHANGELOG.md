# Changelog

Todas as mudanças relevantes deste projeto são documentadas aqui.

O formato segue [Keep a Changelog](https://keepachangelog.com/pt-BR/1.1.0/)
e o projeto adota [Versionamento Semântico](https://semver.org/lang/pt-BR/).
Cada entrada cita a spec de origem (`SPEC-NNNN`); gere-as com `spec_graph.py report SPEC-NNNN --changelog`.

## [Unreleased]

### Fixed
- Variantes `ConfigureAwait(...).GetAwaiter().GetResult()` de Task/ValueTask também proibidas no build (SPEC-0028)
- `GetAwaiter().GetResult()` (Task/ValueTask) passa a ser API proibida no build; `/_diag/modules` lista os módulos que o host de fato compôs; nota sobre o hook de pre-commit no `AGENTS.md` (SPEC-0026)

### Added
- Building blocks dos módulos: `Result`/`Error`, handlers com decorators de validação, logging e unidade de trabalho, erros como `ProblemDetails`, `ModuleDbContext` com schema e filtro de tenant, outbox/inbox com entrega exatamente uma vez e dispatcher em segundo plano (`Juker.SharedKernel` e o novo `Juker.Infrastructure`) (SPEC-0004)
- Harness de testes de integração (`Juker.TestKit`): Postgres real com banco por classe clonado de um modelo migrado, `JukerApiFactory` com relógio controlado e outbox desligado, `MigrationsGuard` e `FakeHttpServer` (SPEC-0003)
- Suíte de arquitetura com as regras ARQ-01..ARQ-08, teste de convenções de testes e política de tokens do design (SPEC-0003)
- Solução .NET 10 com AppHost do Aspire, host único e os cinco módulos com seus contratos; `/health/live`, `/health/ready` e `/_diag/modules` (SPEC-0002)
- Analisadores de qualidade (Meziantou, APIs proibidas no build), gerenciamento central de pacotes e `nuget.config` só com nuget.org (SPEC-0002)
- Controle SDD no repositório: `tools/sdd`, hooks de git (não ativados), template de PR e convenções em `AGENTS.md`/`CLAUDE.md` (SPEC-0002)
