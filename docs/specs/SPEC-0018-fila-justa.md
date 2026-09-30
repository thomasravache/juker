---
id: SPEC-0018
title: Fila justa
tier: full
type: feature
user_facing: false
status: approved
created: 2026-09-30
parent: SPEC-0010
depends_on: [SPEC-0013, SPEC-0015]
consumes_contract: [SPEC-0011@1, SPEC-0012@1]
contract_version: 1
touches: [src/Modules/Queue/**, tests/Juker.Modules.Queue.UnitTests/**, tests/Juker.IntegrationTests/Queue/**]
adrs: [ADR-0001, ADR-0006]
external: []
size: M
approved_by: thomas
approved_at: 2026-09-30
---

# SPEC-0018 — Fila justa

## 1. Visão Geral
O coração do Juker: o módulo Queue recebe os pedidos das comandas e mantém uma **fila justa e auto-organizada**. Cada pedido tem uma **vez** (rodada): as primeiras músicas de cada comanda tocam antes das segundas, as segundas antes das terceiras, e assim por diante; dentro da mesma vez, o saldo de votos e depois o horário do pedido decidem a ordem. Quem chegou agora passa na frente de quem já pediu várias. Inclui os limites (3 pendentes por comanda, sem duplicar música na fila, sem repetir música tocada nos últimos 60 min), a remoção do próprio pedido e a API que a TV, o painel e a reprodução usam.

## 2. Motivação & Escopo
**Motivação:** é a dor original — "uma mesma mesa coloca 10 músicas seguidas" — resolvida por regra, sem ninguém do bar precisar aprovar nada.

**Objetivos (dentro do escopo):**
- Modelo: `Request` (Id, VenueId, Night, Comanda, MediaId, snapshot de título/artista/tipo/duração/miniatura, RequestedAt, Status ∈ {Pending, Playing, Finished, Removed}, FinishReason ∈ {Played, Skipped, Failed}, RemovalReason ∈ {Owner, Manager, Votes, Block, Unavailable}, FinishedAt); `Vote` (RequestId, Comanda, Value) — a tabela nasce aqui, as regras de voto na SPEC-0019.
- `FairQueuePolicy` pura (sem I/O): vez = tocadas da comanda na noite (`Finished` com `Played` ou `Skipped`, mais a `Playing`) + posição do pedido entre os pendentes da comanda; ordem por (vez ↑, saldo ↓, RequestedAt ↑, Id ↑).
- Pedido (`AddRequest`): exige sessão participante (`ISessionsModule`), mídia tocável e dentro da duração (`ICatalogModule` + `VenueRules`), comanda com menos que `MaxPendingPerComanda` pendentes, música (mesma `SongId`) não pendente na fila e não tocada nos últimos `RepeatWindow`.
- Remoção pelo dono (pendente) e pelo gerente (pendente); consulta da fila (cliente, TV e gerente) e dos pedidos da comanda ("Minhas").
- `IQueueModule` para a Playback: `StartNextAsync` (marca o primeiro da fila como `Playing`) e `FinishAsync(requestId, reason)`; `CountByComandaAsync` para o painel.
- Eventos (outbox): `RequestAdded`, `RequestRemoved` (com motivo), `RequestStarted`, `RequestFinished`; notificações `QueueChanged` e `RequestRemoved` pelo `IVenueNotifier`.
- `queueVersion` monotônica por bar; métrica `juker.queue.length` e `juker.requests.added`.

**Não-objetivos (fora do escopo):**
- Telas (SPEC-0024) e regras/endpoint de voto e auto-remoção (SPEC-0019).
- Efeitos do bloqueio de comanda (SPEC-0022).
- Prioridade paga.

## 3. Dependências
- **Implementações necessárias:** SPEC-0013 (`ISessionsModule`, sessão Guest); SPEC-0015 (`ICatalogModule`).
- **Contratos consumidos:** SPEC-0011@1 (`VenueRules`: limite, duração máxima, janela de repetição, noite); SPEC-0012@1 (`IVenueNotifier`).
- **Pré-requisitos externos:** N/A.

## 4. Decisão Arquitetural
**Contexto:** projeto novo; ADR-0001 (módulo com `Domain/` rico e slices) e ADR-0006 (contratos síncronos + eventos).

**Decisão:** a ordenação é calculada na leitura por `FairQueuePolicy` (nada de posição armazenada); comandos que mudam a fila fazem lock por bar (`SELECT … FOR UPDATE` na linha `queue_heads` do bar) para serializar pedidos simultâneos e garantir o limite por comanda.

**Justificativa:** com menos de ~100 itens por bar, recalcular é barato e elimina bugs de reordenação concorrente; o lock por bar garante as invariantes (limite, sem duplicar) sob concorrência.

**Desvio do padrão existente:** Nenhum.

**Alternativas descartadas:** posição persistida com reordenação (frágil sob concorrência); fila em memória (perde tudo num reinício).

**ADRs:** ADR-0001, ADR-0006.

## 5. Requisitos Não-Funcionais
- **Desempenho e escala:** `GET` da fila p95 < 50 ms com 100 itens; `AddRequest` p95 < 150 ms com 20 pedidos simultâneos no mesmo bar — IT-05.
- **Segurança:** cliente só remove pedido da própria comanda; a comanda vem da sessão, nunca do corpo da requisição — IT-03.
- **Privacidade e dados pessoais:** a fila expõe o número da comanda (decisão de produto: "Pedido pela Comanda X"); nada além disso.
- **Disponibilidade e resiliência:** pedidos simultâneos nunca ultrapassam o limite da comanda nem duplicam a música — IT-05.
- **Acessibilidade (UI):** N/A — sem telas nesta spec (SPEC-0024).
- **Custo:** N/A.

## 6. Artefato A — Contrato
**Interface:** API HTTP da fila, `IQueueModule` e eventos.

```text
POST /api/v1/b/{slug}/queue  { "mediaId": "…" }                       (GuestOfVenue, participante)
  201 { "requestId": "…", "position": 5, "round": 1 }
  404 MEDIA_NOT_FOUND
  422 MEDIA_UNAVAILABLE      "Essa música não está disponível agora."
  422 MEDIA_TOO_LONG         "Essa música passa do limite de duração do bar."
  422 COMANDA_LIMIT_REACHED  "Você já tem 3 músicas na fila. Assim que uma tocar, você pode pedir outra."
  409 ALREADY_IN_QUEUE       "Essa música já está na fila."
  422 RECENTLY_PLAYED        "Essa música tocou há pouco. Tente de novo mais tarde."
DELETE /api/v1/b/{slug}/queue/{requestId}                              (GuestOfVenue, dono)
  204 | 403 NOT_OWNER | 409 NOT_PENDING | 404 REQUEST_NOT_FOUND
GET /api/v1/b/{slug}/queue                                             (GuestOfVenue)
  200 { "queueVersion": 42, "items": [ { "requestId", "position", "round", "title", "artist", "kind",
        "durationSeconds", "thumbnailUrl", "comanda", "score", "myVote": -1|0|1, "isMine",
        "secondsAhead" } ] }            // secondsAhead = soma das durações à frente na fila
GET /api/v1/b/{slug}/me/requests                                       (GuestOfVenue)
  200 { "limit": 3, "pendingCount": 2,
        "pending": [ …item… ], "finished": [ { …, "finishedAt", "reason": "played"|"skipped" } ] }
GET /api/v1/tv/queue?take=3                 (TvOfVenue)      → 200 { queueVersion, items (sem myVote/isMine) }
GET /api/v1/admin/queue                     (ManagerOfVenue) → 200 { queueVersion, items + requestedAt }
DELETE /api/v1/admin/queue/{requestId}      (ManagerOfVenue) → 204 | 409 NOT_PENDING | 404

// Juker.Modules.Queue.Contracts
public interface IQueueModule {
  Task<QueueItem?> StartNextAsync(VenueId v, CancellationToken ct);            // Pending → Playing
  Task FinishAsync(Guid requestId, FinishReason reason, CancellationToken ct);  // Playing → Finished
  Task<IReadOnlyDictionary<int, ComandaCounts>> CountByComandaAsync(VenueId v, DateOnly night, CancellationToken ct);
}
public sealed record QueueItem(Guid RequestId, VenueId Venue, int Comanda, Guid MediaId, string Title,
  string Artist, MediaKind Kind, TimeSpan Duration);
public sealed record ComandaCounts(int Pending, int RequestedTonight, bool HasPlaying);
public enum FinishReason { Played, Skipped, Failed }
public enum RemovalReason { Owner, Manager, Votes, Block, Unavailable }
Eventos: RequestAdded(venue, requestId, comanda, mediaId, kind) · RequestRemoved(venue, requestId, comanda, mediaId, reason)
         RequestStarted(venue, requestId, comanda, mediaId) · RequestFinished(venue, requestId, comanda, mediaId, reason)
```

**Arquivos/módulos afetados:** ver `touches` no frontmatter.

### 6.1 Mapa de Comportamentos
| Cenário | Condição / Entrada | Resultado esperado | Testes |
|---|---|---|---|
| Anti-monopólio | comanda 12 pede 3; depois comanda 7 pede 1 | ordem: 12(1ª), 7(1ª), 12(2ª), 12(3ª) | UT-01, IT-01 |
| Quem já tocou vai para depois | comanda 12 já tocou 2 na noite e pede 1; comanda 7 (nova) pede 1 | a da 7 (vez 1) antes da 12 (vez 3) | UT-01 |
| Pulada conta como vez | música da comanda 12 pulada pelo gerente | conta como tocada no cálculo da vez | UT-02 |
| Falha não conta | música da comanda 12 falhou (vídeo indisponível) | não conta como vez | UT-02 |
| Votos só dentro da vez | 7(1ª, saldo -2) e 9(1ª, saldo +3); 12(2ª, saldo +10) | 9 → 7 → 12 | UT-01 |
| Invariante de justiça | filas aleatórias (propriedades) | nenhum pedido de vez maior à frente de um de vez menor; ordem determinística | UT-03 |
| Pedido válido | participante, mídia tocável, dentro do limite | 201 com posição e vez; `RequestAdded` + `QueueChanged` | IT-01 |
| Limite por comanda | 4º pendente da comanda 12 (limite 3), de outro celular da mesma comanda | 422 `COMANDA_LIMIT_REACHED` | IT-02 |
| Tocando libera vaga | uma das 3 começa a tocar | a comanda pode pedir mais uma | IT-02 |
| Duplicada na fila | mesma música já pendente (qualquer comanda) | 409 `ALREADY_IN_QUEUE` | IT-02 |
| Repetição recente | música terminou há 30 min (janela 60) | 422 `RECENTLY_PLAYED`; com 61 min, aceita | IT-02 |
| Mídia inválida | indisponível / acima de 10 min / inexistente | 422 `MEDIA_UNAVAILABLE` / 422 `MEDIA_TOO_LONG` / 404 | IT-02 |
| Remover o próprio | dono remove pendente | 204; `RequestRemoved(owner)`; outra comanda → 403 `NOT_OWNER`; já tocando → 409 | IT-03 |
| Gerente remove | `DELETE /admin/queue/{id}` | 204; `RequestRemoved(manager)` notificado à comanda | IT-03 |
| Próxima para tocar | `StartNextAsync` | o primeiro da ordem vira `Playing`; `RequestStarted` | IT-04, CT-01 |
| Fim da música | `FinishAsync` com played, skipped ou failed | `Finished` com o motivo; `RequestFinished` | IT-04, CT-01 |
| Minhas | comanda com 2 pendentes e 1 tocada | `pendingCount=2`, lista de pendentes e de tocadas hoje | IT-03 |
| TV vê as próximas | `GET /tv/queue?take=3` | 3 primeiros da ordem, sem dados do cliente | IT-04 |
| Concorrência | 20 pedidos simultâneos, 5 da mesma comanda | no máximo 3 da comanda aceitos; nenhuma música duplicada | IT-05 |

## 7. Artefato B — Plano de Testes (TDD)

### 7.1 Testes de Caracterização
- N/A — funcionalidade nova.

### 7.2 Testes Unitários
- **UT-01** — Dado os cenários da tabela (anti-monopólio, quem já tocou, votos só dentro da vez), quando `FairQueuePolicy.Order` é aplicado, então a ordem resultante é exatamente a esperada em cada caso.
- **UT-02** — Dado o histórico da comanda com um `Finished(Skipped)`, um `Finished(Failed)` e um `Playing`, quando a vez do próximo pedido é calculada, então conta o Skipped e o Playing, mas não o Failed.
- **UT-03** — (Propriedades, CsCheck) Dado filas geradas aleatoriamente (até 60 pedidos, 15 comandas, histórico e saldos aleatórios), então: (a) a vez nunca diminui ao longo da ordem; (b) dentro da mesma vez, o saldo nunca aumenta; (c) a ordem é idêntica em duas execuções com a mesma entrada.

### 7.3 Testes de Integração
- **IT-01** — Com Postgres real, sessões das comandas 12 e 7 e mídias da semente: 12 pede 3 e 7 pede 1; `GET /queue` devolve a ordem 12, 7, 12, 12 com `round` 1, 1, 2, 3 e `secondsAhead` coerente; `RequestAdded` sai pelo outbox e `QueueChanged` é notificado com `queueVersion` maior.
- **IT-02** — Regras de pedido: 4º pendente da comanda 12 a partir de um segundo dispositivo → 422 `COMANDA_LIMIT_REACHED`; após `StartNextAsync` pegar uma da 12, o novo pedido é aceito; música já pendente → 409; música terminada há 30 min → 422 `RECENTLY_PLAYED` e há 61 min → 201 (FakeTimeProvider); mídia indisponível → 422; mídia de 11 min → 422 `MEDIA_TOO_LONG`; mídia inexistente → 404.
- **IT-03** — Dono remove pendente (204, `RequestRemoved(owner)`); comanda 7 tenta remover pedido da 12 → 403; remover o que está tocando → 409; gerente remove pelo admin (204, `RequestRemoved(manager)` notificado ao grupo da comanda); `GET /me/requests` reflete pendentes e tocadas hoje.
- **IT-04** — `StartNextAsync` marca o primeiro da ordem como `Playing` (e retorna nulo com a fila vazia); `FinishAsync` com cada motivo grava o `Finished` e publica `RequestFinished`; `GET /tv/queue?take=3` (token Tv) devolve os 3 primeiros sem `myVote`/`isMine`.
- **IT-05** — 20 `POST /queue` simultâneos (5 da mesma comanda e 2 da mesma música): no máximo 3 aceitos para a comanda e 1 para a música; p95 do `AddRequest` < 150 ms e do `GET` < 50 ms com 100 itens.

### 7.4 Testes de Contrato
- **CT-01** — Suíte de contrato de `IQueueModule` e snapshot dos schemas dos eventos `RequestAdded`, `RequestRemoved`, `RequestStarted` e `RequestFinished` e das respostas HTTP (OpenAPI); consumida pelas SPECs 0015, 0016, 0019, 0020, 0021, 0022, 0023 e 0024.

### 7.5 Testes E2E
- N/A — user_facing: false (a jornada do cliente é coberta pelo E2E da SPEC-0024).

### 7.6 Outros
- N/A.

**Dublês e dados de teste:** builders de `Request` e histórico; sessões pelo emissor de teste; mídias da semente do catálogo; `FakeTimeProvider`.

**Ambiente de execução:** Testcontainers (Postgres 18).

## 8. Plano de Rollout
- **Estratégia:** atrás da flag `JukeboxEnabled`.
- **Dados/schema:** migration aditiva `queue.Initial` (`requests`, `votes`, `queue_heads`, índices por (venue, night, status)).
- **Compatibilidade:** N/A.
- **Observabilidade:** `juker.queue.length` por bar, `juker.requests.added` por tipo, contador de rejeições por código.
- **Rollback:** redeploy anterior; tabelas novas.
- **Etapas de migração/coexistência:** N/A.

## 9. Questões em Aberto
- [x] Peso do voto — só reordena dentro da mesma vez (thomas, 2026-09-30)
- [x] Música pulada conta como vez — sim (thomas, 2026-09-30)
- [x] Repetição — mesma música não pode estar duas vezes na fila nem ser pedida nos 60 min após tocar, configurável (thomas, 2026-09-30)
- [x] Limite por comanda — 3 pendentes, compartilhado entre os celulares; a que está tocando deixa de contar, conforme o texto aprovado no design ("Assim que uma tocar, você pode pedir outra") (thomas, 2026-09-30)

## 10. Aprovação (H1)
Registrada no frontmatter (`approved_by`, `approved_at`) somente depois que o humano responder "Aprovado". O arquiteto nunca aprova a própria spec.

## 11. Checklist de Implementação

**Fase 1: Regras e componentes (unitário)**
- [ ] Red: escrever UT-01, UT-02, UT-03 com a tag `SPEC-0018:<ID>` e confirmar que falham pelo motivo certo
- [ ] Green: implementar o mínimo para passar, seguindo os ADRs de fundação e o padrão de slices
- [ ] Refactor mantendo tudo verde
- [ ] Validar: build + lint + suíte de UT (G2)

**Fase 2: Integração e contrato**
- [ ] Red: escrever IT-01, IT-02, IT-03, IT-04, IT-05, CT-01 com a tag `SPEC-0018:<ID>` (Postgres real, esquemas de auth reais, `DrainOutboxAsync`, WireMock — ADR-0012) e confirmar que falham pelo motivo certo
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
| G0 Spec | PASS | `spec_graph.py validate SPEC-0018` → G0 [SPEC-0018]: PASS — 0 erro(s), 0 aviso(s); checklist de julgamento do G0 revisado pelo Architect | 2026-09-30 |
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
