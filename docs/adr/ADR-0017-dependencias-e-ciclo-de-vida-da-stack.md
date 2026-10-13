---
id: ADR-0017
title: Dependências e ciclo de vida — varredura no CI, Dependabot semanal, versões centralizadas e calendário de fim de suporte
status: proposed
origin: decision
date: 2026-10-13
pillars: [dependencias]
baseline: dependencias@2026.09
decision_makers: [thomas]
consulted: [claude]
informed: []
supersedes:
superseded_by:
enforced_by: SPEC-0005 (job security com `dotnet list package --vulnerable`, `dependabot.yml`); gerenciamento central de pacotes (SPEC-0002); `verify` avisa manifesto alterado sem ADR
---

# ADR-0017 — Dependências e ciclo de vida — varredura no CI, Dependabot semanal, versões centralizadas e calendário de fim de suporte

## Contexto e Problema
O Juker depende do .NET 10, do Aspire, do EF Core/Npgsql, do PostgreSQL 18, de pacotes NuGet e de actions do GitHub. Vulnerabilidade sem correção ou runtime fora de suporte viram risco sem ninguém perceber num projeto de um dev só.

## Direcionadores da Decisão
- Dev solo: atualizações precisam chegar sozinhas, como PR revisável.
- Suíte de testes ampla para aceitar atualizações com confiança.
- Stack escolhida pelo suporte longo (ADR-0002, ADR-0004).

## Opções Consideradas
- Dependabot (NuGet e GitHub Actions) + `dotnet list package --vulnerable` no CI
- Renovate

## Resultado da Decisão
**Opção escolhida:** Dependabot + varredura no CI, porque já vem no GitHub, sem app extra, e cobre NuGet e Actions.

**Regras (verificáveis):**
- `dotnet list package --vulnerable --include-transitive` no job `security`; severidade High ou Critical bloqueia o merge (SPEC-0005).
- Dependabot semanal para NuGet e GitHub Actions; cada PR passa pelos mesmos checks e só entra com CI verde. Pacote novo (não atualização) continua exigindo ADR.
- Nenhum runtime fora de suporte: .NET 10 LTS (suporte até 14/11/2028) e PostgreSQL 18 (até nov/2030). O calendário é revisado a cada 6 meses; migração de versão maior vira spec `type: migration`.
- Versões fixadas no `Directory.Packages.props` (gerenciamento central estrito) e no `global.json`; actions fixadas por SHA.
- Licenças compatíveis com uso comercial; biblioteca com licença comercial ou restritiva exige ADR (ex.: MediatR e FluentAssertions descartados no ADR-0002 e ADR-0011).

### Consequências
- **Boa**, porque vulnerabilidade conhecida não chega à `main` e as atualizações chegam como PRs pequenos.
- **Ruim**, porque os PRs do Dependabot gastam minutos do CI e precisam de atenção semanal. Regra adaptada do modelo: em vez de lockfile do NuGet, as versões ficam no gerenciamento central (`packages.lock.json` não é usado).

### Confirmação (G3)
`security_scan` no sdd-config e no CI (SPEC-0005); `verify` avisa manifesto alterado sem ADR.

## Prós e Contras das Opções
| Critério (peso) | Dependabot | Renovate |
|---|---|---|
| Esforço de configuração (4) | 5 | 3 |
| Flexibilidade de agrupamento (2) | 3 | 5 |
| Já integrado ao GitHub (3) | 5 | 3 |

## Mais Informações
Referências neutras: SLSA, OpenSSF Scorecard. Fins de suporte já verificados no ADR-0002 (https://dotnet.microsoft.com/en-us/platform/support/policy/dotnet-core) e no ADR-0004 (https://endoflife.date/postgresql). Dependabot: https://docs.github.com/code-security/dependabot (não verificado nesta data).
