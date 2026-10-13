---
id: SPEC-0016
title: Manutenção do catálogo
tier: full
type: feature
user_facing: false
status: approved
created: 2026-09-30
parent: SPEC-0010
depends_on: [SPEC-0014]
consumes_contract: [SPEC-0020@1, SPEC-0018@1]
contract_version: 1
touches: [src/Modules/Catalog/Juker.Modules.Catalog/Features/Refresh/**, src/Modules/Catalog/Juker.Modules.Catalog/Features/Quality/**, src/Modules/Catalog/Juker.Modules.Catalog/Features/Seed/**, src/Modules/Catalog/Juker.Modules.Catalog/Infrastructure/Persistence/Migrations/**, seeds/**, tests/Juker.Modules.Catalog.UnitTests/Maintenance/**, tests/Juker.IntegrationTests/Catalog/Maintenance/**]
adrs: [ADR-0007]
external: []
size: M
approved_by: thomas
approved_at: 2026-09-30
---

# SPEC-0016 — Manutenção do catálogo

## 1. Visão Geral
Mantém o catálogo saudável e em conformidade: atualiza os dados vindos da API antes de completarem 30 dias, marca como indisponível o vídeo que foi removido ou teve o embed desligado, apaga os dados da API que não puderam ser atualizados, ajusta a **qualidade** de cada mídia pelos sinais do bar (tocou até o fim, foi pulada pelo gerente, saiu por votos, deu erro na TV) e **semeia** o catálogo antes do piloto com uma lista curada de músicas de bar de rock, respeitando a cota diária.

## 2. Motivação & Escopo
**Motivação:** sem manutenção, a TV tentaria tocar vídeos que não existem mais, o catálogo violaria a regra dos 30 dias do YouTube e a primeira noite do piloto dependeria de buscas externas.

**Objetivos (dentro do escopo):**
- Job diário (05:00 America/Sao_Paulo): seleciona mídias com `FetchedAt` com mais de 25 dias, em lotes de 50 via `IMediaProvider.GetDetailsAsync` (1 unidade por lote), usando a reserva de manutenção; atualiza título, canal, duração, embed e miniatura; vídeo inexistente, privado ou sem embed → `Unavailable`.
- Dados da API com mais de 30 dias que não puderam ser atualizados (cota) → `RawTitle`, `ChannelTitle` e `ThumbnailUrl` apagados; a mídia fica `Unavailable` até ser atualizada (a identidade `Song` e o `MediaRef` permanecem).
- Consumidores de eventos:
  - `PlaybackFailed` (SPEC-0020): `Unavailable`/`EmbedNotAllowed` → mídia `Unavailable` na hora; `Unknown` → qualidade -1.
  - `PlaybackFinished` (SPEC-0020): tocou até o fim → +1; pulada pelo gerente → -2.
  - `RequestRemoved` com motivo `votes` (SPEC-0018) → -3.
  - `QualityScore` limitado a [-20, +20].
- Semeadura: `juker admin seed-catalog --file seeds/rock-bar.csv [--daily-units 5000]` cria uma fila de semeadura (artista; título; tipos `clip|karaoke|both`) processada em segundo plano dentro do orçamento diário, retomável; `juker admin seed-status` mostra o progresso.
- Arquivo inicial `seeds/rock-bar.csv` com ~300 músicas clássicas de bar de rock (nacionais e internacionais), revisado pelo fundador antes do piloto.

**Não-objetivos (fora do escopo):**
- Correção manual do tipo de mídia pelo gerente (fora do MVP).
- Curadoria automática por popularidade externa.

## 3. Dependências
- **Implementações necessárias:** SPEC-0014 — `IMediaProvider`, `QuotaLedger` (reserva de manutenção) e ingestão.
- **Contratos consumidos:** SPEC-0020@1 (eventos `PlaybackFailed`/`PlaybackFinished`); SPEC-0018@1 (evento `RequestRemoved` com motivo).
- **Pré-requisitos externos:** revisão da lista `seeds/rock-bar.csv` pelo fundador.

## 4. Decisão Arquitetural
**Contexto:** projeto novo; ADR-0007 (regra de 30 dias, catálogo próprio).

**Decisão:** slices `Features/Refresh` (job), `Features/Quality` (consumidores idempotentes via inbox) e `Features/Seed` (CLI + processador em segundo plano) no módulo Catalog.

**Justificativa:** a manutenção fica isolada no módulo dono dos dados, e os sinais de qualidade chegam por eventos, sem acoplar Playback/Queue ao Catalog.

**Desvio do padrão existente:** Nenhum.

**Alternativas descartadas:** atualizar só sob demanda na hora do pedido (a TV já receberia o vídeo quebrado); semear manualmente pela interface (lento e sujeito a erro).

**ADRs:** ADR-0007.

## 5. Requisitos Não-Funcionais
- **Desempenho e escala:** atualização de 10.000 mídias usa ≤ 200 unidades por dia (lotes de 50) — IT-01.
- **Segurança:** CLI só roda no servidor (acesso SSH do fundador).
- **Privacidade e dados pessoais:** N/A — sem dados de pessoas.
- **Disponibilidade e resiliência:** job e semeadura param sozinhos quando a reserva do dia acaba e continuam no dia seguinte, sem gastar a cota das buscas dos clientes — IT-03.
- **Acessibilidade (UI):** N/A — sem telas.
- **Custo:** dentro da cota gratuita (reserva de 1.000 unidades/dia; a semeadura só usa o saldo que sobra ao fim do dia, ou o `--daily-units` antes do piloto).

## 6. Artefato A — Contrato
**Interface:** job de atualização, regras de qualidade, CLI de semeadura e formato do arquivo.

```text
Job "catalog-refresh": diário 05:00 America/Sao_Paulo
  seleção: Status=Available && FetchedAt < agora-25d, ordem FetchedAt asc, lotes de 50
  retenção: FetchedAt < agora-30d && não atualizada → apaga RawTitle/ChannelTitle/ThumbnailUrl, Status=Unavailable

Qualidade (QualityScore ∈ [-20, +20]):
  PlaybackFinished(reason=played) +1 · PlaybackFinished(reason=skipped) -2
  RequestRemoved(reason=votes) -3 · PlaybackFailed(code=unknown) -1
  PlaybackFailed(code=unavailable|embed_not_allowed) → Status=Unavailable

CLI:
  juker admin seed-catalog --file seeds/rock-bar.csv [--daily-units N]   → "N itens enfileirados"
  juker admin seed-status → "Semeadura: 120/300 concluídos, 3 sem resultado, próximo lote amanhã"
Arquivo CSV (UTF-8, separador ';'): artista;titulo;tipos      ex.: Raul Seixas;Maluco Beleza;both
```

**Arquivos/módulos afetados:** ver `touches` no frontmatter.

### 6.1 Mapa de Comportamentos
| Cenário | Condição / Entrada | Resultado esperado | Testes |
|---|---|---|---|
| Regras de qualidade | sequência de sinais | score correto e limitado a [-20, +20] | UT-01 |
| Seleção para atualizar | mídias de 10, 26 e 31 dias | só as de 26 e 31 selecionadas, mais antigas primeiro | UT-02 |
| Atualização | lote com um vídeo removido e um sem embed | ambos `Unavailable`; os demais atualizados com `FetchedAt` novo | IT-01 |
| Retenção de 30 dias | mídia de 31 dias sem cota para atualizar | dados da API apagados e `Unavailable` | IT-02 |
| Erro na TV | `PlaybackFailed(unavailable)` | mídia `Unavailable` imediatamente, fora da busca | IT-04 |
| Sinais do bar | `PlaybackFinished` played/skipped e `RequestRemoved` votes | score +1/-2/-3; evento repetido não conta duas vezes (inbox) | IT-04 |
| Semeadura no orçamento | 300 itens com `--daily-units 1010` | 10 buscas no dia, o resto fica para os próximos dias; retomada após reinício | IT-03 |
| Semeadura sem resultado | item que não acha vídeo tocável | marcado "sem resultado", não trava a fila | IT-03 |
| Arquivo inválido | linha sem título | CLI rejeita com "Linha 12: título obrigatório" | UT-03 |
| Contrato dos eventos consumidos | schemas de `PlaybackFailed`, `PlaybackFinished` e `RequestRemoved` | consumidores aceitam os payloads dos contratos | CT-01 |

## 7. Artefato B — Plano de Testes (TDD)

### 7.1 Testes de Caracterização
- N/A — funcionalidade nova.

### 7.2 Testes Unitários
- **UT-01** — Dado uma mídia com score 19, quando recebe `played` duas vezes, então fica 20; dado score -19, quando recebe `votes`, então fica -20.
- **UT-02** — Dado mídias com `FetchedAt` de 10, 26 e 31 dias atrás, quando `RefreshSelector.Select` roda, então retorna a de 31 e a de 26, nessa ordem.
- **UT-03** — Dado um CSV com uma linha sem título na linha 12, quando `SeedFileParser.Parse` roda, então retorna o erro "Linha 12: título obrigatório"; e linhas válidas com `both` geram dois itens (clip e karaoke).

### 7.3 Testes de Integração
- **IT-01** — Com Postgres real e o `FakeHttpServer`: 120 mídias vencidas são atualizadas em 3 lotes (3 unidades no `QuotaLedger`); um vídeo removido e um sem embed viram `Unavailable`.
- **IT-02** — Mídia com 31 dias e reserva de manutenção esgotada: após o job, título/canal/miniatura ficam nulos e o status `Unavailable`; a `Song` continua existindo.
- **IT-03** — Semeadura de 300 itens com `--daily-units 1010`: no primeiro dia, 10 buscas; o processo é reiniciado e continua de onde parou no dia seguinte (FakeTimeProvider); um item sem resultado fica marcado e o processamento segue.
- **IT-04** — Eventos `PlaybackFailed(unavailable)`, `PlaybackFinished(played)`, `PlaybackFinished(skipped)` e `RequestRemoved(votes)` entregues pelo outbox ajustam status e score; o mesmo evento entregue duas vezes conta uma vez.

### 7.4 Testes de Contrato
- **CT-01** — Os consumidores validam os payloads contra os schemas publicados por SPEC-0020@1 (`PlaybackFailed`, `PlaybackFinished`) e SPEC-0018@1 (`RequestRemoved`).

### 7.5 Testes E2E
- N/A — jobs de manutenção do catálogo em segundo plano; nenhuma tela ou API pública muda.

### 7.6 Outros
- Antes do piloto: executar a semeadura em staging e em produção com a lista revisada e registrar o resultado do `seed-status` (itens concluídos e sem resultado).

**Dublês e dados de teste:** `FakeHttpServer` com respostas de `videos.list` (existente, removido, sem embed); eventos construídos pelos schemas dos contratos.

**Ambiente de execução:** Testcontainers + WireMock.Net.

## 8. Plano de Rollout
- **Estratégia:** deploy direto; o job começa a rodar no deploy; a semeadura é disparada manualmente pelo fundador.
- **Dados/schema:** migration aditiva `catalog.Maintenance` (`seed_items`, índices em `fetched_at`).
- **Compatibilidade:** N/A.
- **Observabilidade:** contadores `juker.catalog.refreshed`, `juker.catalog.unavailable`, `juker.catalog.seed.pending`; log de resumo do job.
- **Rollback:** redeploy anterior; o job é idempotente.
- **Etapas de migração/coexistência:** N/A.

## 9. Questões em Aberto
Nenhuma.

## 10. Aprovação (H1)
Registrada no frontmatter (`approved_by`, `approved_at`) somente depois que o humano responder "Aprovado". O arquiteto nunca aprova a própria spec.

## 11. Checklist de Implementação

**Fase 1: Regras e componentes (unitário)**
- [ ] Red: escrever UT-01, UT-02, UT-03 com a tag `SPEC-0016:<ID>` e confirmar que falham pelo motivo certo
- [ ] Green: implementar o mínimo para passar, seguindo os ADRs de fundação e o padrão de slices
- [ ] Refactor mantendo tudo verde
- [ ] Validar: build + lint + suíte de UT (G2)

**Fase 2: Integração e contrato**
- [ ] Red: escrever IT-01, IT-02, IT-03, IT-04, CT-01 com a tag `SPEC-0016:<ID>` (Postgres real, esquemas de auth reais, `DrainOutboxAsync`, WireMock — ADR-0012) e confirmar que falham pelo motivo certo
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
| G0 Spec | PASS | `spec_graph.py validate SPEC-0016` → G0 [SPEC-0016]: PASS — 0 erro(s), 0 aviso(s); checklist de julgamento do G0 revisado pelo Architect | 2026-09-30 |
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
