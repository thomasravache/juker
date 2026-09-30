---
id: SPEC-0014
title: Busca externa no YouTube com controle de cota
tier: full
type: feature
user_facing: true
status: approved
created: 2026-09-30
parent: SPEC-0010
depends_on: [SPEC-0015]
consumes_contract: [SPEC-0008@1]
contract_version: 1
touches: [src/Modules/Catalog/Juker.Modules.Catalog/Domain/Providers/**, src/Modules/Catalog/Juker.Modules.Catalog/Domain/Classification/**, src/Modules/Catalog/Juker.Modules.Catalog/Features/ExternalSearch/**, src/Modules/Catalog/Juker.Modules.Catalog/Infrastructure/YouTube/**, src/Modules/Catalog/Juker.Modules.Catalog/Infrastructure/Quota/**, src/Modules/Catalog/Juker.Modules.Catalog/Infrastructure/Persistence/Migrations/**, src/Web/Juker.Web.Client/Guest/Search/External/**, deploy/observability/alerts.yaml, tests/Juker.Modules.Catalog.UnitTests/ExternalSearch/**, tests/Juker.IntegrationTests/Catalog/ExternalSearch/**, tests/Juker.E2ETests/Guest/ExternalSearch/**]
adrs: [ADR-0007, ADR-0010]
external: []
size: M
approved_by: thomas
approved_at: 2026-09-30
---

# SPEC-0014 — Busca externa no YouTube com controle de cota

## 1. Visão Geral
Quando o cliente não acha a música no catálogo, ele toca em **"Não achou? Buscar no YouTube"**. O Juker consulta a YouTube Data API v3 pelo provedor plugável, filtra o que pode tocar no bar (embed permitido, sem transmissão ao vivo, dentro da duração máxima), classifica o tipo (clipe, karaokê, letra, ao vivo), grava as músicas e mídias no catálogo e devolve os resultados. Toda busca externa passa por um **orçamento de cota** diário (com reserva para manutenção), um **cache de consultas** e o **limite de 5 buscas externas por comanda por noite**.

## 2. Motivação & Escopo
**Motivação:** o catálogo precisa crescer com o que o bar pede de verdade, sem estourar a cota de ~100 buscas por dia do projeto.

**Objetivos (dentro do escopo):**
- `IMediaProvider` no domínio do Catalog: `SearchAsync(query, karaoke)` e `GetDetailsAsync(externalIds ≤ 50)`; implementação `YouTubeMediaProvider` (`search.list` com `type=video`, `videoEmbeddable=true`, `maxResults=15`, `regionCode=BR`, `relevanceLanguage=pt`; `videos.list` com `snippet,contentDetails,status`).
- Filtros: `status.embeddable`, não é ao vivo (`liveBroadcastContent == none`), `duration ≤ MaxVideoDuration` do bar, `duration ≥ 60 s`.
- Classificador de tipo por título/canal: karaokê ("karaoke", "karaokê", "instrumental com letra", "sing along", canal na lista `Catalog:KaraokeChannels`); letra ("lyrics", "letra", "legendado"); ao vivo ("live", "ao vivo"); padrão clipe ("official video", "clipe oficial") ou `Other`.
- Extrator de título/artista: padrões "Artista - Título", "Artista – Título (…)" e canal "<Artista> - Topic"/"<Artista>VEVO".
- Upsert idempotente de `Song`/`Media` por `MediaRef` (a mesma mídia nunca duplica).
- Orçamento: `QuotaLedger` por dia do Pacífico (reinício da cota do YouTube), 10.000 unidades, reserva de 1.000 para manutenção; `search` = 100, `videos` = 1 por lote; sem saldo → `EXTERNAL_SEARCH_UNAVAILABLE`.
- Cache: consulta normalizada + `karaoke` → IDs de mídia por 7 dias (não gasta cota nem o limite da comanda).
- Limite por comanda por noite (`ExternalSearchesPerComandaPerNight`, padrão 5), contando só as buscas que chamam a API.
- Métrica `juker.youtube.quota.used` e alerta "cota > 80% do dia" em `alerts.yaml`.
- UI: botão "Não achou? Buscar no YouTube" na tela Buscar, resultados externos na mesma lista, contador "Buscas no YouTube: 3 de 5 hoje", mensagens de limite e indisponibilidade.

**Não-objetivos (fora do escopo):**
- Atualização de 30 dias e remoção de dados vencidos (SPEC-0016).
- Escolha da melhor versão karaokê de uma música e canais preferidos (SPEC-0017, que refina o classificador).
- Pedido de aumento de cota ao Google (ação do fundador fora do código).

## 3. Dependências
- **Implementações necessárias:** SPEC-0015 — modelo `Song`/`Media`, schema `catalog` e a tela Buscar.
- **Contratos consumidos:** SPEC-0008@1 — `JukerMetrics.YouTubeQuotaUsed` e o formato de `alerts.yaml`.
- **Pré-requisitos externos:** projeto no Google Cloud com a YouTube Data API v3 ativada e chave de API restrita ao IP dos servidores (`YOUTUBE_API_KEY` nos GitHub Environments), criada pelo fundador.

## 4. Decisão Arquitetural
**Contexto:** projeto novo; ADR-0007 (provedor plugável, só `Catalog.Infrastructure` conhece o YouTube) e ADR-0010 (métrica e alerta).

**Decisão:** slice `Features/ExternalSearch` orquestra limite → cache → orçamento → provedor → filtros → classificação → upsert; o provedor e o `QuotaLedger` ficam em `Infrastructure`.

**Justificativa:** a ordem garante que nenhuma unidade de cota é gasta quando o cache resolve ou quando o limite da comanda já foi atingido.

**Desvio do padrão existente:** Nenhum.

**Alternativas descartadas:** busca não oficial (Innertube) sem cota (quebra com frequência e viola os termos de forma mais direta; ADR-0007); chave de API do bar (a própria política do YouTube exige um projeto por API Client; fica como possível plano B futuro, fora do MVP).

**ADRs:** ADR-0007, ADR-0010.

## 5. Requisitos Não-Funcionais
- **Desempenho e escala:** busca externa p95 < 2,5 s (limitada pelo YouTube); busca em cache p95 < 200 ms — IT-02.
- **Segurança:** a chave nunca sai do servidor nem aparece em logs (redação da SPEC-0008); `q` limitado a 80 caracteres; somente `GuestOfVenue` participante — IT-03.
- **Privacidade e dados pessoais:** o texto buscado é guardado só no cache normalizado, sem comanda; a contagem por comanda guarda apenas número e noite.
- **Disponibilidade e resiliência:** timeout de 5 s e 1 retry com jitter para erros 5xx do YouTube; erro 403 `quotaExceeded` → marca o dia como esgotado e responde `EXTERNAL_SEARCH_UNAVAILABLE` — IT-04.
- **Acessibilidade (UI):** mensagens de limite e indisponibilidade em região `aria-live` — E2E-01.
- **Custo:** gratuito dentro da cota padrão; 1 busca externa = 101 unidades (search + videos).

## 6. Artefato A — Contrato
**Interface:** endpoint de busca externa, `IMediaProvider` e a política de cota.

```text
POST /api/v1/b/{slug}/catalog/external-search   { "q": "raul seixas maluco beleza", "karaoke": false }  (GuestOfVenue)
  200 { "items": [ …item do contrato da SPEC-0015… ], "fromCache": false,
        "externalSearchesUsed": 3, "externalSearchesLimit": 5 }
  400 QUERY_TOO_SHORT | QUERY_TOO_LONG
  429 EXTERNAL_SEARCH_LIMIT        "Você já usou as 5 buscas no YouTube desta noite. Continue buscando no catálogo."
  503 EXTERNAL_SEARCH_UNAVAILABLE  "A busca no YouTube está indisponível agora. Tente no catálogo."

// Domain (Catalog)
public interface IMediaProvider {
  string Name { get; }                                            // "youtube"
  Task<IReadOnlyList<string>> SearchAsync(string query, bool karaoke, CancellationToken ct);
  Task<IReadOnlyList<ProviderMediaDetails>> GetDetailsAsync(IReadOnlyList<string> ids, CancellationToken ct);
}
public sealed record ProviderMediaDetails(string ExternalId, string Title, string ChannelTitle, TimeSpan Duration,
  bool Embeddable, bool IsLive, string? ThumbnailUrl, bool Exists);

Cota (por dia do fuso America/Los_Angeles): limite 10.000; reserva de manutenção 1.000;
  busca externa permitida se usado + 101 ≤ 9.000
```

**Arquivos/módulos afetados:** ver `touches` no frontmatter.

### 6.1 Mapa de Comportamentos
| Cenário | Condição / Entrada | Resultado esperado | Testes |
|---|---|---|---|
| Classificação do tipo | títulos com karaoke, lyrics, ao vivo e clipe oficial | Karaoke, Lyric, Live e Clip, respectivamente | UT-01 |
| Título e artista | "Raul Seixas - Maluco Beleza (Clipe Oficial)" e canal "Raul Seixas - Topic" | artista "Raul Seixas", título "Maluco Beleza" | UT-01 |
| Filtros | vídeo ao vivo, não embeddable, 45 s, 12 min | descartados com limite de 10 min | UT-02, IT-01 |
| Orçamento | usado 8.950 unidades | busca negada (`EXTERNAL_SEARCH_UNAVAILABLE`); com 8.899 é permitida | UT-03 |
| Dia do Pacífico | 00:30 BRT (ainda dia anterior em Los Angeles) | conta no dia de Los Angeles | UT-03 |
| Provedor real contra o contrato | servidor fake com as respostas da Data API | parâmetros corretos (`type=video`, `videoEmbeddable=true`), paginação ignorada, detalhes em lote de até 50 | IT-01 |
| Ingestão | busca externa com 5 vídeos válidos | músicas e mídias gravadas; nova busca local encontra a música | IT-02, E2E-01 |
| Sem duplicar | mesma mídia retornada em duas buscas | uma única `Media` por `MediaRef` | IT-02 |
| Cache | mesma consulta em 7 dias | nenhuma chamada ao provedor; `fromCache=true`; não conta no limite | IT-02 |
| Limite por comanda | 6ª busca externa da comanda 12 na noite | 429 `EXTERNAL_SEARCH_LIMIT`; outra comanda continua podendo | IT-03, E2E-02 |
| Cota esgotada no YouTube | provedor responde 403 `quotaExceeded` | dia marcado como esgotado; 503 `EXTERNAL_SEARCH_UNAVAILABLE` | IT-04, E2E-02 |
| Falha transitória | 503 do YouTube e depois 200 | 1 retry e sucesso | IT-04 |
| Métrica e alerta | busca externa gasta 101 unidades | `juker.youtube.quota.used` +101; alerta de 80% declarado | IT-02, CT-01 |
| Contrato do provedor | `YouTubeMediaProvider` e `FakeMediaProvider` | mesma suíte de contrato de `IMediaProvider` | CT-01 |

## 7. Artefato B — Plano de Testes (TDD)

### 7.1 Testes de Caracterização
- N/A — funcionalidade nova.

### 7.2 Testes Unitários
- **UT-01** — Dado títulos e canais reais de exemplo (tabela de casos no teste), quando `MediaKindClassifier` e `TitleArtistExtractor` são aplicados, então o tipo e o par artista/título batem com o esperado em cada caso.
- **UT-02** — Dado detalhes com ao vivo, não embeddable, 45 s, 9 min e 12 min, quando `PlayableFilter` aplica o limite de 10 min, então só o de 9 min passa.
- **UT-03** — Dado o `QuotaLedger` com 8.950 unidades usadas, quando uma busca de 101 é pedida, então é negada; com 8.899, é permitida; e às 03:30Z (00:30 BRT, 20:30 do dia anterior em Los Angeles) a conta vai para o dia de Los Angeles.

### 7.3 Testes de Integração
- **IT-01** — `YouTubeMediaProvider` contra o `FakeHttpServer` com respostas gravadas da Data API: a requisição de `search` leva `type=video`, `videoEmbeddable=true`, `maxResults=15` e a chave; `videos.list` recebe até 50 IDs; o mapeamento de duração ISO 8601 e dos campos está correto.
- **IT-02** — Endpoint com provedor fake HTTP e Postgres real: a primeira busca grava músicas e mídias, retorna `fromCache=false` e incrementa a métrica em 101; a busca local seguinte encontra a música; a mesma consulta de novo retorna `fromCache=true` sem chamar o servidor fake; mídias repetidas não duplicam; mede o p95 do cache.
- **IT-03** — Comanda 12 faz 5 buscas externas distintas (200) e a 6ª → 429 `EXTERNAL_SEARCH_LIMIT`; a comanda 13 faz a 1ª com sucesso; um cliente sem sessão → 401.
- **IT-04** — Servidor fake responde 403 `quotaExceeded` → 503 `EXTERNAL_SEARCH_UNAVAILABLE` e o dia fica esgotado (novas buscas não chamam o servidor); servidor com 503 seguido de 200 → sucesso com 1 retry.

### 7.4 Testes de Contrato
- **CT-01** — Suíte de contrato de `IMediaProvider` executada contra o `YouTubeMediaProvider` (com o `FakeHttpServer`) e o `FakeMediaProvider` dos E2E; e validação do `alerts.yaml` com a regra de cota no formato da SPEC-0008@1. Consumida pelas SPECs 0016 e 0017.

### 7.5 Testes E2E
- **E2E-01** — Com o YouTube simulado pelo servidor fake: o cliente busca "maluco beleza" (sem resultados no catálogo), toca em "Não achou? Buscar no YouTube", vê "Maluco Beleza – Raul Seixas" e "Buscas no YouTube: 1 de 5 hoje"; limpa e busca de novo no catálogo, e a música aparece sem usar o YouTube.
- **E2E-02** — Depois de 5 buscas externas distintas da mesma comanda, a 6ª mostra "Você já usou as 5 buscas no YouTube desta noite. Continue buscando no catálogo."; em outra comanda, com o servidor fake devolvendo `quotaExceeded`, a busca mostra "A busca no YouTube está indisponível agora. Tente no catálogo."

### 7.6 Outros
- Verificação manual em staging com a chave real: uma busca externa grava mídias reais e o painel do Google Cloud mostra 101 unidades usadas.

**Dublês e dados de teste:** respostas gravadas da Data API em `tests/Juker.IntegrationTests/Catalog/ExternalSearch/Recordings/*.json`; `FakeMediaProvider` para E2E; `FakeTimeProvider` para os dias da cota.

**Ambiente de execução:** Testcontainers + WireMock.Net (IT); AppHost com `YouTube:BaseUrl` apontando para o servidor fake (E2E).

## 8. Plano de Rollout
- **Estratégia:** atrás da flag `JukeboxEnabled`; chave real só em staging/produção.
- **Dados/schema:** migration aditiva `catalog.ExternalSearch` (`quota_days`, `external_search_cache`, `comanda_external_searches`).
- **Compatibilidade:** N/A.
- **Observabilidade:** métrica `juker.youtube.quota.used` (por operação), contador de buscas externas negadas por motivo, alerta de cota > 80%.
- **Rollback:** redeploy anterior; ou zerar `ExternalSearchesPerComandaPerNight` nas configurações para desligar a busca externa sem deploy.
- **Etapas de migração/coexistência:** N/A.

## 9. Questões em Aberto
- [x] Limite de buscas no YouTube por comanda por noite — 5, configurável (thomas, 2026-09-30)

## 10. Aprovação (H1)
Registrada no frontmatter (`approved_by`, `approved_at`) somente depois que o humano responder "Aprovado". O arquiteto nunca aprova a própria spec.

## 11. Checklist de Implementação

**Fase 1: Regras e componentes (unitário)**
- [ ] Red: escrever UT-01, UT-02, UT-03 com a tag `SPEC-0014:<ID>` e confirmar que falham pelo motivo certo
- [ ] Green: implementar o mínimo para passar, seguindo os ADRs de fundação e o padrão de slices
- [ ] Refactor mantendo tudo verde
- [ ] Validar: build + lint + suíte de UT (G2)

**Fase 2: Integração e contrato**
- [ ] Red: escrever IT-01, IT-02, IT-03, IT-04, CT-01 com a tag `SPEC-0014:<ID>` (Postgres real, esquemas de auth reais, `DrainOutboxAsync`, WireMock — ADR-0012) e confirmar que falham pelo motivo certo
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
| G0 Spec | PASS | `spec_graph.py validate SPEC-0014` → G0 [SPEC-0014]: PASS — 0 erro(s), 0 aviso(s); checklist de julgamento do G0 revisado pelo Architect | 2026-09-30 |
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
