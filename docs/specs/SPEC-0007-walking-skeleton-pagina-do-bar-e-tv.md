---
id: SPEC-0007
title: "Walking skeleton: página do bar e TV tocando"
tier: full
type: foundation
user_facing: true
status: approved
created: 2026-09-30
parent: SPEC-0001
depends_on: [SPEC-0003, SPEC-0004, SPEC-0005, SPEC-0025]
consumes_contract: []
contract_version: 2
touches: [src/Modules/Venues/**, src/Web/**, src/Juker.Host/**, tests/Juker.Modules.Venues.UnitTests/**, tests/Juker.IntegrationTests/Venues/**, tests/Juker.IntegrationTests/Web/**, tests/Juker.E2ETests/**, tests/Juker.Web.UnitTests/**]
adrs: [ADR-0001, ADR-0003, ADR-0004, ADR-0007, ADR-0009, ADR-0013]
external: []
size: M
approved_by: thomas
approved_at: 2026-09-30
---

# SPEC-0007 — Walking skeleton: página do bar e TV tocando

## 1. Visão Geral
A fatia mais fina do produto passando por todas as camadas e chegando à produção pelo pipeline: um bar cadastrado por comando administrativo, a **página pública do bar** (`/b/{slug}`, prerender + WASM) mostrando o nome do bar e "Jukebox ativa" com o visual do Stitch, e a **tela de TV** (`/tv/{slug}`) tocando uma lista fixa de vídeos pelo componente de player, passando para o próximo ao terminar. Inclui o pipeline de Tailwind v4 com os tokens do design system, o orçamento de payload do app do cliente e a verificação manual do YouTube Premium no hardware real.

## 2. Motivação & Escopo
**Motivação:** provar cedo os três maiores riscos técnicos (payload do WASM no 4G, player na TV com autoplay e Premium, deploy de ponta a ponta) antes de construir qualquer regra de jukebox.

**Objetivos (dentro do escopo):**
- Módulo Venues mínimo: entidade `Venue` (Id, Slug, Name, CreatedAt), schema `venues`, primeira migration, comando `juker admin create-venue --slug <slug> --name <nome>`.
- `GET /api/v1/b/{slug}` → dados públicos do bar.
- Páginas `/b/{slug}` (InteractiveWebAssembly com prerender) e `/b/{slug}` inexistente → página "Bar não encontrado".
- `/tv/{slug}` (InteractiveWebAssembly) com o componente `MediaPlayer` (interface) e a implementação `YouTubePlayer` (IFrame Player API via JS interop), tocando a lista de demonstração configurada em `Tv:DemoPlaylist`, sem nada sobreposto ao vídeo.
- `FakeMediaPlayer` (vídeos locais curtos) selecionado por configuração para os E2E.
- Tailwind v4 standalone no build do `Juker.Web`, com `@theme` gerado dos tokens do Stitch (cores, Oswald/Inter, raios, espaçamentos).
- Service worker do cliente cacheando o runtime do WASM.
- Bar fixo **`juker-smoke`** ("Juker Smoke", sem QR público) criado pela CLI (`create-venue`), usado só pelo smoke (ADR-0013).
- Smoke E2E (`Category=Smoke`) executável contra qualquer host já em execução via `JUKER_E2E_BASE_URL` (SPEC-0025). **Nesta fase (CD adiado), roda contra o host local** (`dotnet run --project src/Aspire/Juker.AppHost -c Release`); a execução pós-deploy em staging/produção entra com a SPEC-0006.

**Não-objetivos (fora do escopo):**
- Autenticação do gerente e pareamento da TV (SPEC-0009 e SPEC-0021); a `/tv/{slug}` desta spec só toca a lista de demonstração.
- Entrada do cliente com comanda e PIN, fila e busca (SPEC-0010).
- Layout final da TV (SPEC-0021).

## 3. Dependências
- **Implementações necessárias:** SPEC-0003 (harness de integração), SPEC-0004 (building blocks), SPEC-0005 (CI), SPEC-0025 (harness de E2E, Playwright e API de teste). A SPEC-0006 (deploy e ambientes) saiu das dependências: CD adiado por decisão do fundador.
- **Contratos consumidos:** N/A.
- **Pré-requisitos externos:** uma TV com mini PC ou Fire TV/Chrome e uma conta YouTube Premium de teste para a verificação manual (7.6); a lista de demonstração com vídeos do YouTube que permitem embed (definida pelo fundador em configuração, não no código).

## 4. Decisão Arquitetural
**Contexto:** projeto novo; ADR-0001 (módulos), ADR-0003 (render mode por área e Tailwind), ADR-0004 (schema por módulo), ADR-0007 (player isolado atrás de interface), ADR-0009 (deploy) e ADR-0013 (E2E e smoke).

**Decisão:** primeiro slice do módulo Venues (`Features/GetPublicVenue`) consumido pelo `Juker.Web.Client` via `HttpClient` tipado; o player da TV é o componente `MediaPlayer` com implementações YouTube e Fake.

**Justificativa:** exercita o caminho completo módulo → API → WASM → deploy com o menor código possível e já fixa a fronteira do player exigida pelo ADR-0007.

**Desvio do padrão existente:** Nenhum.

**Alternativas descartadas:** skeleton só com `/health` (não prova o WASM nem o player); TV em modo Server (ADR-0003).

**ADRs:** ADR-0001, ADR-0003, ADR-0004, ADR-0007, ADR-0009, ADR-0013.

## 5. Requisitos Não-Funcionais
- **Desempenho e escala:** primeiro acesso a `/b/{slug}` com payload compactado (brotli) ≤ 3,0 MB — IT-03; LCP ≤ 2,5 s com 4G rápido emulado no Lighthouse mobile — 7.6.
- **Segurança:** endpoint público só expõe `slug`, `name` e `status`; slug validado (`^[a-z0-9-]{3,40}$`) — UT-01.
- **Privacidade e dados pessoais:** N/A — nenhum dado pessoal.
- **Disponibilidade e resiliência:** o player da TV, ao receber erro de um vídeo (indisponível ou embed negado), pula para o próximo em até 3 s — E2E-01 (erro simulado no Fake).
- **Acessibilidade (UI):** `/b/{slug}` sem violações axe de nível A/AA — E2E-01 via `AssertNoA11yViolationsAsync`.
- **Custo:** N/A — sem chamada à Data API (o player do IFrame não consome cota).

## 6. Artefato A — Contrato
**Interface:** `GET /api/v1/b/{slug}`, páginas `/b/{slug}` e `/tv/{slug}`, contrato do componente de player e comando administrativo.

```text
GET /api/v1/b/{slug}
  200 application/json  { "slug": "rock", "name": "Bar do Rock", "status": "active" }
  404 application/problem+json { "code": "VENUE_NOT_FOUND" }
  400 application/problem+json { "code": "VENUE_SLUG_INVALID" }   (slug fora do padrão)

Página /b/{slug}   → cabeçalho "JUKER", nome do bar, indicador "Jukebox ativa"
Página /b/{slug} inexistente → "Bar não encontrado" + texto "Confira o QR Code ou o link na TV"
Página /tv/{slug}  → área de vídeo 16:9 tocando Tv:DemoPlaylist em sequência

Componente de player (Juker.Web.Client.Tv):
  interface IMediaPlayer {
    Task LoadAsync(MediaRef media, bool autoplay);   // MediaRef { Provider, ExternalId }
    Task PauseAsync(); Task ResumeAsync(); Task StopAsync();
    event Action<MediaRef> Ready; event Action<MediaRef> Ended;
    event Action<MediaRef, PlayerErrorCode> Failed;   // Unavailable | EmbedNotAllowed | Unknown
    event Action<TimeSpan, TimeSpan> Progress;        // posição, duração (a cada 1 s)
  }

CLI:
  juker admin create-venue --slug rock --name "Bar do Rock"
    exit 0 + "Bar criado: rock" | exit 2 + "Slug já existe" | exit 2 + "Slug inválido"
```

**Arquivos/módulos afetados:** ver `touches` no frontmatter.

### 6.1 Mapa de Comportamentos
| Cenário | Condição / Entrada | Resultado esperado | Testes |
|---|---|---|---|
| Bar existe | `GET /api/v1/b/rock` | 200 com slug, nome e status | IT-01, CT-01 |
| Bar não existe | `GET /api/v1/b/nao-existe` | 404 `VENUE_NOT_FOUND` | IT-02, E2E-02 |
| Slug inválido | `GET /api/v1/b/AB!` | 400 `VENUE_SLUG_INVALID` | UT-01, IT-02 |
| Cadastro de bar | `create-venue` com slug novo | bar criado; slug duplicado → exit 2 "Slug já existe" | UT-02, IT-04 |
| Página do bar no celular | abrir `/b/rock` em viewport mobile | nome "Bar do Rock" e "Jukebox ativa" visíveis, sem violações de acessibilidade | E2E-01 |
| Payload do cliente | primeiro acesso a `/b/rock` com `Accept-Encoding: br` | soma dos recursos ≤ 3,0 MB | IT-03 |
| TV toca em sequência | `/tv/rock` com playlist de 2 itens | item 1 toca; ao terminar, item 2 toca | E2E-01, CT-02 |
| Vídeo com erro na TV | item 1 falha com `Unavailable` | pula para o item 2 em até 3 s | E2E-01, CT-02 |
| Visual do design system | componentes renderizados | classes vindas dos tokens (`bg-surface`, `text-primary`, `font-display`) | UT-03 |
| Smoke contra host em execução | host local (ou, com a SPEC-0006, o ambiente implantado) com o bar `juker-smoke` | `/health/ready` = 200 e `/b/juker-smoke` mostra "Juker Smoke" e "Jukebox ativa" | E2E-03 |

## 7. Artefato B — Plano de Testes (TDD)

### 7.1 Testes de Caracterização
- N/A — projeto novo.

### 7.2 Testes Unitários
- **UT-01** — Dado slugs `rock`, `bar-do-rock` e `AB!`, `a`, quando validados por `VenueSlug.Create`, então os dois primeiros são válidos e os demais retornam `VENUE_SLUG_INVALID`.
- **UT-02** — Dado um nome vazio ou com mais de 80 caracteres, quando `Venue.Create` é chamado, então retorna `VENUE_NAME_INVALID`.
- **UT-03** — Dado o componente `VenueHeader` (bUnit) com o nome "Bar do Rock", quando renderizado, então usa as classes de token `font-display` e `text-primary` e exibe "Jukebox ativa".

### 7.3 Testes de Integração
- **IT-01** — Com Postgres real e um bar `rock` criado pela migration e pelo handler, `GET /api/v1/b/rock` retorna 200 com o corpo do contrato.
- **IT-02** — `GET /api/v1/b/nao-existe` → 404 `VENUE_NOT_FOUND`; `GET /api/v1/b/AB!` → 400 `VENUE_SLUG_INVALID`.
- **IT-03** — Com o host publicado em Release, um cliente HTTP percorre o HTML de `/b/rock` e os recursos de `_framework` referenciados, com `Accept-Encoding: br`, e a soma dos bytes transferidos é ≤ 3,0 MB.
- **IT-04** — O comando `create-venue` executado duas vezes com o mesmo slug cria o bar na primeira e sai com código 2 e "Slug já existe" na segunda.

### 7.4 Testes de Contrato
- **CT-01** — A resposta de `GET /api/v1/b/{slug}` valida contra o schema OpenAPI publicado em `/openapi/v1.json` (consumido pelas SPECs 0013 e 0021).
- **CT-02** — O `FakeMediaPlayer` e o `YouTubePlayer` passam a mesma suíte de contrato de `IMediaPlayer` (ordem Ready → Progress → Ended; Failed com o código mapeado), com o YouTube exercitado contra um stub do objeto `YT.Player` no bUnit/JS; o contrato é consumido pela SPEC-0020.

### 7.5 Testes E2E
- **E2E-01** — Com um bar criado pela API de teste, o celular 390×844 (Chromium e WebKit) abre `/b/{slug}` e vê o nome do bar e "Jukebox ativa", sem violações axe; a TV 1920×1080 abre `/tv/{slug}` com o `FakeMediaPlayer` e a playlist [falha, ok1, ok2]: o item com falha é pulado, `ok1` toca até o fim e `ok2` começa.
- **E2E-02** — Celular (Chromium e WebKit) abre `/b/nao-existe` e vê "Bar não encontrado".
- **E2E-03** — (Smoke) Contra um host já em execução indicado por `JUKER_E2E_BASE_URL` (nesta fase, o host local em Release com o bar `juker-smoke` criado pela CLI): `/health/ready` = 200 e o celular abre `/b/juker-smoke` e vê "Juker Smoke" e "Jukebox ativa". A execução após cada deploy em staging e produção é responsabilidade da SPEC-0006.

### 7.6 Outros
- **Verificação manual do YouTube Premium (obrigatória antes do SPEC-0010):** na TV real do piloto, com o navegador em kiosk logado numa conta Premium e cookies do YouTube liberados, abrir `/tv/rock` com a `DemoPlaylist` real de 10 vídeos e confirmar: autoplay após uma única interação inicial, 10 vídeos seguidos **sem anúncio** e sem intervenção. Evidência: vídeo/foto e hardware usado, registrados no Relatório de Entrega. Se houver anúncios, abrir impedimento `decisão` para o fundador.
- Performance: Lighthouse mobile em `/b/rock` com o host local em Release e throttling de 4G, LCP ≤ 2,5 s — relatório anexado (a repetição em staging entra com a SPEC-0006).

**Dublês e dados de teste:** `FakeMediaPlayer` com vídeos MP4 de 3 s em `wwwroot/test-media` (só no ambiente `E2E`); bar próprio por teste criado pela API de teste (`e2e-…`) nos E2E e por builder nos IT; bar fixo `juker-smoke` nos ambientes implantados.

**Ambiente de execução:** IT com Testcontainers; E2E local pelo AppHost e no CI (SPEC-0005); smoke contra o host local (staging depois, com a SPEC-0006).

## 8. Plano de Rollout
- **Estratégia:** sem deploy nesta fase (CD adiado; ver SPEC-0006): a entrega é o merge na `main` com o CI verde. A `/tv/{slug}` de demonstração não expõe dados (só toca a playlist configurada); ela é substituída pela TV pareada da SPEC-0021.
- **Dados/schema:** migration `venues.Initial` (tabela `venues`, índice único em `slug`), só aditiva.
- **Compatibilidade:** `/api/v1` é o prefixo de versão pública.
- **Observabilidade:** logs estruturados do handler; o smoke E2E-03 roda contra o host local (e após cada deploy, quando a SPEC-0006 existir).
- **Rollback:** revert do merge na `main`; a migration é aditiva.
- **Etapas de migração/coexistência:** N/A.

## 9. Questões em Aberto
- [x] Qual hardware roda a TV? — Mini PC ou Fire TV/Chrome em modo kiosk, a ser confirmado na verificação manual do Premium (recomendação aceita pelo fundador em 2026-09-30)

## 10. Aprovação (H1)
Registrada no frontmatter (`approved_by`, `approved_at`) somente depois que o humano responder "Aprovado". O arquiteto nunca aprova a própria spec.

## 11. Checklist de Implementação

**Fase 1: Regras e componentes (unitário)**
- [ ] Red: escrever UT-01, UT-02, UT-03 com a tag `SPEC-0007:<ID>` e confirmar que falham pelo motivo certo
- [ ] Green: implementar o mínimo para passar, seguindo os ADRs de fundação e o padrão de slices
- [ ] Refactor mantendo tudo verde
- [ ] Validar: build + lint + suíte de UT (G2)

**Fase 2: Integração e contrato**
- [ ] Red: escrever IT-01, IT-02, IT-03, IT-04, CT-01, CT-02 com a tag `SPEC-0007:<ID>` (Postgres real, esquemas de auth reais, `DrainOutboxAsync`, WireMock — ADR-0012) e confirmar que falham pelo motivo certo
- [ ] Green: implementar endpoints, persistência, migrations e integrações até passar
- [ ] Refactor mantendo tudo verde
- [ ] Validar: build + suíte completa + arquitetura + cobertura do diff ≥ 80% (G2/G3)

**Fase 3: Jornada E2E**
- [ ] Red: escrever E2E-01, E2E-02, E2E-03 com page objects, pré-condições pela API `/_test/*` e jornada pela interface (ADR-0013); confirmar que falham pelo motivo certo
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
| G0 Spec | PASS | `spec_graph.py validate SPEC-0007` → G0 [SPEC-0007]: PASS — 0 erro(s), 0 aviso(s); checklist de julgamento do G0 revisado pelo Architect (revisão ADR-0012/0013) | 2026-09-30 |
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
| 2 | 2026-09-30 | E2E-03 (smoke) passa a rodar contra o host local via `JUKER_E2E_BASE_URL`; dependência da SPEC-0006 removida; Lighthouse local em Release; rollout sem deploy nesta fase | Fundador decidiu deixar a infraestrutura/CD de fora por enquanto: testar localmente até o fim, com o CI do GitHub antes do CD | SPEC-0006 (recebe a execução pós-deploy do smoke e do Lighthouse) | thomas — autorização prévia no chat (2026-09-30: "deixar a infra de fora… optar pela recomendação"), a confirmar no overview |
