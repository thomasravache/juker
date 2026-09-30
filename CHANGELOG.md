# Changelog

Todas as mudanças relevantes deste projeto são documentadas aqui.

O formato segue [Keep a Changelog](https://keepachangelog.com/pt-BR/1.1.0/)
e o projeto adota [Versionamento Semântico](https://semver.org/lang/pt-BR/).
Cada entrada cita a spec de origem (`SPEC-NNNN`); gere-as com `spec_graph.py report SPEC-NNNN --changelog`.

## [Unreleased]

### Added
- Solução .NET 10 com AppHost do Aspire, host único e os cinco módulos com seus contratos; `/health/live`, `/health/ready` e `/_diag/modules` (SPEC-0002)
- Analisadores de qualidade (Meziantou, APIs proibidas no build), gerenciamento central de pacotes e `nuget.config` só com nuget.org (SPEC-0002)
- Controle SDD no repositório: `tools/sdd`, hooks de git (não ativados), template de PR e convenções em `AGENTS.md`/`CLAUDE.md` (SPEC-0002)
