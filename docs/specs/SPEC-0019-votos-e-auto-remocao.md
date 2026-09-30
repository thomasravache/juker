---
id: SPEC-0019
title: Votos e auto-remoção
tier: full
type: feature
user_facing: true
status: approved
created: 2026-09-30
parent: SPEC-0010
depends_on: [SPEC-0024]
consumes_contract: []
contract_version: 1
touches: [src/Modules/Queue/Juker.Modules.Queue/Features/Vote/**, src/Modules/Queue/Juker.Modules.Queue/Domain/Voting/**, src/Web/Juker.Web.Client/Guest/Queue/Vote*, tests/Juker.Modules.Queue.UnitTests/Voting/**, tests/Juker.IntegrationTests/Queue/Voting/**, tests/Juker.Web.UnitTests/Guest/Queue/Vote*, tests/Juker.E2ETests/Guest/Voting/**]
adrs: [ADR-0006]
external: []
size: M
approved_by: thomas
approved_at: 2026-09-30
---

# SPEC-0019 — Votos e auto-remoção

## 1. Visão Geral
O cliente vota positivo ou negativo nas músicas das **outras** comandas que estão na fila. Cada comanda tem um voto por música (os celulares da mesma comanda compartilham o voto). O saldo reordena a fila **dentro da mesma vez**, e, quando chega a **-5** (configurável), a música sai da fila sozinha e a comanda dona é avisada.

## 2. Motivação & Escopo
**Motivação:** "se a galera curtir, a música sobe" — sem permitir que uma comanda popular monopolize e sem o gerente precisar agir contra música que ninguém quer.

**Objetivos (dentro do escopo):**
- `PUT /api/v1/b/{slug}/queue/{requestId}/vote` com valor 1, -1 ou 0 (retirar); só pedidos `Pending`; não pode votar em pedido da própria comanda; um voto por (pedido, comanda) — o último celular a votar define o voto da comanda.
- Saldo = soma dos votos por comanda; exposto em `score` e `myVote` na fila (contrato da SPEC-0018).
- Auto-remoção quando `score ≤ AutoRemoveScore` (padrão -5): `Removed(Votes)`, evento `RequestRemoved(votes)`, notificação à comanda dona.
- Notificação `QueueChanged` a cada voto que muda o saldo.
- Botões de voto na tela Fila (Stitch): seta para cima/baixo com o saldo no meio, estados ativos (verde/vermelho com brilho), saldo colorido (verde positivo, vermelho negativo); sem botões nos itens da própria comanda.
- Rate limit de votos por sessão (política `per-guest` da SPEC-0009).

**Não-objetivos (fora do escopo):**
- Votos da comanda bloqueada (SPEC-0022: bloqueado não vota e seus votos são anulados).
- Voto na música que está tocando.
- Votos mudando de vez (decisão do fundador: só reordenam dentro da vez).

## 3. Dependências
- **Implementações necessárias:** SPEC-0024 — tela Fila onde os botões entram (e, por ela, a SPEC-0018 com a tabela `votes` e a política de ordenação).
- **Contratos consumidos:** N/A (usa os contratos já implementados pelas dependências).
- **Pré-requisitos externos:** N/A.

## 4. Decisão Arquitetural
**Contexto:** projeto novo; slice novo no módulo Queue, com o evento via outbox (ADR-0006).

**Decisão:** slice `Features/Vote` com a regra em `Domain/Voting` (`VotePolicy`) e o upsert do voto sob o mesmo lock por bar da SPEC-0018.

**Justificativa:** o lock por bar evita corrida entre dois votos que cruzam o limite de auto-remoção ao mesmo tempo.

**Desvio do padrão existente:** Nenhum.

**Alternativas descartadas:** voto por dispositivo (quebraria a regra de 1 voto por comanda).

**ADRs:** ADR-0006.

## 5. Requisitos Não-Funcionais
- **Desempenho e escala:** `PUT vote` p95 < 100 ms; voto refletido nos outros aparelhos em < 1 s (SPEC-0012) — IT-04.
- **Segurança:** a comanda vem da sessão; votar no próprio pedido → 403 — IT-01.
- **Privacidade e dados pessoais:** quem votou não é exposto a ninguém (só o saldo e o `myVote` da própria comanda).
- **Disponibilidade e resiliência:** votos simultâneos no limite geram exatamente uma remoção — IT-03.
- **Acessibilidade (UI):** botões com rótulo "Curtir"/"Não curtir" e `aria-pressed`; saldo anunciado — UT-02 e E2E-01 (axe).
- **Custo:** N/A.

## 6. Artefato A — Contrato
**Interface:** endpoint de voto e comportamento de auto-remoção.

```text
PUT /api/v1/b/{slug}/queue/{requestId}/vote   { "value": 1 | -1 | 0 }     (GuestOfVenue, participante)
  200 { "requestId": "…", "score": 3, "myVote": 1, "removed": false }
  200 { "requestId": "…", "score": -5, "myVote": -1, "removed": true }   (auto-remoção)
  400 VOTE_VALUE_INVALID
  403 CANNOT_VOTE_OWN        "Não dá para votar na música da sua própria comanda."
  409 NOT_PENDING            "Essa música não está mais na fila."
  404 REQUEST_NOT_FOUND
Auto-remoção: score ≤ VenueRules.AutoRemoveScore → Request.Removed(Votes) + RequestRemoved(votes) + QueueChanged
UI (item de outra comanda): [▲ Curtir] score [▼ Não curtir]; ativo: verde (▲) / vermelho (▼)
```

**Arquivos/módulos afetados:** ver `touches` no frontmatter.

### 6.1 Mapa de Comportamentos
| Cenário | Condição / Entrada | Resultado esperado | Testes |
|---|---|---|---|
| Saldo por comanda | 2 celulares da comanda 7 votam +1 e depois -1 | um voto da comanda 7 (o último, -1) | UT-01, IT-02 |
| Retirar voto | valor 0 | voto removido; saldo recalculado | UT-01, IT-02 |
| Próprio pedido | comanda 12 vota no pedido da 12 | 403 `CANNOT_VOTE_OWN` | IT-01 |
| Não pendente | votar no que está tocando ou já saiu | 409 `NOT_PENDING` | IT-01 |
| Valor inválido | valor 2 | 400 `VOTE_VALUE_INVALID` | IT-01 |
| Reordena dentro da vez | dois pedidos de vez 1; o segundo recebe +2 | o segundo passa à frente; pedido de vez 2 com +10 continua atrás | IT-02, E2E-01 |
| Auto-remoção | 5 comandas votam -1 (limite -5) | `removed=true`; sai da fila; dono recebe o toast "Sua música saiu da fila pelos votos da galera." | UT-02, IT-03, E2E-01 |
| Limite configurado | `AutoRemoveScore=-3` | remove no 3º voto negativo | IT-03 |
| Corrida no limite | 2 votos -1 simultâneos cruzando o limite | uma única remoção e um único evento | IT-03 |
| Tempo real | voto de outro celular | saldo atualizado nos demais em < 1 s | IT-04 |
| Botões | item de outra comanda / item meu | botões com `aria-pressed` / sem botões | UT-03 |

## 7. Artefato B — Plano de Testes (TDD)

### 7.1 Testes de Caracterização
- N/A — funcionalidade nova.

### 7.2 Testes Unitários
- **UT-01** — Dado votos por comanda {7:+1, 9:+1, 11:-1} e a comanda 7 mudando para -1 e a 9 retirando (0), quando `VotePolicy.Apply` roda, então o saldo passa de 1 para -2.
- **UT-02** — Dado `AutoRemoveScore = -5` e saldo -4, quando chega mais um -1, então a decisão é remover (`removed=true`); com saldo -3 e o limite em -5, não remove.
- **UT-03** — Dado o `VoteControl` (bUnit) com `myVote=1` e `score=3`, então "Curtir" tem `aria-pressed="true"`, o saldo é verde e "+3"; com `isMine=true`, o controle não é renderizado.

### 7.3 Testes de Integração
- **IT-01** — Com Postgres real: voto no próprio pedido → 403; no pedido que está tocando → 409; valor 2 → 400; pedido inexistente → 404.
- **IT-02** — Dois dispositivos da comanda 7 votam +1 e depois -1: o saldo tem um único voto da 7 (-1); valor 0 retira; a fila (`GET /queue`) reordena só dentro da mesma vez.
- **IT-03** — Com o limite -5, cinco comandas votam -1: na quinta, `removed=true`, `RequestRemoved(votes)` sai pelo outbox e a notificação vai para o grupo da comanda dona; com `AutoRemoveScore=-3`, remove no terceiro; dois votos simultâneos que cruzam o limite geram uma única remoção.
- **IT-04** — Com clientes do hub conectados, cada voto que muda o saldo gera `QueueChanged` recebido em < 1 s; p95 do `PUT vote` < 100 ms.

### 7.4 Testes de Contrato
- **CT-01** — Snapshot OpenAPI do endpoint de voto e da forma do `score`/`myVote` na fila (acréscimo compatível ao contrato da SPEC-0018); consumido pela SPEC-0023 (painel mostra os votos).

### 7.5 Testes E2E
- **E2E-01** — Comandas 7 e 9 pedem uma música cada (mesma vez); a comanda 12 curte a música da 9 pela tela e a vê passar à frente da 7; depois, a música da 7 recebe 5 votos negativos (4 pela API de teste e 1 pela tela da comanda 12): ela sai da fila em todos os celulares e o celular da comanda 7 mostra "Sua música saiu da fila pelos votos da galera."; a tela não tem violações axe.

### 7.6 Outros
- N/A.

**Dublês e dados de teste:** sessões de várias comandas pelo emissor de teste; configurações do bar por builder.

**Ambiente de execução:** Testcontainers (IT); AppHost + Playwright (E2E).

## 8. Plano de Rollout
- **Estratégia:** atrás da flag `JukeboxEnabled`.
- **Dados/schema:** N/A — a tabela `votes` foi criada pela SPEC-0018; índice único (request_id, comanda) garantido aqui se ainda não existir (migration aditiva).
- **Compatibilidade:** acréscimo compatível na resposta da fila.
- **Observabilidade:** contador `juker.votes` por valor e `juker.requests.removed{reason=votes}`.
- **Rollback:** redeploy anterior; ou `AutoRemoveScore=-20` para praticamente desligar a auto-remoção.
- **Etapas de migração/coexistência:** N/A.

## 9. Questões em Aberto
- [x] Auto-remoção por votos — sim, saldo ≤ -5, configurável (thomas, 2026-09-30)
- [x] Votos de vários celulares da mesma comanda — 1 voto por música por comanda (thomas, 2026-09-30)

## 10. Aprovação (H1)
Registrada no frontmatter (`approved_by`, `approved_at`) somente depois que o humano responder "Aprovado". O arquiteto nunca aprova a própria spec.

## 11. Checklist de Implementação

**Fase 1: Regras e componentes (unitário)**
- [ ] Red: escrever UT-01, UT-02, UT-03 com a tag `SPEC-0019:<ID>` e confirmar que falham pelo motivo certo
- [ ] Green: implementar o mínimo para passar, seguindo os ADRs de fundação e o padrão de slices
- [ ] Refactor mantendo tudo verde
- [ ] Validar: build + lint + suíte de UT (G2)

**Fase 2: Integração e contrato**
- [ ] Red: escrever IT-01, IT-02, IT-03, IT-04, CT-01 com a tag `SPEC-0019:<ID>` (Postgres real, esquemas de auth reais, `DrainOutboxAsync`, WireMock — ADR-0012) e confirmar que falham pelo motivo certo
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
| G0 Spec | PASS | `spec_graph.py validate SPEC-0019` → G0 [SPEC-0019]: PASS — 0 erro(s), 0 aviso(s); checklist de julgamento do G0 revisado pelo Architect | 2026-09-30 |
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
