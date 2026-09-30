---
id: SPEC-0020
title: Reprodução e controle do player
tier: full
type: feature
user_facing: true
status: approved
created: 2026-09-30
parent: SPEC-0010
depends_on: [SPEC-0007, SPEC-0008, SPEC-0009]
consumes_contract: [SPEC-0018@1, SPEC-0012@1, SPEC-0022@1]
contract_version: 1
touches: [src/Modules/Playback/**, src/Web/Juker.Web.Client/Tv/Playback/**, deploy/observability/alerts.yaml, tests/Juker.Modules.Playback.UnitTests/**, tests/Juker.IntegrationTests/Playback/**, tests/Juker.Web.UnitTests/Tv/Playback/**, tests/Juker.E2ETests/Tv/Playback/**]
adrs: [ADR-0005, ADR-0006, ADR-0007]
external: []
size: M
approved_by: thomas
approved_at: 2026-09-30
---

# SPEC-0020 — Reprodução e controle do player

## 1. Visão Geral
O módulo Playback comanda o que a TV toca: mantém o estado do player de cada bar (parado, tocando, pausado), puxa a próxima música da fila justa quando a atual termina, pula quando o vídeo falha, detecta a TV offline e atende o botão de pânico do gerente (**pular**, **pausar/retomar**). A TV só executa: carrega a mídia no `IMediaPlayer` (SPEC-0007), reporta início, progresso, fim e erro, e se ressincroniza ao reconectar.

## 2. Motivação & Escopo
**Motivação:** "é uma tela passiva que fica rodando sozinha" — a TV precisa tocar a noite toda sem ninguém encostar, e o gerente precisa conseguir intervir na hora.

**Objetivos (dentro do escopo):**
- Agregado `VenuePlayback` (VenueId, State ∈ {Idle, Playing, Paused}, CurrentRequestId, StartedAt, PositionSeconds, Version) e `TvPresence` (VenueId, DeviceId, LastHeartbeatAt).
- API da TV (`TvOfVenue`): estado atual, heartbeat a cada 5 s com a posição, `ended` e `failed` (com código) do pedido atual; relatos de um pedido que não é o atual → 409 `STALE_REQUEST` (ignorados).
- API do gerente (`ManagerOfVenue`): pular, pausar, retomar; estado atual.
- Público: `GET /api/v1/b/{slug}/now-playing` (usado pela entrada e pela fila).
- Avanço: fim/erro/pulo → `IQueueModule.FinishAsync` (`Played`/`Failed`/`Skipped`) → `StartNextAsync` → `NowPlayingChanged`; fila vazia → `Idle`; `RequestAdded` com o player `Idle` e a TV online → começa a tocar.
- TV offline: sem heartbeat por 30 s → `TvStatusChanged(offline)`, gauge `juker.tv.online = 0`; alerta "TV offline por mais de 2 min com fila não vazia" em `alerts.yaml`; ao voltar, a TV relê o estado e continua de onde parou (posição do último heartbeat).
- Consumidor de `ComandaBlocked` (SPEC-0022): se a música atual é da comanda bloqueada, pula (`Skipped`).
- Eventos (outbox): `PlaybackFailed` (mediaId, código) e `PlaybackFinished` (requestId, mediaId, motivo `played|skipped`).
- Controlador da TV (`TvPlaybackController`): escuta `NowPlayingChanged`/`PlaybackStateChanged`/`Resynced`, carrega a mídia com autoplay, envia heartbeats e relatos; na primeira carga, a tela "Toque para iniciar o Juker" libera o autoplay com som (política dos navegadores).

**Não-objetivos (fora do escopo):**
- Layout da TV (letreiro, a seguir, QR, avisos) e pareamento (SPEC-0021).
- Painel do gerente (SPEC-0023 usa esta API).
- Volume, mudo e crossfade.

## 3. Dependências
- **Implementações necessárias:** SPEC-0007 — `IMediaPlayer` (YouTube e Fake) e a página da TV; SPEC-0008 — `JukerMetrics` e `alerts.yaml`; SPEC-0009 — política `TvOfVenue` e emissor de teste.
- **Contratos consumidos:** SPEC-0018@1 (`IQueueModule`, `RequestAdded`); SPEC-0012@1 (`IVenueNotifier`, eventos do hub); SPEC-0022@1 (evento `ComandaBlocked`).
- **Pré-requisitos externos:** N/A.

## 4. Decisão Arquitetural
**Contexto:** projeto novo; ADR-0005 (servidor é a fonte da verdade; TV relê ao reconectar), ADR-0006 (eventos via outbox) e ADR-0007 (a TV conhece só o `MediaRef` e o `IMediaPlayer`).

**Decisão:** máquina de estados pura em `Domain/` (`PlaybackStateMachine`), slices por comando (`Skip`, `Pause`, `Resume`, `ReportEnded`, `ReportFailed`, `Heartbeat`) e um `BackgroundService` de presença; concorrência por versão otimista no `VenuePlayback`.

**Justificativa:** as transições ficam testáveis sem I/O, e a versão otimista resolve relatos duplicados (ex.: TV e gerente pulando ao mesmo tempo).

**Desvio do padrão existente:** Nenhum.

**Alternativas descartadas:** a TV decidir a próxima música (estado espalhado, frágil em queda de rede); cronômetro no servidor para adivinhar o fim do vídeo (dessincroniza com buffering e anúncios).

**ADRs:** ADR-0005, ADR-0006, ADR-0007.

## 5. Requisitos Não-Funcionais
- **Desempenho e escala:** do `ended` à TV receber a próxima mídia p95 < 1 s; pular/pausar refletido na TV em < 1 s — IT-02.
- **Segurança:** só a TV pareada relata; só o gerente pula/pausa; o `now-playing` público expõe título, artista, tipo, comanda e o tamanho da fila — IT-05.
- **Privacidade e dados pessoais:** N/A — sem dados pessoais além da comanda.
- **Disponibilidade e resiliência:** TV que reconecta retoma a mesma música (posição do último heartbeat, tolerância de 10 s); relatos duplicados/atrasados não avançam a fila duas vezes — IT-03 e IT-04.
- **Acessibilidade (UI):** a tela "Toque para iniciar o Juker" tem botão grande focável — UT-03.
- **Custo:** N/A — o IFrame não consome cota.

## 6. Artefato A — Contrato
**Interface:** API da TV, do gerente, pública; máquina de estados e eventos.

```text
TV (TvOfVenue):
  GET  /api/v1/tv/state → 200 { playbackVersion, state: "idle"|"playing"|"paused",
        current: { requestId, mediaRef:{provider, externalId}, title, artist, kind, comanda,
                   durationSeconds, positionSeconds } | null }
  POST /api/v1/tv/playback/heartbeat { requestId|null, positionSeconds, playerState } → 204
  POST /api/v1/tv/playback/{requestId}/ended                                    → 204 | 409 STALE_REQUEST
  POST /api/v1/tv/playback/{requestId}/failed { code: "unavailable"|"embed_not_allowed"|"unknown" } → 204 | 409
Gerente (ManagerOfVenue):
  GET  /api/v1/admin/playback           → 200 (mesmo corpo de /tv/state + tvOnline: bool)
  POST /api/v1/admin/playback/skip      → 204 | 409 NOTHING_PLAYING
  POST /api/v1/admin/playback/pause     → 204 | 409 NOT_PLAYING
  POST /api/v1/admin/playback/resume    → 204 | 409 NOT_PAUSED
Público (jukebox ligado):
  GET  /api/v1/b/{slug}/now-playing     → 200 { title, artist, kind, comanda, positionSeconds, durationSeconds, queueCount } | 204

Transições (PlaybackStateMachine):
  Idle   --RequestAdded & TV online--> Playing(next)       Idle --Start sem fila--> Idle
  Playing --ended--> Finish(Played) → next | Idle          Playing --failed--> Finish(Failed) → next | Idle
  Playing --skip--> Finish(Skipped) → next | Idle          Playing --pause--> Paused --resume--> Playing
  Paused --skip--> Finish(Skipped) → next | Idle           qualquer --ComandaBlocked(atual)--> skip
Eventos: PlaybackFailed(venue, requestId, mediaId, code) · PlaybackFinished(venue, requestId, mediaId, reason: played|skipped)
Hub: NowPlayingChanged, PlaybackStateChanged, TvStatusChanged (SPEC-0012)
Alerta: juker-tv-offline — juker.tv.online == 0 por 2 min e juker.queue.length > 0 → alto
```

**Arquivos/módulos afetados:** ver `touches` no frontmatter.

### 6.1 Mapa de Comportamentos
| Cenário | Condição / Entrada | Resultado esperado | Testes |
|---|---|---|---|
| Transições | cada evento em cada estado da tabela | estado e ação resultantes corretos; evento inválido → erro | UT-01 |
| Começa sozinho | player Idle, TV online, chega `RequestAdded` | Playing com o pedido; TV recebe `NowPlayingChanged` | IT-01, E2E-01 |
| Avanço no fim | `ended` do atual | atual `Played`, próximo começa em < 1 s | IT-02, E2E-01 |
| Vídeo com erro | `failed(unavailable)` | atual `Failed` (não conta como vez), `PlaybackFailed` publicado, próximo começa | IT-02 |
| Pular | gerente pula | atual `Skipped` (conta como vez), `PlaybackFinished(skipped)`, próximo em < 1 s | IT-02, E2E-01 |
| Pausar/retomar | gerente pausa e retoma | TV pausa e retoma o player; estado `paused`/`playing` | IT-02, E2E-01 |
| Operação inválida | pausar com Idle; retomar sem pausa; pular sem música | 409 com o código do contrato | IT-05 |
| Fila acaba | `ended` sem próximo | `Idle`; `NowPlayingChanged(null)` | IT-01 |
| Relato atrasado | `ended` de um pedido que não é o atual | 409 `STALE_REQUEST`; fila não avança | IT-03 |
| Duplicado | dois `ended` simultâneos do atual | avança uma única vez | IT-03 |
| TV offline | sem heartbeat por 30 s | `TvStatusChanged(offline)`, `juker.tv.online=0`; regra de alerta declarada | IT-04, CT-01 |
| TV volta | reconecta após 1 min | relê o estado e retoma a mesma música perto da posição do último heartbeat | IT-04, UT-02 |
| Bloqueio da comanda atual | `ComandaBlocked` da comanda que está tocando | pula a música | IT-06 |
| Now-playing público | tocando | título, artista, comanda e `queueCount`; com Idle → 204; jukebox desligado → 404 | IT-05 |
| Autoplay | primeira carga da TV | tela "Toque para iniciar o Juker"; após o toque, carrega com autoplay | UT-03 |

## 7. Artefato B — Plano de Testes (TDD)

### 7.1 Testes de Caracterização
- N/A — funcionalidade nova.

### 7.2 Testes Unitários
- **UT-01** — Dado cada combinação (estado × evento) da tabela de transições, quando `PlaybackStateMachine.Handle` é aplicado, então o novo estado e as ações (`Finish` com motivo, `StartNext`, notificação) são os esperados; combinações inválidas retornam o erro do contrato.
- **UT-02** — Dado o `TvPlaybackController` com `IMediaPlayer` fake, quando recebe `NowPlayingChanged`, `Resynced` com `positionSeconds=95` e `PlaybackStateChanged(paused)`, então chama `LoadAsync` com autoplay, retoma em 95 s e chama `PauseAsync`; ao `Ended` do player, faz o POST `ended` com o `requestId` atual; envia heartbeat a cada 5 s (FakeTimeProvider).
- **UT-03** — Dado a primeira carga da TV (bUnit), então aparece "Toque para iniciar o Juker" com um botão focável; após o toque, o controlador inicia e a tela some.

### 7.3 Testes de Integração
- **IT-01** — Com Postgres real, fila e TV de teste: com o player Idle e a TV online, um `RequestAdded` inicia a reprodução e a TV (cliente do hub) recebe `NowPlayingChanged`; com o `ended` do último, o estado volta a Idle com `NowPlayingChanged(null)`.
- **IT-02** — `ended`, `failed(unavailable)` e `skip` avançam para o próximo com os motivos `Played`, `Failed` e `Skipped` no `IQueueModule`; `PlaybackFailed`/`PlaybackFinished` saem pelo outbox; pausar/retomar geram `PlaybackStateChanged`; do relato à notificação da próxima, p95 < 1 s.
- **IT-03** — `ended` de um `requestId` antigo → 409 `STALE_REQUEST` sem avançar; dois `ended` simultâneos do atual avançam uma vez só.
- **IT-04** — Sem heartbeat por 30 s (FakeTimeProvider), `TvStatusChanged(offline)` vai para o grupo `staff` e o gauge fica 0; um heartbeat depois disso volta a `online`, e `GET /tv/state` devolve a mesma música com a posição do último heartbeat.
- **IT-05** — Pausar com Idle → 409 `NOT_PLAYING`; retomar sem pausa → 409 `NOT_PAUSED`; pular sem música → 409 `NOTHING_PLAYING`; `now-playing` público com música → 200, com Idle → 204, com jukebox desligado → 404; cliente sem token Tv em `/tv/*` → 401.
- **IT-06** — `ComandaBlocked` (payload do contrato da SPEC-0022) da comanda que está tocando faz o player pular; de outra comanda, não muda nada.

### 7.4 Testes de Contrato
- **CT-01** — Snapshot OpenAPI dos endpoints de TV, gerente e público, schemas de `PlaybackFailed`/`PlaybackFinished` (consumidos pelas SPECs 0013, 0016, 0021, 0023 e 0024); validação do consumidor contra `IQueueModule` (SPEC-0018@1) e `ComandaBlocked` (SPEC-0022@1); regra `juker-tv-offline` válida no formato de `alerts.yaml` da SPEC-0008.

### 7.5 Testes E2E
- **E2E-01** — TV de teste (FakeMediaPlayer, vídeos de 3 s) aberta e iniciada; duas músicas pedidas pela API de teste: a TV toca a 1ª e passa sozinha para a 2ª; o gerente (API) pausa → a TV pausa; retoma → continua; pula → a TV vai para a próxima (ou para o estado parado quando não há próxima).

### 7.6 Outros
- Resistência em staging: TV real tocando por 2 horas seguidas com a `DemoPlaylist`/fila de teste, com uma queda de Wi-Fi forçada de 1 min no meio; a TV retoma sozinha (registrar no Relatório de Entrega).

**Dublês e dados de teste:** `IMediaPlayer` fake; TV de teste com token do emissor de teste (pareamento real na SPEC-0021); eventos pelos schemas dos contratos.

**Ambiente de execução:** Testcontainers + host real (IT); AppHost + Playwright (E2E); staging com TV real (7.6).

## 8. Plano de Rollout
- **Estratégia:** atrás da flag `JukeboxEnabled` (a TV só toca a fila se o bar estiver com o jukebox ligado).
- **Dados/schema:** migration aditiva `playback.Initial` (`venue_playback`, `tv_presence`).
- **Compatibilidade:** N/A.
- **Observabilidade:** `juker.tv.online`, `juker.playback.errors` por código, alerta `juker-tv-offline`.
- **Rollback:** redeploy anterior; desligar a flag do bar para a TV voltar a só exibir o estado parado.
- **Etapas de migração/coexistência:** a página de demonstração `/tv/{slug}` da SPEC-0007 continua até a SPEC-0021 trocar pela TV pareada.

## 9. Questões em Aberto
- [x] Pausar/retomar do gerente — entra no MVP (thomas, 2026-09-30)
- [x] Música pulada conta como vez — sim (thomas, 2026-09-30)

## 10. Aprovação (H1)
Registrada no frontmatter (`approved_by`, `approved_at`) somente depois que o humano responder "Aprovado". O arquiteto nunca aprova a própria spec.

## 11. Checklist de Implementação

**Fase 1: Regras e componentes (unitário)**
- [ ] Red: escrever UT-01, UT-02, UT-03 com a tag `SPEC-0020:<ID>` e confirmar que falham pelo motivo certo
- [ ] Green: implementar o mínimo para passar, seguindo os ADRs de fundação e o padrão de slices
- [ ] Refactor mantendo tudo verde
- [ ] Validar: build + lint + suíte de UT (G2)

**Fase 2: Integração e contrato**
- [ ] Red: escrever IT-01, IT-02, IT-03, IT-04, IT-05, IT-06, CT-01 com a tag `SPEC-0020:<ID>` (Postgres real, esquemas de auth reais, `DrainOutboxAsync`, WireMock — ADR-0012) e confirmar que falham pelo motivo certo
- [ ] Green: implementar endpoints, persistência, migrations e integrações até passar
- [ ] Refactor mantendo tudo verde
- [ ] Validar: build + suíte completa + arquitetura + cobertura do diff ≥ 80% (G2/G3)

**Fase 3: Jornada E2E**
- [ ] Red: escrever E2E-01 com page objects, pré-condições pela API `/_test/*` e jornada pela interface (ADR-0013); confirmar que falham pelo motivo certo
- [ ] Green: jornada completa passando localmente (cliente em Chromium e WebKit quando aplicável), sem violações axe

**Fase 4: Verificações adicionais (seção 7.6)**
- [ ] Executar as verificações da seção 7.6 e anexar a evidência ao Relatório de Entrega

**Fase final: Integração, entrega e documentação**
- [ ] Review independente (G4)
- [ ] Integração + CI verde (G5) e aprovação (H2)
- [ ] Deploy via pipeline com smoke/E2E no ambiente (G6)
- [ ] Relatório de Entrega, docs raiz e CHANGELOG (G7)

## 12. Registro de Gates
| Gate | Status | Evidência | Data |
|---|---|---|---|
| G0 Spec | PASS | `spec_graph.py validate SPEC-0020` → G0 [SPEC-0020]: PASS — 0 erro(s), 0 aviso(s); checklist de julgamento do G0 revisado pelo Architect | 2026-09-30 |
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
