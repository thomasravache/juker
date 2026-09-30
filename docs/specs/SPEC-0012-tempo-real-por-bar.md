---
id: SPEC-0012
title: Tempo real por bar
tier: full
type: feature
user_facing: false
status: approved
created: 2026-09-30
parent: SPEC-0010
depends_on: [SPEC-0008, SPEC-0009]
consumes_contract: []
contract_version: 1
touches: [src/BuildingBlocks/Juker.Infrastructure/Realtime/**, src/Juker.Host/Realtime/**, src/Web/Juker.Web.Client/Realtime/**, tests/Juker.IntegrationTests/Realtime/**, tests/Juker.Web.UnitTests/Realtime/**]
adrs: [ADR-0005, ADR-0008]
external: []
size: M
approved_by: thomas
approved_at: 2026-09-30
---

# SPEC-0012 — Tempo real por bar

## 1. Visão Geral
Hub SignalR único (`/hubs/venue`) que conecta celulares, TV e painel de cada bar, com grupos por público (clientes, comanda, TV, equipe), o notificador que os módulos usam para avisar mudanças e o cliente de conexão do `Juker.Web.Client` com reconexão automática e releitura do estado. O servidor só **avisa** que algo mudou (com uma versão); quem recebe relê pelo HTTP. O servidor é sempre a fonte da verdade.

## 2. Motivação & Escopo
**Motivação:** o diferencial "a galera vota e a fila se mexe na hora" depende de todos os aparelhos refletirem a mudança em menos de 1 s, mesmo com a rede ruim do bar.

**Objetivos (dentro do escopo):**
- Hub `/hubs/venue` aceitando os esquemas Guest, Tv e Staff (SPEC-0009); ao conectar, entra nos grupos certos pelo principal.
- Grupos: `v:{venueId}:all`, `v:{venueId}:guests`, `v:{venueId}:c:{comanda}`, `v:{venueId}:tv`, `v:{venueId}:staff`.
- `IVenueNotifier` (Infrastructure) com um método por evento do contrato, usado pelos módulos (chamado pelos consumidores dos eventos de integração ou após o commit).
- `VenueConnection` no Web.Client: reconexão em 0, 2, 5, 10 e 30 s, repetindo a cada 30 s; evento `Resynced` ao reconectar (as páginas relêem tudo); estado `Reconnecting` exposto para o banner "Reconectando…" depois de 5 s sem conexão.
- Números de versão monotônicos por bar e por tipo (`queueVersion`, `playbackVersion`) para descartar notificações fora de ordem.

**Não-objetivos (fora do escopo):**
- Os eventos de negócio em si (emitidos pelas specs de cada módulo).
- Backplane para várias instâncias (ADR-0005).
- Mensagens do cliente para o servidor pelo hub (comandos vão por HTTP; a TV reporta por HTTP, SPEC-0020).

## 3. Dependências
- **Implementações necessárias:** SPEC-0009 — esquemas Guest, Tv e Staff e `ITenantContext`.
- **Contratos consumidos:** N/A.
- **Pré-requisitos externos:** N/A.

## 4. Decisão Arquitetural
**Contexto:** projeto novo; ADR-0005 (SignalR, instância única) e ADR-0008 (identidade).

**Decisão:** um hub por aplicação, com grupos por bar; payloads mínimos (tipo + versão + IDs), releitura por HTTP.

**Justificativa:** payload mínimo evita divergência entre o que chega pelo hub e o que a API responde, e torna a reconexão trivial.

**Desvio do padrão existente:** Nenhum.

**Alternativas descartadas:** enviar a fila inteira por evento (payload grande no 4G e risco de estado divergente); um hub por módulo (mais conexões por aparelho).

**ADRs:** ADR-0005, ADR-0008.

## 5. Requisitos Não-Funcionais
- **Desempenho e escala:** do `Notify*` até o recebimento em 50 conexões simultâneas do mesmo bar, p95 < 1 s — IT-02; suportar 500 conexões por bar em uma instância — IT-02 com carga de 500 conexões (NBomber ou clientes em paralelo).
- **Segurança:** conexão sem autenticação é recusada; um cliente nunca entra em grupo de outro bar nem no grupo `staff` — IT-01.
- **Privacidade e dados pessoais:** payloads só com IDs, números de comanda e versões — CT-01.
- **Disponibilidade e resiliência:** após queda de rede, o cliente reconecta e dispara `Resynced` — UT-01 e IT-03.
- **Acessibilidade (UI):** o banner "Reconectando…" usa `role="status"` — UT-02.
- **Custo:** N/A.

## 6. Artefato A — Contrato
**Interface:** hub, grupos, eventos servidor → cliente e API do cliente.

```text
Hub: /hubs/venue   (auth: Guest | Tv | Staff)
Grupos ao conectar:
  Guest  → v:{id}:all, v:{id}:guests, v:{id}:c:{comanda}
  Tv     → v:{id}:all, v:{id}:tv
  Staff  → v:{id}:all, v:{id}:staff

Eventos (servidor → cliente), todos com { venueId, version }:
  QueueChanged            → v:{id}:all            { queueVersion }
  NowPlayingChanged       → v:{id}:all            { playbackVersion, requestId|null }
  PlaybackStateChanged    → v:{id}:all            { playbackVersion, state: "playing"|"paused"|"idle" }
  RequestRemoved          → v:{id}:c:{comanda}    { requestId, reason: "owner"|"manager"|"votes"|"block"|"unavailable" }
  ComandaModeChanged      → v:{id}:c:{comanda}    { mode: "participant"|"listener" }
  TvStatusChanged         → v:{id}:staff          { online: bool }
  PinRotated              → v:{id}:tv, v:{id}:staff  { expiresAt }
  VenueSettingsChanged    → v:{id}:all            { settingsVersion }

public interface IVenueNotifier {
  Task QueueChanged(VenueId v, long version); Task NowPlayingChanged(VenueId v, long version, Guid? requestId);
  Task PlaybackStateChanged(VenueId v, long version, PlaybackState s);
  Task RequestRemoved(VenueId v, int comanda, Guid requestId, RemovalReason r);
  Task ComandaModeChanged(VenueId v, int comanda, ComandaMode m);
  Task TvStatusChanged(VenueId v, bool online); Task PinRotated(VenueId v, DateTimeOffset expiresAt);
  Task VenueSettingsChanged(VenueId v, long version);
}

Web.Client:
  public sealed class VenueConnection : IAsyncDisposable {
    ConnectionStatus Status;              // Connected | Reconnecting | Disconnected
    event Action Resynced;                // após reconectar
    IDisposable On<TEvent>(Action<TEvent> handler);   // descarta versão menor que a última vista
    Task StartAsync(CancellationToken ct);
  }
```

**Arquivos/módulos afetados:** ver `touches` no frontmatter.

### 6.1 Mapa de Comportamentos
| Cenário | Condição / Entrada | Resultado esperado | Testes |
|---|---|---|---|
| Conexão anônima | sem cookie/token | conexão recusada (401) | IT-01 |
| Grupos por principal | cliente da comanda 12, TV e gerente do bar A | cada um recebe só os eventos dos seus grupos; ninguém recebe eventos do bar B | IT-01 |
| Latência | `QueueChanged` com 50 clientes conectados | p95 recebimento < 1 s; 500 conexões suportadas | IT-02 |
| Reconexão | conexão derrubada e rede restabelecida | status `Reconnecting` → `Connected`; `Resynced` disparado uma vez | UT-01, IT-03 |
| Evento fora de ordem | chega versão 5 depois da 7 | versão 5 é descartada | UT-01 |
| Banner de reconexão | 5 s em `Reconnecting` | banner "Reconectando…" com `role="status"` aparece; some ao reconectar | UT-02 |
| Contrato de eventos | cada método do `IVenueNotifier` | payload com os campos do contrato e sem dados pessoais | CT-01 |

## 7. Artefato B — Plano de Testes (TDD)

### 7.1 Testes de Caracterização
- N/A — funcionalidade nova.

### 7.2 Testes Unitários
- **UT-01** — Dado um `VenueConnection` com transporte fake, quando a conexão cai e volta, então o status passa por `Reconnecting` → `Connected` e `Resynced` dispara uma vez; e um `QueueChanged` com versão menor que a última vista é descartado.
- **UT-02** — Dado o componente `ReconnectBanner` (bUnit) e o status `Reconnecting` por 5 s (FakeTimeProvider), então exibe "Reconectando…" com `role="status"`; com `Connected`, não exibe.

### 7.3 Testes de Integração
- **IT-01** — Com o host real: conexão sem auth é recusada; cliente (comanda 12, bar A), TV (bar A), gerente (bar A) e cliente do bar B conectados; cada `Notify*` é recebido só pelos grupos do contrato.
- **IT-02** — 50 clientes do mesmo bar recebem 100 `QueueChanged` com p95 < 1 s; depois, 500 conexões simultâneas se mantêm por 60 s sem erro.
- **IT-03** — Cliente real do Web.Client (HubConnection) com o host reiniciado no meio: reconecta sozinho e dispara `Resynced`.

### 7.4 Testes de Contrato
- **CT-01** — Snapshot dos payloads JSON de cada evento (nome e campos) aprovado em `tests/Juker.IntegrationTests/Realtime/events.approved.json`; consumido pelas SPECs 0013, 0018, 0019, 0020, 0021, 0022 e 0023.

### 7.5 Testes E2E
- N/A — user_facing: false (as jornadas que usam o tempo real têm E2E nas specs de cada tela).

### 7.6 Outros
- Carga: o IT-02 registra a latência p95 e o consumo de memória com 500 conexões no Relatório de Entrega.

**Dublês e dados de teste:** transporte fake do SignalR para UT; emissores de teste de sessão/token (SPEC-0009).

**Ambiente de execução:** host real em memória (TestServer com WebSockets) para IT; carga local.

## 8. Plano de Rollout
- **Estratégia:** deploy direto; nenhum evento de negócio ainda.
- **Dados/schema:** N/A.
- **Compatibilidade:** novos eventos podem ser adicionados; mudar campos existentes exige emenda (CT-01).
- **Observabilidade:** gauge `juker.realtime.connections` por bar e tipo de principal; log de conexões recusadas.
- **Rollback:** redeploy anterior.
- **Etapas de migração/coexistência:** N/A.

## 9. Questões em Aberto
Nenhuma.

## 10. Aprovação (H1)
Registrada no frontmatter (`approved_by`, `approved_at`) somente depois que o humano responder "Aprovado". O arquiteto nunca aprova a própria spec.

## 11. Checklist de Implementação

**Fase 1: Regras e componentes (unitário)**
- [ ] Red: escrever UT-01, UT-02 com a tag `SPEC-0012:<ID>` e confirmar que falham pelo motivo certo
- [ ] Green: implementar o mínimo para passar, seguindo os ADRs de fundação e o padrão de slices
- [ ] Refactor mantendo tudo verde
- [ ] Validar: build + lint + suíte de UT (G2)

**Fase 2: Integração e contrato**
- [ ] Red: escrever IT-01, IT-02, IT-03, CT-01 com a tag `SPEC-0012:<ID>` (Postgres real, esquemas de auth reais, `DrainOutboxAsync`, WireMock — ADR-0012) e confirmar que falham pelo motivo certo
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
| G0 Spec | PASS | `spec_graph.py validate SPEC-0012` → G0 [SPEC-0012]: PASS — 0 erro(s), 0 aviso(s); checklist de julgamento do G0 revisado pelo Architect | 2026-09-30 |
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
