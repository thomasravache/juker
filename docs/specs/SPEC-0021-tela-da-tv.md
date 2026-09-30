---
id: SPEC-0021
title: Tela da TV
tier: full
type: feature
user_facing: true
status: approved
created: 2026-09-30
parent: SPEC-0010
depends_on: [SPEC-0020]
consumes_contract: [SPEC-0013@1, SPEC-0011@1, SPEC-0018@1]
contract_version: 1
touches: [src/Modules/Playback/Juker.Modules.Playback/Features/Pairing/**, src/Modules/Playback/Juker.Modules.Playback/Infrastructure/Persistence/Migrations/**, src/Modules/Playback/Juker.Modules.Playback.Contracts/Pairing*, src/Web/Juker.Web.Client/Tv/Screen/**, src/Web/Juker.Web.Client/Tv/Pairing/**, src/Web/Juker.Web/ShortLinks/**, tests/Juker.Modules.Playback.UnitTests/Pairing/**, tests/Juker.IntegrationTests/Playback/Pairing/**, tests/Juker.IntegrationTests/Web/ShortLinks/**, tests/Juker.Web.UnitTests/Tv/Screen/**, tests/Juker.E2ETests/Tv/Screen/**]
adrs: [ADR-0003, ADR-0007, ADR-0008]
external: []
size: M
approved_by: thomas
approved_at: 2026-09-30
---

# SPEC-0021 — Tela da TV

## 1. Visão Geral
A tela passiva do bar, no layout aprovado no Stitch: o vídeo em 16:9 **sem nada sobreposto**, a barra lateral **"A SEGUIR"** com as próximas 3 músicas e as comandas, o letreiro neon **"TOCANDO AGORA: Música – Artista • Pedido pela Comanda X"** (com o ícone de karaokê quando for o caso), o **QR Code** com o link curto e o **código de 4 dígitos**, os **avisos do bar** em rodízio, "N mesas conectadas", o relógio e o **estado de fila vazia**. Inclui o **pareamento da TV** com um código gerado no painel e o **link curto** do bar (`/{slug}` → `/b/{slug}`).

## 2. Motivação & Escopo
**Motivação:** a TV é o que todo o bar vê; ela precisa explicar sozinha como participar e deixar claro quem pediu cada música.

**Objetivos (dentro do escopo):**
- Pareamento: o gerente gera um código de 6 caracteres (A–Z sem O/I, 2–9), válido por 10 min e de uso único; a TV abre `/tv`, digita o código e recebe o token de dispositivo (implementação real de `ITvTokenIssuer`, cookie HttpOnly de 1 ano); o gerente lista e revoga TVs pareadas (API; a tela fica na SPEC-0023).
- `/tv` substitui a página de demonstração `/tv/{slug}` (que passa a redirecionar para `/tv`).
- Layout 1920×1080: cabeçalho (logo, bar, "N mesas conectadas" via `ISessionsModule`, relógio), vídeo à esquerda (~75%) com o `IMediaPlayer` e o `TvPlaybackController` da SPEC-0020, lateral "A SEGUIR" (até 3 itens de `GET /api/v1/tv/queue?take=3`: miniatura, TÍTULO, artista, "COMANDA X", badge), letreiro abaixo do vídeo com barra de progresso, bloco do QR (SVG gerado no servidor com QRCoder, MIT) + "Peça sua música pelo celular" + link curto + "Código: 4821" (atualizado por `PinRotated`), avisos (`VenueRules.Announcements`) trocando a cada 20 s.
- Estado pausado: letreiro mostra "PAUSADO" (nada sobre o vídeo).
- Estado de fila vazia: área central com "A FILA ESTÁ VAZIA!", "Escaneie o QR Code e peça a primeira música da noite", QR grande, código e link curto.
- Tamanhos mínimos para leitura a 5 m (títulos ≥ 40 px, textos ≥ 28 px a 1080p).
- Link curto: `GET /{slug}` → 302 `/b/{slug}` para slugs válidos, exceto rotas reservadas (`b`, `tv`, `admin`, `api`, `hubs`, `health`, `openapi`, `_framework`, `_content`).
- Textos ajustados na revisão do design: sem "AO VIVO", "TV DISPLAY", "SALÃO PRINCIPAL", "Tempo restante", "Palco disponível" e "Milhões de clipes".

**Não-objetivos (fora do escopo):**
- Tela de pareamento no painel (SPEC-0023).
- Temas por bar (cores/logo próprios).
- Anúncios de terceiros na TV.

## 3. Dependências
- **Implementações necessárias:** SPEC-0020 — controlador da TV, estado e eventos de reprodução.
- **Contratos consumidos:** SPEC-0013@1 (`GET /api/v1/tv/pin`, `PinRotated`, `ISessionsModule.CountConnectedComandasAsync`); SPEC-0011@1 (`VenueRules.Announcements`, `VenueSettingsChanged`); SPEC-0018@1 (`GET /api/v1/tv/queue?take=3`).
- **Pré-requisitos externos:** N/A.

## 4. Decisão Arquitetural
**Contexto:** projeto novo; ADR-0003 (TV em WASM), ADR-0007 (nada sobreposto ao vídeo) e ADR-0008 (token de dispositivo por pareamento).

**Decisão:** o pareamento fica no módulo Playback (dono da TV), com `Features/Pairing`; a tela é composta de componentes em `Juker.Web.Client/Tv/Screen`; o QR e o link curto são endpoints do `Juker.Web`.

**Justificativa:** a TV é um recurso de reprodução; manter o pareamento junto evita outro módulo só para dispositivos.

**Desvio do padrão existente:** Nenhum.

**Alternativas descartadas:** login do gerente na TV (senha digitada num controle remoto, sessão curta); QR gerado no navegador (mais JS no WASM da TV sem ganho).

**ADRs:** ADR-0003, ADR-0007, ADR-0008.

## 5. Requisitos Não-Funcionais
- **Desempenho e escala:** a TV roda 8 h sem crescimento de memória acima de 20% — 7.6; troca de música refletida no letreiro e na lateral em < 1 s — E2E-01.
- **Segurança:** código de pareamento de uso único, expira em 10 min, 5 tentativas erradas por IP em 10 min → 429; token armazenado como hash; TV revogada perde o acesso na próxima chamada — IT-01 e IT-02.
- **Privacidade e dados pessoais:** a TV mostra só números de comanda e músicas.
- **Disponibilidade e resiliência:** sem conexão, a TV mantém o vídeo atual e mostra "Reconectando…" no cabeçalho (fora do vídeo) — UT-03.
- **Acessibilidade (UI):** contraste AA para leitura à distância; sem texto só em cor (o karaokê tem ícone e palavra) — UT-01 e E2E-01 (axe).
- **Custo:** N/A.

## 6. Artefato A — Contrato
**Interface:** pareamento, link curto, QR e o fluxo de tela da TV.

```text
POST   /api/v1/admin/tv/pairing-codes            (ManagerOfVenue) → 201 { "code": "K7M2QX", "expiresAt": "…" }
GET    /api/v1/admin/tv/devices                  (ManagerOfVenue) → 200 [ { deviceId, name, pairedAt, lastSeenAt, online } ]
DELETE /api/v1/admin/tv/devices/{deviceId}       (ManagerOfVenue) → 204
POST   /api/v1/tv/pair { "code": "K7M2QX", "name": "TV do salão" } (anônimo)
  201 + Set-Cookie (token Tv) | 422 PAIRING_CODE_INVALID "Código inválido ou expirado." | 429 TOO_MANY_ATTEMPTS
GET /{slug}              → 302 /b/{slug}   (slug válido e não reservado) | 404
GET /api/v1/tv/qr.svg    (TvOfVenue) → image/svg+xml com a URL https://<domínio>/{slug}

Tela /tv (1920×1080):
  sem token → "Parear esta TV" · "Digite o código mostrado no painel do gerente" · 6 caixas · [Parear]
  cabeçalho: JUKER | BAR DO ROCK | "8 Mesas Conectadas" | 20:56
  vídeo 16:9 (esquerda) — nada sobreposto
  lateral: "A SEGUIR" · 1..3 itens (miniatura, TÍTULO, Artista, "COMANDA 14", badge)
  aviso em rodízio (20 s): "🍺 CHOPP EM DOBRO ATÉ 21H"
  bloco QR: "PEÇA SUA MÚSICA PELO CELULAR" · QR · "juker.bar/rock" · "CÓDIGO: 4821"
  letreiro: "TOCANDO AGORA" | "ENTER SANDMAN — METALLICA" | "PEDIDO PELA COMANDA 12" | 🎤 se karaokê | barra de progresso
  pausado: letreiro "PAUSADO"
  fila vazia: "A FILA ESTÁ VAZIA!" · "Escaneie o QR Code e peça a primeira música da noite" · QR grande · código · link
```

**Arquivos/módulos afetados:** ver `touches` no frontmatter.

### 6.1 Mapa de Comportamentos
| Cenário | Condição / Entrada | Resultado esperado | Testes |
|---|---|---|---|
| Letreiro | tocando "Enter Sandman" da comanda 12 | "ENTER SANDMAN — METALLICA" e "PEDIDO PELA COMANDA 12"; com karaokê, ícone 🎤 + "KARAOKÊ" | UT-01, E2E-01 |
| A seguir | 5 pendentes | só os 3 primeiros da ordem justa, com comanda e badge | UT-01, E2E-01 |
| Fila vazia | Idle sem pendentes | estado "A FILA ESTÁ VAZIA!" com QR, código e link | UT-02, E2E-02 |
| Pausado | estado `paused` | letreiro "PAUSADO"; nada sobre o vídeo | UT-02 |
| Avisos | 2 avisos configurados | alternam a cada 20 s; sem avisos, o bloco some | UT-02 |
| PIN atualizado | `PinRotated` | "CÓDIGO" muda sem recarregar | UT-03 |
| Mesas conectadas | 8 comandas com sessão na noite | "8 Mesas Conectadas" | UT-03 |
| Reconectando | hub fora por 5 s | "Reconectando…" no cabeçalho; vídeo continua | UT-03 |
| Pareamento | código válido | 201, token Tv, TV passa a exibir a tela do bar | IT-01, E2E-01 |
| Código inválido/expirado/usado | código errado, 10 min depois ou reutilizado | 422 `PAIRING_CODE_INVALID` | IT-01 |
| Força bruta | 6ª tentativa errada do IP em 10 min | 429 | IT-01 |
| Revogação | gerente revoga a TV | próxima chamada da TV → 401; TV volta à tela "Parear esta TV" | IT-02 |
| Link curto | `/rock` | 302 para `/b/rock`; `/admin` e `/api` não são capturados; slug inexistente → 404 | UT-04, IT-03 |
| QR | `qr.svg` | SVG que decodifica para `https://<domínio>/rock` | IT-03 |
| Demonstração antiga | `/tv/rock` | 302 para `/tv` | IT-03 |

## 7. Artefato B — Plano de Testes (TDD)

### 7.1 Testes de Caracterização
- N/A — funcionalidade nova (a página de demonstração da SPEC-0007 é substituída de propósito; o CT-02 da SPEC-0007 continua valendo para o player).

### 7.2 Testes Unitários
- **UT-01** — Dado o `NowPlayingMarquee` e o `UpNextList` (bUnit) com "Enter Sandman" da comanda 12 (clipe) e 5 pendentes, então o letreiro mostra os textos do contrato e a lista exibe só 3 itens com "COMANDA X"; com mídia karaokê, aparece "🎤 KARAOKÊ".
- **UT-02** — Dado a `TvScreen` (bUnit) nos estados Idle sem fila, `paused` e com 2 avisos, então mostra o estado de fila vazia, o letreiro "PAUSADO" e os avisos alternando a cada 20 s (FakeTimeProvider); sem avisos, o bloco não é renderizado.
- **UT-03** — Dado `PinRotated`, contagem de 8 comandas e o hub em `Reconnecting` por 5 s, então o código muda, o cabeçalho mostra "8 Mesas Conectadas" e "Reconectando…", e o player não é recarregado.
- **UT-04** — Dado os caminhos `/rock`, `/admin`, `/api`, `/_framework` e `/Rock!`, quando o `ShortLinkMatcher` avalia, então só `/rock` é tratado como link curto.

### 7.3 Testes de Integração
- **IT-01** — Com Postgres real: o gerente gera o código; `POST /tv/pair` com ele → 201 e cookie Tv aceito em `/api/v1/tv/state`; o mesmo código de novo → 422; código gerado há 11 min → 422; 6 tentativas erradas do mesmo IP → 429 na 6ª.
- **IT-02** — Gerente lista as TVs (com `lastSeenAt`) e revoga uma; a TV revogada recebe 401 na chamada seguinte.
- **IT-03** — `GET /rock` → 302 `/b/rock`; `GET /inexistente` → 404; `GET /tv/rock` → 302 `/tv`; `qr.svg` decodificado (ZXing.Net no teste) contém `https://<domínio configurado>/rock`.

### 7.4 Testes de Contrato
- **CT-01** — Snapshot OpenAPI do pareamento e da lista/revogação de TVs (consumido pela SPEC-0023); validação dos clientes da TV contra `GET /api/v1/tv/pin` (SPEC-0013@1), `GET /api/v1/tv/queue` (SPEC-0018@1) e o schema de `VenueSettingsChanged` (SPEC-0011@1).

### 7.5 Testes E2E
- **E2E-01** — Gerente gera o código pela API de teste; a TV (1920×1080) abre `/tv`, digita o código e pareia; 4 músicas são pedidas por 3 comandas (uma karaokê); a TV mostra "TOCANDO AGORA" com "PEDIDO PELA COMANDA X", a lateral "A SEGUIR" com as 3 próximas na ordem justa, o QR, o link curto e o código; quando a música termina (vídeos de 3 s), o letreiro e a lateral atualizam em < 1 s; sem violações axe.
- **E2E-02** — Com a fila vazia, a TV pareada mostra "A FILA ESTÁ VAZIA!" com o QR e o código; um pedido feito pelo celular tira a TV desse estado e ela começa a tocar.

### 7.6 Outros
- Na TV real do piloto: leitura a 5 m do letreiro, da lateral e do código (checklist com fotos), e 8 h ligada sem crescimento de memória acima de 20% (DevTools/`performance.memory` registrado a cada hora).

**Dublês e dados de teste:** `FakeMediaPlayer`; pedidos via API de teste; `FakeTimeProvider` para a rotação dos avisos e do PIN.

**Ambiente de execução:** bUnit; Testcontainers (IT); AppHost + Playwright (E2E); TV real em staging (7.6).

## 8. Plano de Rollout
- **Estratégia:** atrás da flag `JukeboxEnabled`; a TV do piloto é pareada em staging primeiro.
- **Dados/schema:** migration aditiva `playback.Pairing` (`tv_pairing_codes`, `tv_devices`).
- **Compatibilidade:** `/tv/{slug}` redireciona para `/tv`, preservando links antigos da demonstração.
- **Observabilidade:** contador de pareamentos e revogações; `lastSeenAt` por TV no painel.
- **Rollback:** redeploy anterior (a demonstração da SPEC-0007 volta); TVs pareadas continuam válidas na nova tentativa.
- **Etapas de migração/coexistência:** a rota de demonstração é removida quando a TV do piloto estiver pareada e estável por uma semana.

## 9. Questões em Aberto
Nenhuma.

## 10. Aprovação (H1)
Registrada no frontmatter (`approved_by`, `approved_at`) somente depois que o humano responder "Aprovado". O arquiteto nunca aprova a própria spec.

## 11. Checklist de Implementação

**Fase 1: Regras e componentes (unitário)**
- [ ] Red: escrever UT-01, UT-02, UT-03, UT-04 com a tag `SPEC-0021:<ID>` e confirmar que falham pelo motivo certo
- [ ] Green: implementar o mínimo para passar, seguindo os ADRs de fundação e o padrão de slices
- [ ] Refactor mantendo tudo verde
- [ ] Validar: build + lint + suíte de UT (G2)

**Fase 2: Integração e contrato**
- [ ] Red: escrever IT-01, IT-02, IT-03, CT-01 com a tag `SPEC-0021:<ID>` (Postgres real, esquemas de auth reais, `DrainOutboxAsync`, WireMock — ADR-0012) e confirmar que falham pelo motivo certo
- [ ] Green: implementar endpoints, persistência, migrations e integrações até passar
- [ ] Refactor mantendo tudo verde
- [ ] Validar: build + suíte completa + arquitetura + cobertura do diff ≥ 80% (G2/G3)

**Fase 3: Jornada E2E**
- [ ] Red: escrever E2E-01, E2E-02 com page objects, pré-condições pela API `/_test/*` e jornada pela interface (ADR-0013); confirmar que falham pelo motivo certo
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
| G0 Spec | PASS | `spec_graph.py validate SPEC-0021` → G0 [SPEC-0021]: PASS — 0 erro(s), 0 aviso(s); checklist de julgamento do G0 revisado pelo Architect | 2026-09-30 |
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
