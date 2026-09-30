---
id: SPEC-0017
title: Modo karaokê
tier: full
type: feature
user_facing: true
status: approved
created: 2026-09-30
parent: SPEC-0010
depends_on: [SPEC-0014, SPEC-0020, SPEC-0024]
consumes_contract: []
contract_version: 1
touches: [src/Modules/Catalog/Juker.Modules.Catalog/Features/Karaoke/**, src/Modules/Catalog/Juker.Modules.Catalog/Domain/Classification/**, src/Modules/Catalog/Juker.Modules.Catalog/Features/ExternalSearch/KaraokeQuery*, src/Web/Juker.Web.Client/Guest/Search/KaraokeToggle*, src/Web/Juker.Web.Client/Shared/MediaKindBadge*, tests/Juker.Modules.Catalog.UnitTests/Karaoke/**, tests/Juker.IntegrationTests/Catalog/Karaoke/**, tests/Juker.E2ETests/Guest/Karaoke/**]
adrs: [ADR-0007]
external: []
size: M
approved_by: thomas
approved_at: 2026-09-30
---

# SPEC-0017 — Modo karaokê

## 1. Visão Geral
Com o botão **"🎤 Modo Karaokê"** ligado, o cliente busca a música normalmente e o Juker entrega a **versão karaokê** (instrumental com letra) dessa música, sem o cliente precisar caçar o vídeo certo. Se o catálogo ainda não tem a versão karaokê, a busca no YouTube já procura por ela. O pedido chega à fila e à TV com o badge "🎤 Karaokê", e a TV toca essa versão.

## 2. Motivação & Escopo
**Motivação:** o karaokê é o diferencial contra o concorrente (só Spotify, só áudio) e é pedido explícito do fundador.

**Objetivos (dentro do escopo):**
- `KaraokeResolver`: dada uma música, escolhe a melhor mídia `Karaoke` por (canal na lista `Catalog:KaraokeChannels` → `QualityScore` → duração mais próxima do clipe da mesma música).
- Lista inicial configurável de canais preferidos (ex.: Sing King, KaraFun, canais brasileiros de karaokê definidos na configuração pelo fundador).
- Refinamento do classificador: canal na lista = Karaoke mesmo sem a palavra no título; título com "karaoke" + "cover" sem "instrumental/letra" → Other (evita covers cantados).
- Busca local com `karaoke=true` agrupando por música e usando o `KaraokeResolver`.
- Busca externa com `karaoke=true` acrescenta " karaoke" à consulta quando o termo não contém a palavra (cache separado por modo).
- Componente compartilhado `MediaKindBadge` ("Clipe" / "🎤 Karaokê") usado na busca, na fila, em "Minhas", na TV e no painel.
- Toggle com estado lembrado durante a sessão (armazenamento local do navegador, sem dado pessoal).

**Não-objetivos (fora do escopo):**
- Fila de cantores, microfone ou pontuação de canto (conceito descartado).
- Letra sincronizada própria (a letra vem no próprio vídeo).
- Correção manual do tipo pelo gerente.

## 3. Dependências
- **Implementações necessárias:** SPEC-0014 (classificador e busca externa); SPEC-0020 (a TV toca a mídia do pedido — necessário para o E2E); SPEC-0024 (tela Buscar com o botão "Pedir" e a fila do cliente).
- **Contratos consumidos:** N/A.
- **Pré-requisitos externos:** lista de canais de karaokê preferidos definida pelo fundador na configuração (pode começar vazia).

## 4. Decisão Arquitetural
**Contexto:** projeto novo; ADR-0007 (classificação e `MediaKind` no catálogo próprio).

**Decisão:** slice `Features/Karaoke` com o `KaraokeResolver` e extensões na busca local/externa; o badge é um componente compartilhado do Web.Client.

**Justificativa:** a escolha da versão fica num único lugar do Catalog; a fila e a TV só recebem o `mediaId` já resolvido.

**Desvio do padrão existente:** Nenhum.

**Alternativas descartadas:** o cliente escolher o vídeo karaokê manualmente (é exatamente a dor a resolver); marcação manual pelo bar (trabalho para o bar).

**ADRs:** ADR-0007.

## 5. Requisitos Não-Funcionais
- **Desempenho e escala:** busca local com `karaoke=true` mantém p95 < 150 ms — IT-01.
- **Segurança:** N/A — sem nova superfície além da busca existente.
- **Privacidade e dados pessoais:** o estado do toggle fica só no navegador — revisão G4.
- **Disponibilidade e resiliência:** sem versão karaokê tocável → mensagem "Ainda não temos a versão karaokê dessa música" com o atalho para a busca no YouTube — E2E-01.
- **Acessibilidade (UI):** badge com texto (não só o ícone); toggle `role="switch"` — UT-02.
- **Custo:** a busca externa no modo karaokê custa o mesmo (101 unidades).

## 6. Artefato A — Contrato
**Interface:** regras de escolha, parâmetro `karaoke` nas buscas e componente de badge.

```text
KaraokeResolver.Pick(song) → Media? :
  candidatos: Kind=Karaoke && Playable && QualityScore > -10
  ordem: canal ∈ Catalog:KaraokeChannels desc, QualityScore desc, |Duration - Duration(clipe)| asc

GET  /api/v1/b/{slug}/catalog/search?q=&karaoke=true   → 1 item por música, com a mídia do resolver
POST /api/v1/b/{slug}/catalog/external-search { q, karaoke: true }
     → consulta enviada ao provedor: q + " karaoke" (se q não contiver "karaok")

<MediaKindBadge Kind="MediaKind.Karaoke" />  → "🎤 Karaokê"   ·   Kind=Clip → "Clipe"
Vazio no modo karaokê: "Ainda não temos a versão karaokê dessa música" + "Buscar karaokê no YouTube"
```

**Arquivos/módulos afetados:** ver `touches` no frontmatter.

### 6.1 Mapa de Comportamentos
| Cenário | Condição / Entrada | Resultado esperado | Testes |
|---|---|---|---|
| Canal preferido vence | duas mídias karaokê, uma de canal preferido com score menor | a do canal preferido é escolhida | UT-01 |
| Desempate por duração | dois karaokês sem canal preferido e mesmo score | o de duração mais próxima do clipe | UT-01 |
| Cover cantado | título "Sweet Child O' Mine karaoke cover (cantado)" | classificado como Other, não Karaoke | UT-03 |
| Canal da lista sem a palavra | canal "Sing King", título "Sweet Child O' Mine" | classificado como Karaoke | UT-03 |
| Busca local karaokê | `karaoke=true` para "sweet child" | um item por música com a mídia do resolver | IT-01 |
| Busca externa karaokê | `karaoke=true`, "sweet child" | consulta enviada ao provedor com " karaoke"; cache separado do modo normal | IT-02 |
| Sem versão karaokê | música só com clipe | mensagem de vazio e atalho para a busca externa de karaokê | E2E-01 |
| Pedido toca karaokê na TV | toggle ligado, pedir a música | a TV carrega o `MediaRef` da versão karaokê e o letreiro mostra "🎤" | E2E-01 |
| Badge | `Kind=Karaoke` e `Kind=Clip` | textos "🎤 Karaokê" e "Clipe" | UT-02 |

## 7. Artefato B — Plano de Testes (TDD)

### 7.1 Testes de Caracterização
- N/A — funcionalidade nova.

### 7.2 Testes Unitários
- **UT-01** — Dado as mídias karaokê A (canal preferido, score 0), B (score 5) e C/D (sem canal, mesmo score, durações 5:40 e 7:10, com o clipe de 5:56), quando `KaraokeResolver.Pick` é aplicado, então escolhe A; sem A, escolhe B; sem A e B, escolhe C.
- **UT-02** — Dado o `MediaKindBadge` (bUnit) com Karaoke e Clip, então renderiza "🎤 Karaokê" e "Clipe"; o toggle tem `role="switch"` e `aria-checked` coerente.
- **UT-03** — Dado os casos "karaoke cover (cantado)", canal "Sing King" sem a palavra no título e "Instrumental com letra", quando o classificador refinado roda, então retorna Other, Karaoke e Karaoke.

### 7.3 Testes de Integração
- **IT-01** — Com Postgres real e a semente com clipes e karaokês: `search?karaoke=true&q=sweet child` retorna um item por música com a mídia escolhida pelo resolver; p95 < 150 ms com 50.000 mídias.
- **IT-02** — Com o `FakeHttpServer`: a busca externa com `karaoke=true` envia `q=sweet child karaoke`; com `q=sweet child karaoke`, não duplica a palavra; o cache de "sweet child" no modo normal não é usado no modo karaokê.

### 7.4 Testes de Contrato
- N/A — não há contrato novo entre specs; o parâmetro `karaoke` já faz parte dos contratos das SPECs 0014 e 0015.

### 7.5 Testes E2E
- **E2E-01** — Cliente liga o "Modo Karaokê", busca "sweet child", vê o item com "🎤 Karaokê" e toca em "Pedir"; a TV (FakeMediaPlayer) carrega o `MediaRef` da versão karaokê e o letreiro exibe o ícone de karaokê; em seguida, busca uma música que só tem clipe e vê "Ainda não temos a versão karaokê dessa música" com o atalho para a busca no YouTube.

### 7.6 Outros
- Verificação manual em staging: 20 músicas da lista do piloto no modo karaokê, com pelo menos 17 trazendo uma versão instrumental com letra correta (registrar a taxa no Relatório de Entrega; abaixo disso, ajustar a lista de canais).

**Dublês e dados de teste:** semente com pares clipe/karaokê; `FakeHttpServer` para a busca externa; `FakeMediaPlayer` na TV.

**Ambiente de execução:** Testcontainers (IT); AppHost + Playwright (E2E).

## 8. Plano de Rollout
- **Estratégia:** atrás da flag `JukeboxEnabled`.
- **Dados/schema:** N/A — usa o modelo existente; a lista de canais fica na configuração.
- **Compatibilidade:** N/A.
- **Observabilidade:** contador de buscas no modo karaokê sem resultado (sinal para ajustar canais ou semear).
- **Rollback:** redeploy anterior.
- **Etapas de migração/coexistência:** N/A.

## 9. Questões em Aberto
Nenhuma.

## 10. Aprovação (H1)
Registrada no frontmatter (`approved_by`, `approved_at`) somente depois que o humano responder "Aprovado". O arquiteto nunca aprova a própria spec.

## 11. Checklist de Implementação

**Fase 1: Regras e componentes (unitário)**
- [ ] Red: escrever UT-01, UT-02, UT-03 com a tag `SPEC-0017:<ID>` e confirmar que falham pelo motivo certo
- [ ] Green: implementar o mínimo para passar, seguindo os ADRs de fundação e o padrão de slices
- [ ] Refactor mantendo tudo verde
- [ ] Validar: build + lint + suíte de UT (G2)

**Fase 2: Integração e contrato**
- [ ] Red: escrever IT-01, IT-02 com a tag `SPEC-0017:<ID>` (Postgres real, esquemas de auth reais, `DrainOutboxAsync`, WireMock — ADR-0012) e confirmar que falham pelo motivo certo
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
| G0 Spec | PASS | `spec_graph.py validate SPEC-0017` → G0 [SPEC-0017]: PASS — 0 erro(s), 0 aviso(s); checklist de julgamento do G0 revisado pelo Architect | 2026-09-30 |
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
