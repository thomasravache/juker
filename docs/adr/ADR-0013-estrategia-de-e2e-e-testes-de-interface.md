---
id: ADR-0013
title: Estratégia de E2E e testes de interface — Aspire.Hosting.Testing + Playwright (Chromium e WebKit), ambiente E2E controlável e bUnit para componentes
status: accepted
origin: decision
date: 2026-09-30
decision_makers: [thomas]
consulted: [claude]
informed: []
supersedes:
superseded_by:
enforced_by: SPEC-0003:IT-03, SPEC-0003:IT-06, SPEC-0003:UT-01 e SPEC-0005:IT-05
---

# ADR-0013 — Estratégia de E2E e testes de interface

## Contexto e Problema
Todas as specs com tela têm E2E, e as jornadas do Juker dependem de tempo (PIN a cada 15 min, virada da noite, TV offline após 30 s, avisos da TV a cada 20 s), de um provedor externo (YouTube) e de vários aparelhos ao mesmo tempo (celulares, TV, painel). Sem uma estratégia única, os E2E seriam lentos, instáveis ou dependeriam de rede e de relógio real.

## Direcionadores da Decisão
- O E2E roda contra o app real, com a mesma topologia de produção.
- Zero dependência de rede externa e de relógio real.
- Clientes em iPhone (Safari) são uma parte grande do público.
- Testes estáveis e com diagnóstico fácil quando falham.
- Um dev sozinho: poucos E2E, cada um valendo muito.

## Opções Consideradas
- **A.** App iniciado pelo `Aspire.Hosting.Testing` no ambiente `E2E` + Playwright .NET (Chromium e WebKit) + API de controle de teste só no ambiente `E2E` + bUnit para componentes
- **B.** App iniciado com Testcontainers separados (Postgres + imagem do host) + Playwright só em Chromium
- **C.** E2E somente contra o staging, sem ambiente local

## Resultado da Decisão
**Opção escolhida:** "A", porque o AppHost já sobe o host e o Postgres exatamente como em produção (Testcontainers por fora duplicaria a configuração) e o ambiente `E2E` permite controlar tempo e dados sem nenhum atalho em produção.

Regras:
1. **Onde roda:** local e no CI, pelo `AspireAppFixture` (Aspire.Hosting.Testing) com `ASPNETCORE_ENVIRONMENT=E2E`. Após cada deploy, um subconjunto `Category=Smoke` roda contra o staging e a produção (G6).
2. **Navegadores:** jornadas do **cliente** em **Chromium e WebKit** (viewport 390×844, pt-BR); **TV** (1920×1080) e **painel** (1280×800 e 390×844) em Chromium.
3. **Tempo:** no servidor, um `ControllableTimeProvider` registrado **só** no ambiente `E2E`, avançado por `POST /_test/clock/advance`. No navegador, a API de relógio do Playwright (`page.Clock`) controla os timers do cliente (debounce, rodízio de avisos, banner de reconexão).
4. **API de controle de teste** (`/_test/*`, só no ambiente `E2E`): relógio, criação de bar/gerente/configurações, emissão de credenciais para **pré-condições** (ex.: sessão de uma comanda, token de TV) e ações de apoio (ex.: pedir música por outra comanda, avançar a reprodução). A **jornada em teste** é sempre feita pela interface. Um teste garante que `/_test/*` responde 404 em `Production` e `Staging`.
5. **Dados:** cada teste cria o próprio bar com slug único (`e2e-<guid>`). O multi-tenant isola tudo; nada é limpo entre testes, e os testes rodam em paralelo.
6. **Dublês nas bordas:** YouTube → WireMock.Net (respostas gravadas, ADR-0012); player → `FakeMediaPlayer` com vídeos de 3 s. Nenhum E2E acessa a internet. O YouTube real e a TV real só nos testes manuais descritos nas specs (seção 7.6).
7. **Seletores:** papéis e nomes acessíveis (`GetByRole`, `GetByLabel`, `GetByText`) primeiro; `data-testid` só quando não houver alternativa semântica. Isso reforça a acessibilidade.
8. **Estrutura:** **page objects** por tela (`EntryPage`, `SearchPage`, `QueuePage`, `MinePage`, `TvScreen`, `AdminDashboard`, …) em `tests/Juker.E2ETests/Pages`; um teste descreve a jornada, não os cliques.
9. **Estabilidade:** proibido `Thread.Sleep`/`Task.Delay` e retry automático de teste; esperas só por condição (auto-wait/`Expect`). Teste instável é defeito e ganha uma spec `lite`.
10. **Diagnóstico:** trace, vídeo e screenshot do Playwright gravados em `TestResults/playwright/` quando o teste falha, e publicados como artefato no CI.
11. **Acessibilidade:** `AssertNoA11yViolationsAsync` (axe-core, A/AA) em toda tela nova dentro do E2E.
12. **Smoke em produção:** um bar fixo `juker-smoke` com `JukeboxEnabled=true` em staging e produção, sem QR público. As credenciais ficam nos segredos do environment. O smoke só lê e cria dados desse bar.
13. **Testes de interface:** **bUnit** para componentes (estados, textos, acessibilidade básica, formatação). É o grosso dos testes de UI, com `Category=Unit`. **Regressão visual automática por screenshot fica fora do MVP**; a revisão visual contra o Stitch é manual e documentada no Relatório de Entrega de cada spec de tela.

### Consequências
- **Boa**, porque os E2E são rápidos, paralelos e independentes de rede e relógio, e cobrem o Safari do iPhone via WebKit.
- **Boa**, porque a API de controle atalha só as pré-condições; a jornada real continua passando pela interface.
- **Ruim**, porque a API `/_test/*` é código a mais; mitigado pelo teste que garante 404 fora do ambiente `E2E` e pelo registro condicional num único ponto.
- **Ruim**, porque o WebKit do Playwright aproxima, mas não é idêntico ao Safari do iOS; a verificação num iPhone real fica nos testes manuais das telas do cliente.

### Confirmação (G3)
- SPEC-0003:UT-01 (convenções de tag e categoria nos testes).
- SPEC-0003:IT-03 (fixture Aspire + Playwright em Chromium e WebKit, com o trace gravado na falha).
- SPEC-0003:IT-06 (`/_test/*` disponível em `E2E` e 404 em `Staging`/`Production`).
- SPEC-0005:IT-05 (o CI publica os traces quando um E2E falha).

## Prós e Contras das Opções
| Critério (peso) | A. Aspire + Playwright + ambiente E2E | B. Testcontainers + Chromium | C. Só staging |
|---|---|---|---|
| Paridade com produção (5) | 5 | 4 | 5 |
| Determinismo (tempo e rede) (5) | 5 | 3 | 1 |
| Cobertura do Safari/iPhone (4) | 4 | 1 | 2 |
| Velocidade e feedback local (4) | 4 | 4 | 1 |
| Configuração única (3) | 5 | 2 | 4 |
| **Total ponderado** | **99** | **63** | **51** |

## Mais Informações
- Aspire 13.5 e `Aspire.Hosting.Testing` — https://devblogs.microsoft.com/aspire/whats-new-aspire-13-5/ (2026-09-30).
- Playwright .NET — navegadores suportados (Chromium, Firefox, WebKit) e a API de relógio (`Clock`) — documentação oficial playwright.dev/dotnet. A versão estável será fixada em `Directory.Packages.props` na SPEC-0003, verificada no momento da implementação.
- axe-core via `Deque.AxeCore.Playwright` (licença MPL-2.0) — a verificar na SPEC-0003.
- Relacionados: ADR-0003 (render modes), ADR-0011 (ferramentas), ADR-0012 (IT).
