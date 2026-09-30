---
id: SPEC-0003
title: Harness de testes de integração e suíte de arquitetura
tier: full
type: foundation
user_facing: false
status: approved
created: 2026-09-30
parent: SPEC-0001
depends_on: [SPEC-0002]
consumes_contract: []
contract_version: 1
touches: [tests/Juker.TestKit/**, tests/Juker.IntegrationTests/**, tests/Juker.ArchitectureTests/**, tests/Juker.Modules.*/**, tests/Juker.BuildingBlocks.UnitTests/**, tests/Juker.Web.UnitTests/**, tests/Directory.Build.props, Directory.Packages.props, Juker.slnx, docs/testing.md, tools/recordings/**]
adrs: [ADR-0001, ADR-0002, ADR-0004, ADR-0007, ADR-0011, ADR-0012, ADR-0014]
external: []
size: M
approved_by: thomas
approved_at: 2026-09-30
---

# SPEC-0003 — Harness de testes de integração e suíte de arquitetura

## 1. Visão Geral
Entrega os projetos de teste de unidade, integração e arquitetura, o `Juker.TestKit` de integração e a suíte de arquitetura com as regras ARQ-01..ARQ-08, implementando a estratégia do ADR-0012: Postgres real, banco por classe a partir de um modelo migrado, esquemas de auth reais e tempo controlado. O harness de E2E (ADR-0013) fica na SPEC-0025. A partir daqui, toda spec escreve seus UT/IT em cima desse harness e com as mesmas convenções.

## 2. Motivação & Escopo
**Motivação:** sem um harness comum, cada spec inventaria sua forma de subir banco e app, e os gates G1/G2/G3 não teriam comandos únicos.

**Objetivos (dentro do escopo):**
- Projetos: `tests/Juker.Modules.{Venues,Sessions,Catalog,Queue,Playback}.UnitTests`, `tests/Juker.BuildingBlocks.UnitTests`, `tests/Juker.IntegrationTests`, `tests/Juker.ArchitectureTests`, `tests/Juker.Web.UnitTests` (bUnit) e `tests/Juker.TestKit` (fixtures compartilhadas).
- `TestKit` (integração, ADR-0012): `PostgresFixture` (um container `postgres:18` por execução; banco `juker_template` com as migrations reais de todos os módulos; um banco por classe clonado do modelo), `MigrationsGuard` (falha se algum `DbContext` tiver mudanças sem migration), `JukerApiFactory` (WebApplicationFactory com o banco da classe, `FakeTimeProvider`, `Outbox:DispatcherEnabled=false` — o `DrainOutboxAsync` é acrescentado pela SPEC-0004 —, os esquemas de auth reais e o ponto de extensão para emissores de teste — os emissores vêm da SPEC-0009), `FakeHttpServer` (WireMock.Net com respostas gravadas) e builders base.
- Teste de política do design system (ADR-0014, regra 7) em `tests/Juker.ArchitectureTests/DesignTokens`: falha com cor literal ou `font-family` em `.razor`, `.razor.css` e `.css` fora de `src/Web/Juker.Web/Styles/theme.css`.
- `tools/recordings/refresh-youtube.sh`: regrava as respostas da Data API usadas pelo `FakeHttpServer` (usado a partir da SPEC-0014).
- Convenções: `[Trait("Spec","SPEC-NNNN:<ID>")]` obrigatório em todo teste; `[Trait("Category","Unit|Integration|E2E|Smoke|Architecture")]`.
- `tests/Juker.ArchitectureTests` com ArchUnitNET implementando ARQ-01..ARQ-07 (SPEC-0001) e **ARQ-08** (ADR-0012): nenhum projeto de teste referencia EF Core InMemory, EF Core Sqlite ou mocks de `HttpMessageHandler`.
- `docs/testing.md` com o guia de uso do harness.
- Preenche em `docs/specs/sdd-config.yml` os comandos `test`, `test_integration` e `arch_test`.

**Não-objetivos (fora do escopo):**
- Testes de funcionalidades (cada spec traz os seus).
- Harness de E2E, API `/_test/*` e Playwright (SPEC-0025).
- Execução no CI (SPEC-0005).
- Provedor fake de mídia para E2E da TV (SPEC-0007).

## 3. Dependências
- **Implementações necessárias:** SPEC-0002 — a solução e os projetos-alvo das regras de arquitetura precisam existir.
- **Contratos consumidos:** N/A
- **Pré-requisitos externos:** Docker disponível para Testcontainers.

## 4. Decisão Arquitetural
**Contexto:** projeto novo; ferramentas definidas no ADR-0011, estratégia de integração no ADR-0012 e regras de fronteira no ADR-0001, ADR-0002, ADR-0004 e ADR-0007.

**Decisão:** um `TestKit` compartilhado e um projeto de testes por nível/módulo, com a suíte de arquitetura lendo os assemblies compilados da solução.

**Justificativa:** fixtures únicas eliminam divergência entre specs, e as regras de arquitetura viram testes que rodam no `arch_test`.

**Desvio do padrão existente:** Nenhum.

**Alternativas descartadas:** fixtures por projeto de teste (duplicação); banco compartilhado limpo com Respawn e handler de auth que libera tudo (ADR-0012).

**ADRs:** ADR-0001, ADR-0002, ADR-0004, ADR-0007, ADR-0011, ADR-0012, ADR-0014.

## 5. Requisitos Não-Funcionais
- **Desempenho e escala:** suíte de UT em menos de 30 s; subida da `PostgresFixture` em menos de 10 s com a imagem em cache; criação de banco por classe a partir do modelo em menos de 1 s — IT-04 e saída do `dotnet test`.
- **Segurança:** N/A — somente código de teste, sem segredos (senhas de container geradas por execução).
- **Privacidade e dados pessoais:** N/A — dados sintéticos de builders.
- **Disponibilidade e resiliência:** testes determinísticos: sem `Thread.Sleep`/`Task.Delay` e sem retry de teste; tempo por `FakeTimeProvider`; eventos entregues por `DrainOutboxAsync`, acrescentado pela SPEC-0004 — verificado no G4.
- **Acessibilidade (UI):** N/A nesta spec (o helper axe-core do E2E vem na SPEC-0025).
- **Custo:** N/A — execução local/CI gratuita.

## 6. Artefato A — Contrato
**Interface:** API de integração do `Juker.TestKit`, convenções de tag/categoria e os comandos de teste do `sdd-config.yml`.

```text
namespace Juker.TestKit                         // integração (ADR-0012)
  sealed class PostgresFixture : IAsyncLifetime  // 1 container por execução; juker_template migrado
    Task<string> CreateDatabaseAsync()           // banco novo da classe, clonado do modelo (< 1 s)
  static class MigrationsGuard { void AssertNoPendingModelChanges(); }   // todos os DbContext
  sealed class JukerApiFactory(PostgresFixture db) : WebApplicationFactory<Program>
    HttpClient CreateClient()
    FakeTimeProvider Time                         // substitui o TimeProvider do host
    void UseTestIssuers(Action<IServiceCollection>)   // emissores de teste (SPEC-0009)
    // Outbox:DispatcherEnabled=false; esquemas de auth reais
  sealed class FakeHttpServer : IDisposable      // WireMock.Net + Recordings/*.json
    Uri BaseUrl

Convenções:
  [Trait("Spec", "SPEC-NNNN:<ID>")]                 obrigatório em todo [Fact]/[Theory]
  [Trait("Category", "Unit"|"Integration"|"E2E"|"Smoke"|"Architecture")]

Comandos (sdd-config.yml):
  test:             dotnet test Juker.slnx -c Release
  test_integration: dotnet test Juker.slnx -c Release --filter Category=Integration
  arch_test:        dotnet test tests/Juker.ArchitectureTests -c Release
```

**Arquivos/módulos afetados:** ver `touches` no frontmatter.

### 6.1 Mapa de Comportamentos
| Cenário | Condição / Entrada | Resultado esperado | Testes |
|---|---|---|---|
| Rastreabilidade obrigatória | teste sem `Trait("Spec", ...)` na solução | teste de convenção falha listando o método | UT-01 |
| Categoria obrigatória | teste sem `Trait("Category", ...)` | teste de convenção falha listando o método | UT-01 |
| Tokens do design | `.razor` com `#ff007a`, `bg-[#fff]`, `rgb(` ou `font-family` fora do `theme.css` | teste de política falha citando arquivo e linha; o `theme.css` pode ter cores | UT-02 |
| Banco isolado por classe | duas classes gravam a mesma chave | nenhuma enxerga os dados da outra; extensões `pg_trgm`/`unaccent` disponíveis | IT-01 |
| Banco a partir do modelo | nova classe de teste | banco clonado do `juker_template` com as migrations reais, em < 1 s | IT-04 |
| Migration esquecida | `DbContext` com mudança no modelo sem migration | `MigrationsGuard` falha citando o contexto | IT-05 |
| Regras de arquitetura detectam violação | assembly de fixture com cada violação ARQ-01..ARQ-08 | cada regra falha com a violação correspondente | IT-02 |
| Regras de arquitetura aprovam a solução | assemblies reais da solução | todas as regras passam | IT-02 |
| Host de teste real | `JukerApiFactory` com o banco da classe | `/health/ready` = 200, `TimeProvider` substituído pelo `FakeTimeProvider` e dispatcher do outbox desligado | IT-03 |
| Filtros por categoria | `--filter Category=Integration` | só testes de integração são executados | CT-01 |

## 7. Artefato B — Plano de Testes (TDD)

### 7.1 Testes de Caracterização
- N/A — projeto novo.

### 7.2 Testes Unitários
- **UT-01** — Dado todos os assemblies de teste da solução, quando refletidos, então todo método com `[Fact]`/`[Theory]` tem um `Trait` "Spec" no formato `SPEC-\d{4}:(CH|UT|IT|CT|E2E)-\d{2}` e um `Trait` "Category" permitido; caso contrário, falha listando os métodos.

- **UT-02** — Dado os arquivos de fixture `Fixtures/DesignTokens/Bad.razor` (com `#ff007a`, `bg-[#fff]` e `font-family`) e `Fixtures/DesignTokens/theme.css` (com cores), quando o teste de política varre os arquivos de UI da solução e as fixtures, então aponta as três violações do `Bad.razor` com arquivo e linha, não aponta o `theme.css`, e passa para os arquivos reais da solução.

### 7.3 Testes de Integração
- **IT-01** — Duas classes de teste com bancos da `PostgresFixture` gravam a mesma chave primária numa tabela de teste sem conflito, e as extensões `pg_trgm` e `unaccent` estão disponíveis.
- **IT-02** — Para um assembly de fixture `Juker.ArchitectureTests.Violations` (compilado só para teste) com uma violação de cada regra ARQ-01..ARQ-08 (incluindo um projeto de teste de exemplo que referencia EF Core InMemory), cada regra reporta a violação esperada; e, para os assemblies reais, todas passam.
- **IT-03** — `JukerApiFactory` com o banco da classe sobe o host real: `/health/ready` = 200; o `TimeProvider` resolvido é o `FakeTimeProvider` exposto pela factory; a configuração `Outbox:DispatcherEnabled` é falsa.
- **IT-04** — Com a `PostgresFixture`, duas chamadas a `CreateDatabaseAsync` criam bancos distintos clonados do `juker_template`, cada um com as tabelas das migrations reais, em menos de 1 s cada.
- **IT-05** — Um `DbContext` de fixture com uma propriedade nova sem migration faz o `MigrationsGuard` falhar citando o contexto; para os `DbContext` reais da solução, passa.

### 7.4 Testes de Contrato
- **CT-01** — Executar `dotnet test --filter Category=Integration --list-tests` retorna somente testes marcados como Integration, e a API pública do `Juker.TestKit` bate com o snapshot aprovado (contrato consumido pelas SPECs 0004, 0005, 0025 e por todas as specs com IT).

### 7.5 Testes E2E
- N/A — user_facing: false.

### 7.6 Outros
- Acessibilidade: o helper axe-core é validado dentro do IT-03 (a página de teste não tem violações).

**Dublês e dados de teste:** assembly `Violations` apenas para o IT-02; `DbContext` de fixture para o IT-05; nenhum dublê de banco.

**Ambiente de execução:** local com Docker; CI no runner Ubuntu com Docker (SPEC-0005).

## 8. Plano de Rollout
- **Estratégia:** deploy direto (código de teste).
- **Dados/schema:** N/A.
- **Compatibilidade:** N/A.
- **Observabilidade:** N/A — somente testes.
- **Rollback:** reverter o commit.
- **Etapas de migração/coexistência:** N/A.

## 9. Questões em Aberto
Nenhuma.

## 10. Aprovação (H1)
Registrada no frontmatter (`approved_by`, `approved_at`) somente depois que o humano responder "Aprovado". O arquiteto nunca aprova a própria spec.

## 11. Checklist de Implementação

**Fase 1: Regras e componentes (unitário)**
- [ ] Red: escrever UT-01, UT-02 com a tag `SPEC-0003:<ID>` e confirmar que falham pelo motivo certo
- [ ] Green: implementar o mínimo para passar, seguindo os ADRs de fundação e o padrão de slices
- [ ] Refactor mantendo tudo verde
- [ ] Validar: build + lint + suíte de UT (G2)

**Fase 2: Integração e contrato**
- [ ] Red: escrever IT-01, IT-02, IT-03, IT-04, IT-05, CT-01 com a tag `SPEC-0003:<ID>` (Postgres real, esquemas de auth reais, `DrainOutboxAsync`, WireMock — ADR-0012) e confirmar que falham pelo motivo certo
- [ ] Green: implementar endpoints, persistência, migrations e integrações até passar
- [ ] Refactor mantendo tudo verde
- [ ] Validar: build + suíte completa + arquitetura + cobertura do diff ≥ 80% (G2/G3)

**Fase 3: Verificações adicionais (seção 7.6)**
- [ ] Executar as verificações da seção 7.6 e anexar a evidência ao Relatório de Entrega

**Fase final: Integração, entrega e documentação**
- [ ] Review independente (G4)
- [ ] Integração + CI verde (G5) e aprovação (H2)
- [ ] Deploy via pipeline com smoke/E2E no ambiente (G6)
- [ ] Relatório de Entrega, docs raiz e CHANGELOG (G7)

## 12. Registro de Gates
| Gate | Status | Evidência | Data |
|---|---|---|---|
| G0 Spec | PASS | `spec_graph.py validate SPEC-0003` → G0 [SPEC-0003]: PASS — 0 erro(s), 0 aviso(s); checklist de julgamento do G0 revisado pelo Architect (revisão ADR-0014) | 2026-09-30 |
| G1 Red | PENDING | | |
| G2 Green | PENDING | | |
| G3 Arquitetura | PENDING | | |
| G4 Review | PENDING | | |
| G5 Integração & CI | PENDING | | |
| H2 Integração aprovada | PENDING | | |
| G6 Deploy | PENDING | | |
| G7 Pronto & Docs | PENDING | | |

## 13. Registro de Impedimentos
| ID | Aberto em | Fase/Gate | Tipo | Descrição | Tentativas | Responsável | Resolução | Fechado em |
|---|---|---|---|---|---|---|---|---|

## 14. Relatório de Entrega

### O que foi entregue

### Como foi feito

### Prova de Correção

### Verificação
| Teste | Comportamento | Resultado | Evidência |
|---|---|---|---|

### Definição de Pronto
- [ ] Todos os testes do plano passando e listados na Verificação
- [ ] Todo comportamento do Mapa de Comportamentos coberto e verificado
- [ ] Suíte completa, arquitetura e CI verdes no resultado integrado (G5)
- [ ] Review independente sem achados blocker/major (G4)
- [ ] Padrão arquitetural existente mantido, ou desvio coberto por ADR aprovado
- [ ] Requisitos não-funcionais medidos com evidência (ou N/A justificado)
- [ ] Disponível no ambiente-alvo via pipeline, com smoke/E2E passando no ambiente (G6)
- [ ] Observabilidade e rollback prontos conforme o Plano de Rollout
- [ ] Documentação raiz e CHANGELOG atualizados (G7)
- [ ] Pendências registradas como novas specs (ou nenhuma)
- [ ] Textos da interface em português do Brasil, sem conceitos fora do produto (contas, créditos, mesa, música ao vivo)
- [ ] Tokens visuais vindos do design system do Stitch (projeto 8904741919458823023), sem cores/fontes avulsas

### Deploy

### Pendências

## 15. Emendas
| Versão do contrato | Data | Mudança | Motivo | Specs impactadas | Aprovado por |
|---|---|---|---|---|---|
