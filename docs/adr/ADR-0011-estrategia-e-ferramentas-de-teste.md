---
id: ADR-0011
title: Ferramentas de teste — xUnit v3, AwesomeAssertions, NSubstitute, bUnit, Testcontainers, Playwright .NET, ArchUnitNET e CsCheck
status: accepted
origin: decision
date: 2026-09-30
decision_makers: [thomas]
consulted: [claude]
informed: []
supersedes:
superseded_by:
enforced_by: ARQ-06 (sem FluentAssertions ≥ 8) e SPEC-0003 (harness com exemplo de cada nível)
---

# ADR-0011 — Ferramentas de teste

## Contexto e Problema
A SDD exige UT, IT com dependências reais, CT, E2E e testes de arquitetura, todos marcados com tags `SPEC-NNNN:<ID>`. É preciso escolher ferramentas atuais e com licença adequada para produto comercial.

## Direcionadores da Decisão
- Licenças permissivas (FluentAssertions ≥ 8 virou comercial).
- IT contra Postgres real (Testcontainers).
- E2E em .NET (mesma linguagem) para cliente, TV e painel.
- Algoritmo da fila justa com testes baseados em propriedades.

## Opções Consideradas
- xUnit v3 + AwesomeAssertions (fork MIT do FluentAssertions 7) + NSubstitute + bUnit + Testcontainers + Playwright .NET + ArchUnitNET + CsCheck
- NUnit + Shouldly + Moq + Cypress (JS)
- MSTest + asserções nativas + Selenium

## Resultado da Decisão
**Opção escolhida:** a primeira. Convenções:
- Tag: `[Trait("Spec", "SPEC-NNNN:UT-01")]`; categoria de nível: `[Trait("Category", "Integration" | "E2E" | "Smoke" | "Architecture")]`.
- Projetos: `tests/Juker.Modules.<Módulo>.UnitTests`, `tests/Juker.IntegrationTests` (WebApplicationFactory + Testcontainers Postgres 18, um banco por classe de teste), `tests/Juker.E2ETests` (Playwright contra o app iniciado pelo `Aspire.Hosting.Testing` localmente e contra a URL de staging no deploy), `tests/Juker.ArchitectureTests`, `tests/Juker.Web.UnitTests` (bUnit).
- Tempo e IDs controlados: `TimeProvider` (FakeTimeProvider nos testes) e geradores de ID injetados.
- YouTube nos IT: servidor HTTP fake (WireMock.Net) com respostas gravadas do contrato da Data API. Nos E2E: provedor fake de mídia com vídeos curtos locais atrás da mesma interface (o IFrame real só no teste manual de Premium, SPEC-0007).
- **Como** cada nível é usado está nos ADRs de estratégia: testes de integração no **ADR-0012** e E2E/testes de interface no **ADR-0013**. Este ADR fica só com a escolha das ferramentas.

### Consequências
- **Boa**, porque tudo em C#, com licenças permissivas e suporte ativo.
- **Ruim**, porque os E2E com Playwright exigem os navegadores instalados no CI (cache no workflow).

### Confirmação (G3)
ARQ-06 impede FluentAssertions (qualquer versão), MediatR e MassTransit. SPEC-0003 entrega um exemplo de cada nível rodando no CI.

## Prós e Contras das Opções
| Critério (peso) | xUnit v3 + Awesome + Playwright | NUnit + Shouldly + Cypress | MSTest + Selenium |
|---|---|---|---|
| Licença (5) | 5 | 5 | 5 |
| Ecossistema/manutenção (4) | 5 | 4 | 3 |
| Uma linguagem só (3) | 5 | 2 | 5 |
| Estabilidade de E2E (4) | 5 | 4 | 2 |
| **Total ponderado** | **80** | **67** | **60** |

## Mais Informações
- xUnit v3 4.0.1 (12/09/2026) — https://www.nuget.org/packages/xunit.v3 (2026-09-30).
- Testcontainers for .NET 4.15.0 (07/09/2026) — https://www.nuget.org/packages/Testcontainers/ (2026-09-30).
- FluentAssertions v8 comercial; AwesomeAssertions (MIT) — https://www.infoq.com/news/2025/01/fluent-assertions-v8-license/ e https://github.com/AwesomeAssertions/AwesomeAssertions (2026-09-30).
- ArchUnitNET ativo; NetArchTest.Rules sem release desde 2021 — https://dotnet.libhunt.com/compare-archunitnet-vs-netarchtest (2026-09-30).
- As versões de Playwright .NET, bUnit, NSubstitute, WireMock.Net e CsCheck serão fixadas em `Directory.Packages.props` na SPEC-0002/0003, verificando a estável mais recente nesse momento.
