---
id: SPEC-0022
title: Bloqueio de comanda e modo ouvinte
tier: full
type: feature
user_facing: true
status: approved
created: 2026-09-30
parent: SPEC-0010
depends_on: [SPEC-0013, SPEC-0018]
consumes_contract: [SPEC-0020@1]
contract_version: 1
touches: [src/Modules/Sessions/Juker.Modules.Sessions/Features/Blocking/**, src/Modules/Sessions/Juker.Modules.Sessions/Features/Comandas/**, src/Modules/Sessions/Juker.Modules.Sessions/Domain/**, src/Modules/Sessions/Juker.Modules.Sessions/Features/Join/**, src/Modules/Sessions/Juker.Modules.Sessions/Infrastructure/Persistence/Migrations/**, src/Modules/Sessions/Juker.Modules.Sessions.Contracts/**, src/Modules/Queue/Juker.Modules.Queue/Features/Moderation/**, src/Modules/Queue/Juker.Modules.Queue/Features/AddRequest/**, src/Modules/Queue/Juker.Modules.Queue/Features/Vote/**, src/Web/Juker.Web.Client/Guest/Listener/**, tests/Juker.Modules.Sessions.UnitTests/Blocking/**, tests/Juker.IntegrationTests/Moderation/**, tests/Juker.Web.UnitTests/Guest/Listener/**, tests/Juker.E2ETests/Guest/Listener/**]
adrs: [ADR-0006]
external: []
size: M
approved_by: thomas
approved_at: 2026-09-30
---

# SPEC-0022 — Bloqueio de comanda e modo ouvinte

## 1. Visão Geral
Quando uma comanda fica sacaneando (áudios de zoeira, spam), o gerente a **bloqueia pela noite**. Efeitos, decididos pelo fundador: todos os celulares dessa comanda entram em **modo ouvinte** (veem a fila e o que está tocando, mas não pedem nem votam), os **pedidos pendentes** dela saem da fila, os **votos** que ela deu deixam de contar e, se a música que está tocando for dela, a TV **pula**. O cliente vê a tela neutra **"Pedidos pausados"**. O bloqueio acaba na virada da noite ou quando o gerente desbloqueia.

## 2. Motivação & Escopo
**Motivação:** é o "botão de pânico" do dono do bar contra quem estraga a noite, sem humilhar o cliente em público.

**Objetivos (dentro do escopo):**
- `ComandaBlock` (VenueId, Night, Comanda, BlockedAt, BlockedBy, UnblockedAt) no módulo Sessions; bloquear/desbloquear pela API do gerente.
- `ISessionsModule.GetModeAsync` passa a retornar `Listener` para comanda bloqueada; entrada nova de comanda bloqueada cria a sessão já como ouvinte.
- Evento `ComandaBlocked`/`ComandaUnblocked` (outbox) e notificação `ComandaModeChanged` para o grupo da comanda.
- Consumidor na Queue: remove os pendentes da comanda (`Removed(Block)`) e apaga os votos dela (saldos recalculados); `AddRequest` e `Vote` recusam ouvinte com 403 `LISTENER_MODE`.
- Pular a música atual quando for da comanda bloqueada fica com o consumidor da Playback (SPEC-0020, já contratado).
- `GET /api/v1/admin/comandas` (lista para o painel) com filtros `all | with-requests | blocked` e busca por número, combinando `ISessionsModule` com `IQueueModule.CountByComandaAsync` e o estado de reprodução.
- Tela do cliente "Pedidos pausados" (Stitch "Comanda Bloqueada"), com "MODO OUVINTE", o texto neutro, o "Tocando agora" e "Ver a fila"; na fila, sem botões de voto e sem "Pedir" na busca.

**Não-objetivos (fora do escopo):**
- Botões e diálogo no painel (SPEC-0023).
- Motivo do bloqueio e alerta de comanda suspeita (ideias para depois do piloto).
- Restaurar pedidos removidos ao desbloquear.

## 3. Dependências
- **Implementações necessárias:** SPEC-0013 (módulo Sessions, sessões e `ISessionsModule`); SPEC-0018 (módulo Queue, pedidos e votos).
- **Contratos consumidos:** SPEC-0020@1 (estado atual da reprodução para marcar "Tocando agora" na lista de comandas; o consumidor de `ComandaBlocked` da Playback).
- **Pré-requisitos externos:** N/A.

## 4. Decisão Arquitetural
**Contexto:** projeto novo; ADR-0006 (evento via outbox com consumidores idempotentes).

**Decisão:** o bloqueio é do módulo Sessions (dono da identidade da comanda); os efeitos na fila e no player são consumidores do evento `ComandaBlocked` nos módulos donos dos dados.

**Justificativa:** cada módulo altera só os próprios dados, e o outbox garante que o bloqueio nunca fique "pela metade" (pedidos ainda na fila depois de bloquear).

**Desvio do padrão existente:** Nenhum.

**Alternativas descartadas:** o handler do bloqueio chamar Queue e Playback diretamente (acoplamento e risco de efeito parcial); revogar as sessões (o cliente perderia até a visão da fila — o fundador escolheu o modo ouvinte).

**ADRs:** ADR-0006.

## 5. Requisitos Não-Funcionais
- **Desempenho e escala:** do bloqueio até a remoção dos pendentes e o modo ouvinte no celular p95 < 2 s — IT-02.
- **Segurança:** só `ManagerOfVenue` do mesmo bar; ouvinte recebe 403 em pedir e votar, mesmo chamando a API direto — IT-03.
- **Privacidade e dados pessoais:** o registro guarda quem bloqueou (ID da equipe) e quando; nada é mostrado aos outros clientes.
- **Disponibilidade e resiliência:** consumidores idempotentes; bloquear duas vezes não duplica efeitos — IT-04.
- **Acessibilidade (UI):** tela "Pedidos pausados" com título em `h1`, texto claro e o botão "Ver a fila" focável — UT-02 e E2E-01 (axe).
- **Custo:** N/A.

## 6. Artefato A — Contrato
**Interface:** API do gerente, efeitos, evento e tela do cliente.

```text
POST   /api/v1/admin/comandas/{comanda}/block     (ManagerOfVenue) → 204 | 409 ALREADY_BLOCKED | 422 COMANDA_OUT_OF_RANGE
DELETE /api/v1/admin/comandas/{comanda}/block     (ManagerOfVenue) → 204 | 409 NOT_BLOCKED
GET    /api/v1/admin/comandas?filter=all|with-requests|blocked&q=12   (ManagerOfVenue)
  200 [ { comanda, devices, pending, requestedTonight, blocked, blockedAt, isPlaying, isNext } ]

Evento (outbox): ComandaBlocked(venue, night, comanda, blockedAt) · ComandaUnblocked(venue, night, comanda)
Efeitos de ComandaBlocked:
  Sessions: GetModeAsync → Listener; ComandaModeChanged(listener) → grupo da comanda
  Queue:    pendentes da comanda → Removed(Block) ; votos da comanda apagados ; QueueChanged
  Playback: atual da comanda → skip (SPEC-0020)
Queue: POST /queue e PUT /vote por ouvinte → 403 LISTENER_MODE "Sua comanda está com os pedidos pausados nesta noite."
Expiração: o bloqueio vale só para a noite (Night); na noite seguinte a comanda volta a participante

Tela do cliente (modo ouvinte):
  "MODO OUVINTE" · "PEDIDOS PAUSADOS" · "Sua comanda está impedida de pedir músicas nesta noite.
   Se achar que é um engano, fale com a equipe do bar." · Tocando agora · [Ver a fila]
  Fila em modo ouvinte: sem botões de voto · Busca: sem botão "Pedir"
```

**Arquivos/módulos afetados:** ver `touches` no frontmatter.

### 6.1 Mapa de Comportamentos
| Cenário | Condição / Entrada | Resultado esperado | Testes |
|---|---|---|---|
| Regras do bloqueio | bloquear, bloquear de novo, desbloquear, desbloquear sem bloqueio | 204, 409 `ALREADY_BLOCKED`, 204, 409 `NOT_BLOCKED` | UT-01, IT-01 |
| Expira na virada | bloqueio na noite 02/10, consulta na noite 03/10 | comanda participante de novo | UT-01, IT-05 |
| Pendentes removidos | comanda 12 com 2 pendentes | ambos `Removed(Block)`; `QueueChanged` | IT-02, E2E-01 |
| Votos anulados | comanda 12 votou -1 na música da 7 (saldo -1) | voto apagado; saldo da 7 volta a 0; ordem recalculada | IT-02, E2E-01 |
| Modo ouvinte ao vivo | celulares da 12 conectados | recebem `ComandaModeChanged(listener)` e mostram "Pedidos pausados" | IT-02, E2E-01 |
| Entrada de bloqueada | novo celular entra com a comanda 12 | sessão criada como ouvinte; vai para "Pedidos pausados" | IT-03 |
| Ouvinte tenta pedir/votar | chamada direta à API | 403 `LISTENER_MODE` | IT-03 |
| Desbloquear | gerente desbloqueia | modo participante; pode pedir de novo; removidos não voltam | IT-01, E2E-01 |
| Idempotência | evento `ComandaBlocked` entregue duas vezes | efeitos aplicados uma vez | IT-04 |
| Lista de comandas | 3 comandas, uma bloqueada, uma tocando | filtros e busca retornam os campos certos (`blocked`, `isPlaying`, `isNext`) | IT-06 |
| Fora da faixa | bloquear a comanda 999 com faixa 1–300 | 422 `COMANDA_OUT_OF_RANGE` | IT-01 |
| Tela do ouvinte | modo `listener` | textos do contrato; sem voto e sem "Pedir" | UT-02 |

## 7. Artefato B — Plano de Testes (TDD)

### 7.1 Testes de Caracterização
- N/A — a entrada (SPEC-0013) e a fila (SPEC-0018) têm testes próprios; os IT desta spec incluem guardas para o caminho participante (`(guarda: passa antes da mudança)` no IT-03 para comandas não bloqueadas).

### 7.2 Testes Unitários
- **UT-01** — Dado `ComandaBlock` para a noite 02/10, quando `IsBlocked(noite)` é consultado para 02/10 e 03/10, então retorna verdadeiro e falso; bloquear o que já está bloqueado retorna `ALREADY_BLOCKED`; desbloquear sem bloqueio retorna `NOT_BLOCKED`.
- **UT-02** — Dado o `ListenerScreen` e a Fila em modo `listener` (bUnit), então aparecem "MODO OUVINTE", "PEDIDOS PAUSADOS" e o texto do contrato em `h1`/parágrafo, o botão "Ver a fila", e a Fila não renderiza controles de voto.

### 7.3 Testes de Integração
- **IT-01** — Com Postgres real: bloquear a comanda 12 → 204; de novo → 409; desbloquear → 204; de novo → 409; bloquear a 999 com faixa 1–300 → 422.
- **IT-02** — Comanda 12 com 2 pendentes e um voto -1 na música da comanda 7; após o bloqueio: os 2 pedidos `Removed(Block)`, o voto apagado (saldo da 7 = 0), `QueueChanged` e `ComandaModeChanged(listener)` recebidos pelo grupo da 12, tudo em p95 < 2 s.
- **IT-03** — Um novo dispositivo entra com a comanda 12 bloqueada → sessão com `mode=listener`; `POST /queue` e `PUT /vote` pela sessão ouvinte → 403 `LISTENER_MODE`; (guarda: passa antes da mudança) a comanda 13, não bloqueada, continua pedindo e votando.
- **IT-04** — O mesmo `ComandaBlocked` entregue duas vezes (reprocessamento do outbox) remove e anula uma única vez.
- **IT-05** — Bloqueio na noite 02/10; às 06:01 de 03/10 (FakeTimeProvider, virada 06:00), a comanda 12 entra como participante.
- **IT-06** — `GET /admin/comandas` com filtros `all`, `with-requests` e `blocked` e `q=12` retorna os campos do contrato, com `isPlaying` vindo do estado da reprodução (SPEC-0020) e `isNext` do primeiro da fila.

### 7.4 Testes de Contrato
- **CT-01** — Snapshot dos schemas de `ComandaBlocked`/`ComandaUnblocked` (consumidos pela SPEC-0020) e do OpenAPI de `/admin/comandas` e do bloqueio (consumidos pela SPEC-0023); validação do uso do estado de reprodução contra o contrato da SPEC-0020@1.

### 7.5 Testes E2E
- **E2E-01** — A comanda 12 tem 2 músicas na fila e votou negativo na da comanda 7; o gerente bloqueia a 12 (API de teste): no celular da 12, aparece "Pedidos pausados" sem recarregar; na Fila da comanda 7, as músicas da 12 sumiram e o saldo da música dela voltou a 0; o gerente desbloqueia e o celular da 12 volta a poder pedir; sem violações axe na tela do ouvinte.

### 7.6 Outros
- N/A.

**Dublês e dados de teste:** sessões de várias comandas pelo emissor de teste; `FakeTimeProvider` para a virada da noite.

**Ambiente de execução:** Testcontainers + host real (IT); AppHost + Playwright (E2E).

## 8. Plano de Rollout
- **Estratégia:** atrás da flag `JukeboxEnabled`.
- **Dados/schema:** migration aditiva `sessions.Blocking` (`comanda_blocks`, índice por venue/night/comanda).
- **Compatibilidade:** `ISessionsModule.GetModeAsync` já existia (SPEC-0013); agora pode retornar `Listener`.
- **Observabilidade:** contador `juker.comandas.blocked`; log de auditoria de bloqueio/desbloqueio (quem, quando, comanda).
- **Rollback:** redeploy anterior; bloqueios existentes deixam de ter efeito na versão anterior (aceitável).
- **Etapas de migração/coexistência:** N/A.

## 9. Questões em Aberto
- [x] Bloqueado vota? — não; modo ouvinte só leitura (thomas, 2026-09-30)
- [x] Bloqueio remove os pedidos pendentes? — sim (thomas, 2026-09-30)
- [x] Bloqueio anula os votos já dados? — sim (thomas, 2026-09-30)

## 10. Aprovação (H1)
Registrada no frontmatter (`approved_by`, `approved_at`) somente depois que o humano responder "Aprovado". O arquiteto nunca aprova a própria spec.

## 11. Checklist de Implementação

**Fase 1: Regras e componentes (unitário)**
- [ ] Red: escrever UT-01, UT-02 com a tag `SPEC-0022:<ID>` e confirmar que falham pelo motivo certo
- [ ] Green: implementar o mínimo para passar, seguindo os ADRs de fundação e o padrão de slices
- [ ] Refactor mantendo tudo verde
- [ ] Validar: build + lint + suíte de UT (G2)

**Fase 2: Integração e contrato**
- [ ] Red: escrever IT-01, IT-02, IT-03, IT-04, IT-05, IT-06, CT-01 com a tag `SPEC-0022:<ID>` (Postgres real, esquemas de auth reais, `DrainOutboxAsync`, WireMock — ADR-0012) e confirmar que falham pelo motivo certo
- [ ] Green: implementar endpoints, persistência, migrations e integrações até passar
- [ ] Refactor mantendo tudo verde
- [ ] Validar: build + suíte completa + arquitetura + cobertura do diff ≥ 80% (G2/G3)

**Fase 3: Jornada E2E**
- [ ] Red: escrever E2E-01 com page objects, pré-condições pela API `/_test/*` e jornada pela interface (ADR-0013); confirmar que falham pelo motivo certo
- [ ] Green: jornada completa passando localmente (cliente em Chromium e WebKit quando aplicável), sem violações axe

**Fase final: Integração, entrega e documentação**
- [ ] Review independente (G4)
- [ ] Integração + CI verde (G5) e aprovação (H2)
- [ ] Deploy via pipeline com smoke/E2E no ambiente (G6)
- [ ] Relatório de Entrega, docs raiz e CHANGELOG (G7)

## 12. Registro de Gates
| Gate | Status | Evidência | Data |
|---|---|---|---|
| G0 Spec | PASS | `spec_graph.py validate SPEC-0022` → G0 [SPEC-0022]: PASS — 0 erro(s), 0 aviso(s); checklist de julgamento do G0 revisado pelo Architect | 2026-09-30 |
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
