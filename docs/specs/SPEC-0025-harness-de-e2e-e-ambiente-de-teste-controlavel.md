---
id: SPEC-0025
title: Harness de E2E e ambiente de teste controlável
tier: full
type: foundation
user_facing: false
status: approved
created: 2026-09-30
parent: SPEC-0001
depends_on: [SPEC-0003]
consumes_contract: []
contract_version: 2
touches: [tests/Juker.E2ETests/**, tests/Juker.TestKit/E2E/**, tests/Juker.IntegrationTests/Testing/**, src/Juker.Host/Testing/**, Directory.Packages.props, Juker.slnx, docs/testing.md]
adrs: [ADR-0003, ADR-0011, ADR-0013]
external: []
size: M
approved_by: thomas
approved_at: 2026-09-30
---

# SPEC-0025 — Harness de E2E e ambiente de teste controlável

## 1. Visão Geral
Implementa a estratégia do ADR-0013: o app sobe inteiro pelo `Aspire.Hosting.Testing` no ambiente `E2E`, o Playwright dirige navegadores reais (Chromium e WebKit), o servidor tem um relógio controlável e uma API `/_test/*` para montar pré-condições (existentes **só** no ambiente `E2E`), os testes usam page objects e deixam trace, vídeo e screenshot quando falham, e toda tela passa pela verificação de acessibilidade axe-core.

## 2. Motivação & Escopo
**Motivação:** as jornadas do Juker dependem de tempo (PIN, virada da noite, TV offline), de vários aparelhos ao mesmo tempo e de iPhones; sem esse harness, os E2E seriam lentos, instáveis ou dependeriam de rede e relógio real.

**Objetivos (dentro do escopo):**
- Projeto `tests/Juker.E2ETests` e `tests/Juker.TestKit/E2E`.
- `AspireAppFixture`: sobe o `Juker.AppHost` com `ASPNETCORE_ENVIRONMENT=E2E` (Postgres em container do próprio AppHost), uma vez por execução.
- `PlaywrightFixture`: Chromium e WebKit; `NewGuestPageAsync(Chromium|WebKit)` (390×844, pt-BR, fuso America/Sao_Paulo), `NewTvPageAsync()` (1920×1080), `NewAdminPageAsync(mobile)` (1280×800 ou 390×844); relógio do navegador via `page.Clock`; em falha, `trace.zip`, vídeo e screenshot em `TestResults/playwright/<teste>/`; `AssertNoA11yViolationsAsync` (axe-core, A/AA).
- `PageObject` base e a convenção de pastas `tests/Juker.E2ETests/Pages/<Área>/`.
- Host (`src/Juker.Host/Testing`), registrado por um único método `AddE2ETestingSupport()` chamado só quando o ambiente é `E2E`:
  - `ControllableTimeProvider` no lugar do `TimeProvider` do sistema;
  - `POST /_test/clock/advance` e `POST /_test/venues` (bar + gerente + configurações, slug `e2e-<guid>`);
  - ponto de extensão `MapTestEndpoints(IEndpointRouteBuilder)` para as specs seguintes acrescentarem atalhos de pré-condição (ex.: sessão de comanda, token de TV, avançar a reprodução).
- `TestControlClient` no TestKit para chamar a API de teste.
- Categoria `Smoke` e o parâmetro `JUKER_E2E_BASE_URL`: com ele definido, os testes `Smoke` rodam contra um ambiente implantado (sem AppHost e sem `/_test/*`).
- Preenche `test_e2e` e `smoke_test` no `sdd-config.yml` e a seção de E2E do `docs/testing.md` (seletores por papel/rótulo, sem `sleep`, sem retry, jornada pela interface e pré-condições pela API de teste).

**Não-objetivos (fora do escopo):**
- Jornadas de funcionalidades (cada spec traz as suas).
- Execução no CI (SPEC-0005).
- `FakeMediaPlayer` e o bar `juker-smoke` (SPEC-0007).
- Regressão visual automática por screenshot (fora do MVP, ADR-0013).

## 3. Dependências
- **Implementações necessárias:** SPEC-0003 — convenções de tag/categoria, `Juker.TestKit` e o teste de convenções (UT-01 de lá).
- **Contratos consumidos:** N/A.
- **Pré-requisitos externos:** Docker; navegadores do Playwright (`playwright install --with-deps chromium webkit`).

## 4. Decisão Arquitetural
**Contexto:** projeto novo; ADR-0013 (estratégia de E2E), ADR-0011 (Playwright .NET) e ADR-0003 (áreas `/b`, `/tv`, `/admin`).

**Decisão:** o suporte a E2E no host fica isolado em `src/Juker.Host/Testing` e entra por um único ponto condicional ao ambiente; o TestKit de E2E encapsula AppHost, navegadores e API de controle.

**Justificativa:** um ponto único de registro torna simples provar (IT-02) que nada de teste vaza para staging ou produção.

**Desvio do padrão existente:** Nenhum.

**Alternativas descartadas:** Testcontainers separados para o E2E e só Chromium (ADR-0013); `#if DEBUG` para esconder a API de teste (o binário de produção seria diferente do testado).

**ADRs:** ADR-0003, ADR-0011, ADR-0013.

## 5. Requisitos Não-Funcionais
- **Desempenho e escala:** subida do AppHost no E2E em menos de 60 s com as imagens em cache; testes E2E em paralelo por classe — medido no IT-01.
- **Segurança:** `/_test/*` e o `ControllableTimeProvider` não existem em `Staging` nem em `Production` — IT-02.
- **Privacidade e dados pessoais:** N/A — dados sintéticos.
- **Disponibilidade e resiliência:** sem `Thread.Sleep`/`Task.Delay` e sem retry de teste; esperas só por condição — verificado no G4 e pelo UT-01.
- **Acessibilidade (UI):** o helper axe-core é validado no IT-01 (página de teste sem violações) e fica obrigatório em toda tela nova.
- **Custo:** N/A.

## 6. Artefato A — Contrato
**Interface:** API do TestKit de E2E, API de teste do host e convenções.

```text
namespace Juker.TestKit.E2E
  sealed class AspireAppFixture : IAsyncLifetime   // AppHost com ASPNETCORE_ENVIRONMENT=E2E
    Uri HostUrl ; TestControlClient Control
  sealed class TestControlClient
    Task AdvanceClockAsync(TimeSpan by)                        // POST /_test/clock/advance
    Task<TestVenue> CreateVenueAsync(VenueSetup? setup = null)  // POST /_test/venues
  enum BrowserKind { Chromium, WebKit }
  sealed class PlaywrightFixture : IAsyncLifetime
    Task<IPage> NewGuestPageAsync(BrowserKind kind)   // 390x844, pt-BR, America/Sao_Paulo
    Task<IPage> NewTvPageAsync()                      // Chromium, 1920x1080
    Task<IPage> NewAdminPageAsync(bool mobile)        // Chromium, 1280x800 | 390x844
    Task AssertNoA11yViolationsAsync(IPage page)      // axe-core A/AA
    // em falha: TestResults/playwright/<teste>/{trace.zip, video.webm, screenshot.png}
  abstract class PageObject(IPage page)

Host — somente ASPNETCORE_ENVIRONMENT=E2E (AddE2ETestingSupport / MapTestEndpoints):
  POST /_test/clock/advance { "seconds": 900 }            → 204
  POST /_test/venues { "slug"?, "name"?, "settings"? }    → 201 { venueId, slug, managerEmail, managerPassword }
  Staging/Production: /_test/* → 404 ; TimeProvider = TimeProvider.System

Smoke: JUKER_E2E_BASE_URL=https://staging.<domínio> → roda só Category=Smoke contra a URL, sem AppHost
Comandos (sdd-config.yml):
  test_e2e:   dotnet test --project tests/Juker.E2ETests -c Release
  smoke_test: dotnet test --project tests/Juker.E2ETests -c Release --filter-trait "Category=Smoke"
```

**Arquivos/módulos afetados:** ver `touches` no frontmatter.

### 6.1 Mapa de Comportamentos
| Cenário | Condição / Entrada | Resultado esperado | Testes |
|---|---|---|---|
| Sem espera fixa | teste E2E que usa `Thread.Sleep`/`Task.Delay` | teste de convenção falha listando o método | UT-01 |
| App sobe no ambiente E2E | `AspireAppFixture` | host e Postgres de pé; `/health/ready` = 200 em menos de 60 s | IT-01 |
| Dois navegadores | página de teste aberta em Chromium e WebKit | carrega nos dois, sem violações axe | IT-01 |
| Diagnóstico de falha | teste de exemplo que falha de propósito (executado isolado) | `trace.zip`, vídeo e screenshot gravados | IT-03 |
| Relógio do servidor | `AdvanceClockAsync(15 min)` | `TimeProvider` do host 15 min à frente | IT-02 |
| Bar de teste | `CreateVenueAsync()` | bar `e2e-…` com gerente e configurações; dois testes criam bares distintos | IT-02 |
| Nada de teste fora do E2E | host em `Staging` e `Production` | `/_test/*` → 404 e `TimeProvider.System` registrado | IT-02 |
| Smoke contra ambiente | `JUKER_E2E_BASE_URL` definido | só os `Smoke` rodam, sem subir o AppHost e sem chamar `/_test/*` | CT-01 |

## 7. Artefato B — Plano de Testes (TDD)

### 7.1 Testes de Caracterização
- N/A — projeto novo.

### 7.2 Testes Unitários
- **UT-01** — Dado o assembly `Juker.E2ETests`, quando analisado (reflexão + leitura do IL das chamadas), então nenhum método de teste chama `Thread.Sleep` ou `Task.Delay`; caso contrário, falha listando os métodos.

### 7.3 Testes de Integração
- **IT-01** — `AspireAppFixture` sobe o AppHost no ambiente `E2E` e `/health/ready` responde 200 em menos de 60 s; a `PlaywrightFixture` abre uma página de teste (`/_test/ping`, HTML mínimo) em Chromium e WebKit e o `AssertNoA11yViolationsAsync` não encontra violações.
- **IT-02** — Com o host em `E2E`: `POST /_test/clock/advance` (900 s) avança o `TimeProvider` do host em 15 min; duas chamadas a `POST /_test/venues` criam bares distintos com slug `e2e-…`. Com o host em `Staging` e em `Production` (WebApplicationFactory com o ambiente trocado): `/_test/*` responde 404 e o `TimeProvider` resolvido é `TimeProvider.System`.
- **IT-03** — Um teste de exemplo marcado para falhar (executado isolado pelo IT via `dotnet test --project … --filter-trait`) deixa `trace.zip`, `video.webm` e `screenshot.png` em `TestResults/playwright/<teste>/`.

### 7.4 Testes de Contrato
- **CT-01** — Com `JUKER_E2E_BASE_URL` apontando para um servidor local de teste, `dotnet test --project tests/Juker.E2ETests --filter-trait "Category=Smoke" --list-tests` lista só os Smoke e a execução não sobe o AppHost nem chama `/_test/*`; e a API pública do `Juker.TestKit.E2E` bate com o snapshot aprovado (consumida pela SPEC-0005 e por todas as specs com E2E).

### 7.5 Testes E2E
- N/A — user_facing: false (o harness é validado pelos IT; a primeira jornada real é a da SPEC-0007).

### 7.6 Outros
- N/A.

**Dublês e dados de teste:** página `/_test/ping` só no ambiente `E2E`; teste de exemplo que falha de propósito, excluído da execução normal por categoria própria (`Category=HarnessSelfTest`, fora do `test_e2e`).

**Ambiente de execução:** local com Docker e navegadores; CI no runner Ubuntu (SPEC-0005).

## 8. Plano de Rollout
- **Estratégia:** deploy direto; o código de `src/Juker.Host/Testing` vai no binário, mas só é registrado no ambiente `E2E`.
- **Dados/schema:** N/A.
- **Compatibilidade:** novos atalhos de pré-condição são acrescentados pelas specs via `MapTestEndpoints`, sempre sob o mesmo registro condicional.
- **Observabilidade:** N/A.
- **Rollback:** reverter o commit.
- **Etapas de migração/coexistência:** N/A.

## 9. Questões em Aberto
- [x] Navegadores do E2E — Chromium e WebKit para o cliente; Chromium para TV e painel (thomas, 2026-09-30)
- [x] Regressão visual automática — fora do MVP; revisão manual contra o Stitch (thomas, 2026-09-30)

## 10. Aprovação (H1)
Registrada no frontmatter (`approved_by`, `approved_at`) somente depois que o humano responder "Aprovado". O arquiteto nunca aprova a própria spec.

## 11. Checklist de Implementação

**Fase 1: Regras e componentes (unitário)**
- [ ] Red: escrever UT-01 com a tag `SPEC-0025:<ID>` e confirmar que falham pelo motivo certo
- [ ] Green: implementar o mínimo para passar, seguindo os ADRs de fundação e o padrão de slices
- [ ] Refactor mantendo tudo verde
- [ ] Validar: build + lint + suíte de UT (G2)

**Fase 2: Integração e contrato**
- [ ] Red: escrever IT-01, IT-02, IT-03, CT-01 com a tag `SPEC-0025:<ID>` (Postgres real, esquemas de auth reais, `DrainOutboxAsync`, WireMock — ADR-0012) e confirmar que falham pelo motivo certo
- [ ] Green: implementar endpoints, persistência, migrations e integrações até passar
- [ ] Refactor mantendo tudo verde
- [ ] Validar: build + suíte completa + arquitetura + cobertura do diff ≥ 80% (G2/G3)

**Fase final: Integração, entrega e documentação**
- [ ] Review independente (G4)
- [ ] Integração + CI verde (G5) e aprovação (H2)
- [ ] Deploy via pipeline com smoke/E2E no ambiente (G6)
- [ ] Relatório de Entrega, docs raiz e CHANGELOG (G7)

## 12. Registro de Gates
| Gate | Status | Evidência | Data |
|---|---|---|---|
| G0 Spec | PASS | `spec_graph.py validate SPEC-0025` → G0 [SPEC-0025]: PASS — 0 erro(s), 0 aviso(s); checklist de julgamento do G0 revisado pelo Architect (revisão ADR-0012/0013) | 2026-09-30 |
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
| 2 | 2026-09-30 | Comandos de teste migrados para o Microsoft.Testing.Platform (xUnit v3 no .NET 10): `dotnet test --solution/--project … --filter-trait "Category=…"` e `--list-tests`, no lugar da sintaxe VSTest (`dotnet test <sln> --filter Category=…`) | O xUnit v3 4.x roda no MTP e o SDK do .NET 10 recusa a sintaxe VSTest (constatado na SPEC-0002; `global.json` com `test.runner = Microsoft.Testing.Platform`) | SPEC-0005 (consome SPEC-0025@1 → revisar e fixar @2) | thomas, 2026-09-30 |
