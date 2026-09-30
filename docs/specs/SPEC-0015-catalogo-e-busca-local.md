---
id: SPEC-0015
title: Catálogo e busca local
tier: full
type: feature
user_facing: true
status: approved
created: 2026-09-30
parent: SPEC-0010
depends_on: [SPEC-0007, SPEC-0008, SPEC-0009]
consumes_contract: [SPEC-0011@1, SPEC-0013@1, SPEC-0018@1]
contract_version: 1
touches: [src/Modules/Catalog/Juker.Modules.Catalog/Domain/**, src/Modules/Catalog/Juker.Modules.Catalog/Features/Search/**, src/Modules/Catalog/Juker.Modules.Catalog/Features/Top/**, src/Modules/Catalog/Juker.Modules.Catalog/Features/Stats/**, src/Modules/Catalog/Juker.Modules.Catalog/Infrastructure/Persistence/**, src/Modules/Catalog/Juker.Modules.Catalog/CatalogModule.cs, src/Modules/Catalog/Juker.Modules.Catalog.Contracts/**, src/Web/Juker.Web.Client/Guest/Search/**, tests/Juker.Modules.Catalog.UnitTests/Search/**, tests/Juker.IntegrationTests/Catalog/Search/**, tests/Juker.Web.UnitTests/Guest/Search/**, tests/Juker.E2ETests/Guest/Search/**]
adrs: [ADR-0004, ADR-0007]
external: []
size: M
approved_by: thomas
approved_at: 2026-09-30
---

# SPEC-0015 — Catálogo e busca local

## 1. Visão Geral
O catálogo próprio do Juker: **músicas** (identidade nossa: título e artista normalizados) e as **mídias** de cada música (referência ao vídeo no provedor, tipo clipe/karaokê/letra/ao vivo, duração, disponibilidade). O cliente busca enquanto digita e recebe resultados do nosso banco em milissegundos, com tolerância a erro de digitação e acentos, e vê as listas "🔥 Mais pedidas neste bar" e "📈 Em alta no Juker". Nenhum áudio ou vídeo é armazenado.

## 2. Motivação & Escopo
**Motivação:** a busca local é o que torna o produto rápido e o que economiza a cota do YouTube; quanto mais o bar usa, menos o Juker depende da API.

**Objetivos (dentro do escopo):**
- Modelo: `Song` (Id, Title, Artist, SearchText sem acento e em minúsculas), `Media` (Id, SongId, `MediaRef` {Provider, ExternalId} único, Kind ∈ {Clip, Karaoke, Lyric, Live, Other}, DurationSeconds, Embeddable, Status ∈ {Available, Unavailable}, RawTitle, ChannelTitle, ThumbnailUrl, FetchedAt, QualityScore), `VenueMediaStats` (VenueId, MediaId, RequestCount, LastRequestedAt) e `GlobalMediaStats` (MediaId, dia, RequestCount).
- Schema `catalog` com `pg_trgm` + `unaccent` e índice GIN trigram em `SearchText`.
- Busca: `GET /api/v1/b/{slug}/catalog/search?q=&karaoke=` com mínimo de 2 caracteres; só mídias `Available`, `Embeddable` e com duração ≤ `MaxVideoDuration` do bar; `karaoke=true` → só `Karaoke`; `karaoke=false` → preferência para `Clip` (depois `Lyric`, `Live` e `Other`), uma mídia por música; ordenação por similaridade, pedidos no bar e `QualityScore`; esconde mídias com `QualityScore ≤ -10`.
- Listas: `GET /api/v1/b/{slug}/catalog/top?scope=venue|global&karaoke=` (bar: últimas 30 noites; global: últimos 7 dias, somente contagens agregadas, sem dados de outros bares).
- Estatísticas atualizadas pelo consumidor do evento `RequestAdded` da Queue (contrato da SPEC-0018).
- `ICatalogModule.GetMediaAsync` para a Queue e a Playback.
- Tela **Buscar** (Stitch "Juker - Buscar Músicas"): barra de busca com limpar, toggle "🎤 Modo Karaokê", carrossel "Mais pedidas neste bar", resultados com miniatura, título, artista, duração e badge "Clipe"/"Karaokê"; debounce de 250 ms; estado vazio "Nenhuma música encontrada no catálogo".
- Semente de desenvolvimento/E2E com ~50 músicas fictícias e mídias fake.

**Não-objetivos (fora do escopo):**
- Botão "Pedir", banner de limite e "Na fila" nos resultados (SPEC-0024).
- Busca no YouTube e ingestão (SPEC-0014); classificação do karaokê (SPEC-0014/0017); atualização de 30 dias e qualidade (SPEC-0016).
- Aba "Em alta no Juker" com filtros por gênero.

## 3. Dependências
- **Implementações necessárias:** SPEC-0007 — host, Web.Client e pipeline de Tailwind.
- **Contratos consumidos:** SPEC-0011@1 (`VenueRules.MaxVideoDuration`); SPEC-0013@1 (shell do cliente e sessão Guest); SPEC-0018@1 (evento `RequestAdded` para as estatísticas).
- **Pré-requisitos externos:** N/A.

## 4. Decisão Arquitetural
**Contexto:** projeto novo; ADR-0004 (Postgres + pg_trgm/unaccent) e ADR-0007 (catálogo próprio, `MediaRef` sem tipos do provedor).

**Decisão:** slices `Features/Search`, `Features/Top` e `Features/Stats` no módulo Catalog; consulta por SQL (EF Core com `EF.Functions.TrigramsSimilarity` via Npgsql) e `ICatalogModule` em `Juker.Modules.Catalog.Contracts`.

**Justificativa:** o trigram do Postgres dá a tolerância a erro necessária sem outro serviço, e o `MediaRef` mantém o resto do sistema livre do YouTube.

**Desvio do padrão existente:** Nenhum.

**Alternativas descartadas:** busca por `ILIKE` (sem tolerância a erro); Elasticsearch (ADR-0004).

**ADRs:** ADR-0004, ADR-0007.

## 5. Requisitos Não-Funcionais
- **Desempenho e escala:** p95 < 150 ms na busca com 50.000 mídias — IT-02 com massa gerada.
- **Segurança:** somente `GuestOfVenue` ou `ManagerOfVenue`; consulta parametrizada; `q` limitado a 80 caracteres — IT-01.
- **Privacidade e dados pessoais:** "Em alta no Juker" usa só contagens agregadas por mídia, sem bar nem comanda — IT-03.
- **Disponibilidade e resiliência:** a busca local funciona mesmo com a cota do YouTube esgotada (não chama o provedor).
- **Acessibilidade (UI):** campo de busca com rótulo; toggle do karaokê como `switch` com `aria-checked`; resultados em lista navegável — UT-03 e E2E-01 (axe).
- **Custo:** N/A — sem chamadas externas.

## 6. Artefato A — Contrato
**Interface:** endpoints de busca e listas, `ICatalogModule` e a tela Buscar.

```text
GET /api/v1/b/{slug}/catalog/search?q=sweet%20chi&karaoke=false   (GuestOfVenue | ManagerOfVenue)
  200 { "items": [ { "mediaId": "…", "songId": "…", "title": "Sweet Child O' Mine", "artist": "Guns N' Roses",
                     "durationSeconds": 356, "kind": "clip", "thumbnailUrl": "…" } ] }   (até 20)
  400 QUERY_TOO_SHORT (< 2 caracteres) | QUERY_TOO_LONG (> 80)
GET /api/v1/b/{slug}/catalog/top?scope=venue|global&karaoke=false  → 200 { "items": [ …mesmo item… ] } (até 10)

// Juker.Modules.Catalog.Contracts
public enum MediaKind { Clip, Karaoke, Lyric, Live, Other }
public readonly record struct MediaRef(string Provider, string ExternalId);
public sealed record MediaSnapshot(Guid MediaId, Guid SongId, MediaRef Ref, string Title, string Artist,
  MediaKind Kind, TimeSpan Duration, bool Playable, string? ThumbnailUrl);   // Playable = Available && Embeddable
public interface ICatalogModule {
  Task<MediaSnapshot?> GetMediaAsync(Guid mediaId, CancellationToken ct);
}

Tela /b/{slug}/buscar:
  campo "Busque uma música ou artista" · toggle "🎤 Modo Karaokê" (texto de apoio "Filtro ativado: somente com letra")
  carrossel "🔥 Mais pedidas neste bar" · lista "N resultados encontrados"
  vazio: "Nenhuma música encontrada no catálogo"
```

**Arquivos/módulos afetados:** ver `touches` no frontmatter.

### 6.1 Mapa de Comportamentos
| Cenário | Condição / Entrada | Resultado esperado | Testes |
|---|---|---|---|
| Normalização | "Guns N’ Roses – Sweet Child" com acento/apóstrofo curvo | `SearchText` sem acento, minúsculo, apóstrofos unificados | UT-01 |
| Erro de digitação | busca "sweet chid" | "Sweet Child O' Mine" entre os 3 primeiros | IT-01 |
| Sem acento | busca "acucar" | encontra "Açúcar" | IT-01 |
| Filtro de disponibilidade | mídia `Unavailable`, não embeddable ou acima de 10 min | fora dos resultados | IT-01 |
| Modo karaokê | `karaoke=true` | só mídias `Karaoke` | IT-01, E2E-01 |
| Uma mídia por música | música com clipe e lyric | só o clipe aparece com `karaoke=false` | UT-02, IT-01 |
| Qualidade baixa | `QualityScore = -10` | escondida da busca | UT-02 |
| Consulta curta/longa | `q="a"` ou 81 caracteres | 400 `QUERY_TOO_SHORT`/`QUERY_TOO_LONG` | IT-01 |
| Mais pedidas no bar | pedidos registrados via `RequestAdded` | lista ordenada por contagem das últimas 30 noites do bar | IT-03 |
| Em alta no Juker | pedidos em dois bares | contagem global de 7 dias, sem expor bar/comanda | IT-03 |
| Desempenho | 50.000 mídias | p95 < 150 ms | IT-02 |
| Tela Buscar | digitar "sweet chi" | resultados aparecem após o debounce, com badge do tipo | UT-03, E2E-01 |
| Busca vazia | termo sem resultados | "Nenhuma música encontrada no catálogo" | UT-03 |
| Contrato do módulo | `GetMediaAsync` | snapshot com `Playable` coerente | CT-01 |

## 7. Artefato B — Plano de Testes (TDD)

### 7.1 Testes de Caracterização
- N/A — funcionalidade nova.

### 7.2 Testes Unitários
- **UT-01** — Dado "Guns N’ Roses" e "Açúcar – Ao Vivo", quando `SearchTextNormalizer.Normalize` é aplicado, então retorna "guns n' roses" e "acucar - ao vivo".
- **UT-02** — Dado uma música com mídias Clip, Lyric e Karaoke e uma mídia com `QualityScore = -10`, quando `MediaSelector.Pick(karaoke:false)` e `Pick(karaoke:true)` são aplicados, então escolhem o Clip e o Karaoke, e a mídia com qualidade -10 nunca é escolhida.
- **UT-03** — Dado a página Buscar (bUnit) com um cliente de API fake, quando o usuário digita "sweet chi", então uma única chamada é feita após 250 ms (FakeTimeProvider), os resultados exibem o badge "Clipe"/"Karaokê", e com lista vazia aparece "Nenhuma música encontrada no catálogo"; o toggle tem `role="switch"`.

### 7.3 Testes de Integração
- **IT-01** — Com Postgres real e a semente: "sweet chid" traz "Sweet Child O' Mine" no top 3; "acucar" traz "Açúcar"; mídias indisponíveis, não embeddable ou com 11 min não aparecem com o limite de 10 min; `karaoke=true` só retorna Karaoke; `q` curto/longo → 400.
- **IT-02** — Com 50.000 mídias geradas, 200 buscas variadas têm p95 < 150 ms.
- **IT-03** — Eventos `RequestAdded` de dois bares atualizam `VenueMediaStats` e `GlobalMediaStats`; `top?scope=venue` do bar A ignora pedidos do bar B; `top?scope=global` soma os dois sem expor o bar.

### 7.4 Testes de Contrato
- **CT-01** — Suíte de contrato de `ICatalogModule` (consumida pelas SPECs 0018, 0020 e 0024) e verificação do consumidor contra o schema do evento `RequestAdded` publicado pela SPEC-0018@1.

### 7.5 Testes E2E
- **E2E-01** — Cliente com sessão abre "Buscar", vê "Mais pedidas neste bar", digita "sweet chi" e vê "Sweet Child O' Mine" com o badge "Clipe"; liga o "Modo Karaokê" e vê somente resultados com "Karaokê"; a tela não tem violações axe.

### 7.6 Outros
- N/A.

**Dublês e dados de teste:** semente `catalog-seed.e2e.json` (músicas fictícias + nomes clássicos, mídias com `Provider=fake`); gerador de massa para o IT-02; eventos `RequestAdded` construídos pelo schema da SPEC-0018.

**Ambiente de execução:** Testcontainers (IT); AppHost + Playwright (E2E).

## 8. Plano de Rollout
- **Estratégia:** atrás da flag `JukeboxEnabled`.
- **Dados/schema:** migration aditiva `catalog.Initial` (extensões, tabelas e índice GIN).
- **Compatibilidade:** N/A.
- **Observabilidade:** histograma `juker.catalog.search.duration` e contador de buscas sem resultado (sinal para semear o catálogo).
- **Rollback:** redeploy anterior; tabelas novas.
- **Etapas de migração/coexistência:** N/A.

## 9. Questões em Aberto
Nenhuma.

## 10. Aprovação (H1)
Registrada no frontmatter (`approved_by`, `approved_at`) somente depois que o humano responder "Aprovado". O arquiteto nunca aprova a própria spec.

## 11. Checklist de Implementação

**Fase 1: Regras e componentes (unitário)**
- [ ] Red: escrever UT-01, UT-02, UT-03 com a tag `SPEC-0015:<ID>` e confirmar que falham pelo motivo certo
- [ ] Green: implementar o mínimo para passar, seguindo os ADRs de fundação e o padrão de slices
- [ ] Refactor mantendo tudo verde
- [ ] Validar: build + lint + suíte de UT (G2)

**Fase 2: Integração e contrato**
- [ ] Red: escrever IT-01, IT-02, IT-03, CT-01 com a tag `SPEC-0015:<ID>` (Postgres real, esquemas de auth reais, `DrainOutboxAsync`, WireMock — ADR-0012) e confirmar que falham pelo motivo certo
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
| G0 Spec | PASS | `spec_graph.py validate SPEC-0015` → G0 [SPEC-0015]: PASS — 0 erro(s), 0 aviso(s); checklist de julgamento do G0 revisado pelo Architect | 2026-09-30 |
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
