---
id: SPEC-0025
title: Harness de E2E e ambiente de teste controlável
tier: full
type: foundation
user_facing: false
status: in-progress
created: 2026-09-30
parent: SPEC-0001
depends_on: [SPEC-0003]
consumes_contract: []
contract_version: 3
touches: [tests/Juker.E2ETests/**, tests/Juker.TestKit.E2E/**, tests/Juker.ArchitectureTests/Juker.ArchitectureTests.csproj, src/Juker.Host/Program.cs, src/Juker.Host/Juker.Host.csproj, tests/Juker.IntegrationTests/Testing/**, src/Juker.Host/Testing/**, tests/fixtures/E2EHarnessSelfTest/**, Directory.Packages.props, Juker.slnx, docs/testing.md]
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
- Projetos `tests/Juker.E2ETests` e `tests/Juker.TestKit.E2E` (assembly próprio, namespace `Juker.TestKit.E2E`, para não alterar a API pública congelada do `Juker.TestKit` da SPEC-0003); ambos referenciados pelo `Juker.ArchitectureTests.csproj` com `ReferenceOutputAssembly=false` (convenção da SPEC-0003).
- `AspireAppFixture`: sobe o `Juker.AppHost` com `ASPNETCORE_ENVIRONMENT=E2E` (Postgres em container do próprio AppHost), uma vez por execução.
- `PlaywrightFixture`: Chromium e WebKit; `NewGuestPageAsync(Chromium|WebKit)` (390×844, pt-BR, fuso America/Sao_Paulo), `NewTvPageAsync()` (1920×1080), `NewAdminPageAsync(mobile)` (1280×800 ou 390×844); relógio do navegador via `page.Clock`; em falha, `trace.zip`, vídeo e screenshot em `TestResults/playwright/<teste>/`; `AssertNoA11yViolationsAsync` (axe-core, A/AA).
- `PageObject` base e a convenção de pastas `tests/Juker.E2ETests/Pages/<Área>/`.
- Host (`src/Juker.Host/Testing`), registrado por um único método `AddE2ETestingSupport()` chamado só quando o ambiente é `E2E`:
  - `ControllableTimeProvider` no lugar do `TimeProvider` do sistema;
  - `POST /_test/clock/advance` e a página `/_test/ping`; `POST /_test/venues` fica no contrato do cliente (`CreateVenueAsync`), mas o endpoint do host é implementado pela SPEC-0007, que cria a entidade `Venue` (emenda v3);
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
    Task<TestVenue> CreateVenueAsync(VenueSetup? setup = null)  // POST /_test/venues (endpoint do host: SPEC-0007)
  sealed record VenueSetup(string? Slug = null, string? Name = null)
  sealed record TestVenue(Guid VenueId, string Slug)          // gerente e configurações: emendas das SPECs 0009/0011
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
  GET  /_test/ping                                       → 200 HTML mínimo (página de teste do IT-01)
  POST /_test/venues { "slug"?, "name"? }                 → 201 { venueId, slug }   (implementado pela SPEC-0007)
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
| Nada de teste fora do E2E | host em `Staging` e `Production` | `/_test/*` → 404 e `TimeProvider.System` registrado | IT-02 |
| Smoke contra ambiente | `JUKER_E2E_BASE_URL` definido | só os `Smoke` rodam, sem subir o AppHost e sem chamar `/_test/*` | CT-01 |

## 7. Artefato B — Plano de Testes (TDD)

### 7.1 Testes de Caracterização
- N/A — projeto novo.

### 7.2 Testes Unitários
- **UT-01** — Dado o assembly `Juker.E2ETests`, quando analisado (reflexão + leitura do IL das chamadas), então nenhum método de teste chama `Thread.Sleep` ou `Task.Delay`; caso contrário, falha listando os métodos.

### 7.3 Testes de Integração
- **IT-01** — `AspireAppFixture` sobe o AppHost no ambiente `E2E` e `/health/ready` responde 200 em menos de 60 s; a `PlaywrightFixture` abre uma página de teste (`/_test/ping`, HTML mínimo) em Chromium e WebKit e o `AssertNoA11yViolationsAsync` não encontra violações.
- **IT-02** — Com o host em `E2E`: `POST /_test/clock/advance` (900 s) avança o `TimeProvider` do host em 15 min; Com o host em `Staging` e em `Production` (WebApplicationFactory com o ambiente trocado): `/_test/*` responde 404 e o `TimeProvider` resolvido é `TimeProvider.System`.
- **IT-03** — Um teste de exemplo que falha de propósito, num projeto de fixture fora da solução (`tests/fixtures/E2EHarnessSelfTest`, que usa a `PlaywrightFixture`), executado pelo IT via `dotnet test --project …` aninhado (o IT fica em `tests/Juker.IntegrationTests/Testing` na coleção serializada `NestedTestRun` da SPEC-0027), deixa `trace.zip`, `video.webm` e `screenshot.png` em `TestResults/playwright/<teste>/`.

### 7.4 Testes de Contrato
- **CT-01** — Com `JUKER_E2E_BASE_URL` apontando para um servidor local de teste, `dotnet test --project tests/Juker.E2ETests --filter-trait "Category=Smoke" --list-tests` lista só os Smoke e a execução não sobe o AppHost nem chama `/_test/*`; e a API pública do `Juker.TestKit.E2E` bate com o snapshot aprovado (consumida pela SPEC-0005 e por todas as specs com E2E).

### 7.5 Testes E2E
- N/A — user_facing: false (o harness é validado pelos IT; a primeira jornada real é a da SPEC-0007).

### 7.6 Outros
- N/A.

**Dublês e dados de teste:** página `/_test/ping` só no ambiente `E2E`; teste de exemplo que falha de propósito no projeto de fixture `tests/fixtures/E2EHarnessSelfTest`, fora da solução, então nunca roda no `test`, no `test_e2e` nem no CI. O IT-01 roda no projeto `Juker.E2ETests` com `Category=E2E` (precisa do AppHost e dos navegadores).

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
| G1 Red | PASS | verify G1: PASS; `dotnet test --solution Juker.slnx -c Release` exit 2 (red: Test run completed with non-success exit code: 2 (see: https://aka.ms/testingplatform/exit) — bd03e75 | 2026-10-13 |
| G2 Green | PASS | build exit 0 (Time Elapsed 00:00:19.50); test exit 0 (duration: 2m 25s 528ms); lint exit 0 () — 3476a61 | 2026-10-13 |
| G3 Arquitetura | PASS | arch_test exit 0 (duration: 3s 006ms) — 3476a61 | 2026-10-13 |
| G4 Review | PASS | verify G1+G4: PASS; revisão: Reviewer independente: APPROVED em 3476a61 (sem blocker/major; 5 minors viram a SPEC-0029) — 3476a61 | 2026-10-13 |
| G5 Integração & CI | PENDING | | |
| H2 Integração aprovada | PENDING | | |
| G6 Deploy | PENDING | | |
| G7 Pronto & Docs | PENDING | | |

## 13. Registro de Impedimentos
| ID | Aberto em | Fase/Gate | Tipo | Descrição | Tentativas | Responsável | Resolução | Fechado em |
|---|---|---|---|---|---|---|---|---|
| IMP-01 | 2026-10-13 | G2 | externo | Os navegadores do Playwright 1.63.0 (Chromium e WebKit) não abrem neste Ubuntu 22.04 por falta de bibliotecas do sistema (libatk, libgbm, libxkbcommon, libasound, dependências do WebKit); instalar exige sudo. Bloqueia o verde do IT-01 e do IT-03 | Test-writer baixou Chromium v1243 e WebKit v2359 sem sudo; o vermelho atual vem de NotImplementedException, não dos navegadores | thomas (rodar sudo npx -y playwright@1.63.0 install-deps chromium webkit) | thomas instalou as dependências de sistema (sudo npx playwright@1.63.0 install-deps chromium webkit) em 2026-10-13; ldd dos binários do Chromium 1243/1248 sem bibliotecas faltando e as do WebKit 2359 vêm empacotadas | 2026-10-13 |

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
| 3 | 2026-10-13 | `POST /_test/venues` sai desta spec: o cliente `CreateVenueAsync` continua no contrato com `VenueSetup(Slug?, Name?)` e `TestVenue(VenueId, Slug)`, e o endpoint do host passa à SPEC-0007; gerente e configurações saem do retorno (entram por emenda das SPECs 0009 e 0011 quando precisarem); `/_test/ping` no contrato; o teste que falha de propósito vai para um projeto de fixture fora da solução (`tests/fixtures/E2EHarnessSelfTest`) e o IT-03 entra na coleção serializada `NestedTestRun`; IT-01 com `Category=E2E` | Revisão do Architect antes do despacho: não existe entidade `Venue` (SPEC-0007), nem gerente (SPEC-0009), nem configurações (SPEC-0011); os tipos `TestVenue`/`VenueSetup` não tinham forma para o snapshot do CT-01; a categoria `HarnessSelfTest` não excluía o teste do `test_e2e` nem da suíte completa; o IT-03 repete o `dotnet test` aninhado corrigido na SPEC-0027 | SPEC-0005 (fixar @3), SPEC-0007 (implementa o endpoint, emenda própria) | thomas — no chat em 2026-10-13 ("Aprovado") |
| escopo | 2026-10-01 | Tipos de E2E em assembly próprio `Juker.TestKit.E2E` (antes: pasta dentro do Juker.TestKit); `touches` inclui o `Program.cs`/`.csproj` do host (registro condicional) e o `.csproj` da suíte de arquitetura | O snapshot público do Juker.TestKit está congelado (SPEC-0003 CT-01); o host precisa chamar `AddE2ETestingSupport()` | Nenhuma — a API do contrato (namespace e tipos) não muda | thomas — autorização prévia no chat (2026-09-30), confirmada por thomas no chat em 2026-10-13 |
| 2 | 2026-09-30 | Comandos de teste migrados para o Microsoft.Testing.Platform (xUnit v3 no .NET 10): `dotnet test --solution/--project … --filter-trait "Category=…"` e `--list-tests`, no lugar da sintaxe VSTest (`dotnet test <sln> --filter Category=…`) | O xUnit v3 4.x roda no MTP e o SDK do .NET 10 recusa a sintaxe VSTest (constatado na SPEC-0002; `global.json` com `test.runner = Microsoft.Testing.Platform`) | SPEC-0005 (consome SPEC-0025@1 → revisar e fixar @2) | thomas, 2026-09-30 |
