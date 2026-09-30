---
id: SPEC-0023
title: Painel do gerente
tier: full
type: feature
user_facing: true
status: approved
created: 2026-09-30
parent: SPEC-0010
depends_on: [SPEC-0021, SPEC-0022]
consumes_contract: [SPEC-0019@1, SPEC-0011@1]
contract_version: 1
touches: [src/Modules/Queue/Juker.Modules.Queue/Features/AdminStats/**, src/Web/Juker.Web/Components/Admin/Dashboard/**, src/Web/Juker.Web/Components/Admin/Comandas/**, src/Web/Juker.Web/Components/Admin/Tv/**, src/Web/Juker.Web/Components/Admin/Layout/**, src/Web/Juker.Web/wwwroot/admin/**, tests/Juker.IntegrationTests/Queue/AdminStats/**, tests/Juker.Web.UnitTests/Admin/**, tests/Juker.E2ETests/Admin/Dashboard/**]
adrs: [ADR-0003]
external: []
size: M
approved_by: thomas
approved_at: 2026-09-30
---

# SPEC-0023 — Painel do gerente

## 1. Visão Geral
O "botão de pânico" do dono do bar, no design aprovado do Stitch, para **tablet** (caixa) e **celular** (gerente circulando), na mesma tela responsiva e instalável como PWA. Aba **Fila**: card "Tocando agora" sempre no topo com **Pular**, **Pular e bloquear** e **Pausar/Retomar**, e a fila com **Remover** e **Bloquear** por música. Aba **Comandas**: busca, filtros e bloquear/desbloquear. Cabeçalho com "TV online" e o código atual; resumo da noite (tocadas hoje, comandas conectadas, mais pedida). Em **Ajustes**, a seção de TVs (gerar código de pareamento, listar e revogar).

## 2. Motivação & Escopo
**Motivação:** "o dono do bar precisa ter o controle absoluto da situação para a festa não desandar" — em segundos, de onde ele estiver.

**Objetivos (dentro do escopo):**
- Layout responsivo: tablet (≥ 768 px) com menu lateral (Fila, Comandas, Ajustes) e fila em tabela densa; celular com card "Tocando agora" fixo no topo, abas Fila/Comandas com contadores e navegação inferior Fila / Comandas / Ajustes; cabeçalho numa linha só (logo, bar, "TV online/offline", "Código 4821").
- Fila: posição, música + artista + badge, "#07" (comanda), saldo de votos, horário do pedido; ações Remover (confirmação) e Bloquear (diálogo); no celular, cards com ações num menu "⋯".
- Diálogo de bloqueio: "Bloquear a comanda 12?" + "Ela não poderá pedir músicas nem votar até o fim da noite." + efeitos calculados ("2 músicas na fila serão removidas", "Os votos dela deixam de contar", "A música atual será pulada" quando for o caso) + [Cancelar] [Bloquear] com contraste AA.
- "Pular e bloquear": pula a atual e abre o diálogo de bloqueio da comanda dela.
- Pausar/Retomar (SPEC-0020).
- Comandas: busca "Buscar comanda", filtros Todas / Com músicas / Bloqueadas, cards com "Comanda #12", badges "Tocando agora"/"Próxima da fila", "N músicas na fila • M pedidas na noite", bloqueada em vermelho com "Desbloquear comanda".
- Resumo: `GET /api/v1/admin/stats` (tocadas hoje, comandas conectadas, mais pedida com contagem).
- Ajustes → TVs: gerar código (mostra o código grande e a expiração), lista de TVs com "online/offline" e "Revogar".
- PWA do painel: manifest (nome "Juker Gerente", ícones, `display: standalone`, `start_url: /admin`) e service worker só para os assets estáticos (nada de cache de API).
- Tudo atualiza pelo tempo real (grupo `staff`).
- Ajustes do design aplicados: sem "Limitador bar 86 dB", "Latência", "POS Engine", "Operational Hub", "AO VIVO", "Mesa 04" e o item "Modo Karaokê" no menu.

**Não-objetivos (fora do escopo):**
- Motivo do bloqueio, alerta de comanda suspeita e correção do tipo de mídia.
- Vários gerentes com permissões diferentes e histórico de noites anteriores.
- Swipe para ações no celular (menu "⋯" no MVP).

## 3. Dependências
- **Implementações necessárias:** SPEC-0021 (API de pareamento e lista de TVs, e com ela a SPEC-0020 de reprodução); SPEC-0022 (bloqueio e lista de comandas).
- **Contratos consumidos:** SPEC-0019@1 (saldo de votos na fila do gerente); SPEC-0011@1 (página Ajustes onde entra a seção de TVs).
- **Pré-requisitos externos:** ícones do PWA exportados do design (logo do Juker).

## 4. Decisão Arquitetural
**Contexto:** projeto novo; ADR-0003 (admin em InteractiveServer, PWA instalável, tokens do Stitch).

**Decisão:** componentes em `Juker.Web/Components/Admin/*` compartilhados entre tablet e celular por breakpoints do Tailwind; o resumo da noite é um slice `Features/AdminStats` no módulo Queue, que consulta `ISessionsModule` para as comandas conectadas.

**Justificativa:** uma única tela responsiva evita dois painéis para manter; o Queue é o dono de "tocadas" e "mais pedida".

**Desvio do padrão existente:** Nenhum.

**Alternativas descartadas:** app separado para celular (duplicação); estatísticas calculadas no navegador (consultas pesadas no cliente).

**ADRs:** ADR-0003.

## 5. Requisitos Não-Funcionais
- **Desempenho e escala:** ação de pular/pausar refletida na TV em < 1 s (SPEC-0020) e no painel em < 1 s — E2E-01; `GET /admin/stats` p95 < 100 ms — IT-01.
- **Segurança:** todas as rotas `/admin` e `/api/v1/admin` com `ManagerOfVenue`; antiforgery nas ações de formulário; confirmação antes de ações destrutivas — E2E-01.
- **Privacidade e dados pessoais:** só números de comanda e músicas.
- **Disponibilidade e resiliência:** circuito do InteractiveServer caído → reconexão automática com a mensagem "Reconectando ao Juker…"; ações ficam desabilitadas enquanto isso — UT-03.
- **Acessibilidade (UI):** botões de pânico com rótulo textual e área mínima de 48 px; diálogos com foco preso e Esc; contraste AA (inclusive o botão vermelho de bloquear) — UT-02 e E2E-01/E2E-02 (axe).
- **Custo:** N/A.

## 6. Artefato A — Contrato
**Interface:** endpoint de resumo e fluxo de telas do painel.

```text
GET /api/v1/admin/stats   (ManagerOfVenue)
  200 { "playedTonight": 47, "connectedComandas": 12, "mostRequested": { "title": "Sweet Child O' Mine", "count": 6 } | null }

Telas (/admin):
  cabeçalho: JUKER · BAR DO ROCK · ● TV ONLINE | ● TV OFFLINE · CÓDIGO 4821
  Fila:
    [Tocando agora] miniatura · TÍTULO · artista · badge · "Pedido pela Comanda 12" · progresso
      [⏭ Pular música] [🚫 Pular e bloquear comanda] [⏸ Pausar | ▶ Retomar]
    tabela (tablet) / cards (celular): # · música/artista/badge · #07 · saldo · 21:04 · [Remover] [Bloquear]
    remover → "Remover 'Paranoid' da fila?" [Cancelar] [Remover]
    bloquear → diálogo com efeitos calculados (ver seção 2)
  Comandas: [Buscar comanda (ex: 07, 12)…] · [Todas (12)] [Com músicas (7)] [Bloqueadas (1)]
    card: COMANDA #12 · [TOCANDO AGORA]|[PRÓXIMA DA FILA] · "2 músicas na fila • 4 pedidas na noite" · [Bloquear]
    bloqueada: fundo vermelho · "Bloqueada às 22:15" · [Desbloquear comanda]
  Resumo: "Músicas tocadas hoje: 47" · "Comandas conectadas: 12" · "Mais pedida: SWEET CHILD O' MINE (6X)"
  Ajustes → TVs: [Gerar código de pareamento] → "K7M2QX" (expira em 10 min) · lista: nome · online/offline · [Revogar]
  navegação: tablet = menu lateral · celular = barra inferior (Fila · Comandas · Ajustes)
  PWA: /admin/manifest.webmanifest · service worker de assets
```

**Arquivos/módulos afetados:** ver `touches` no frontmatter.

### 6.1 Mapa de Comportamentos
| Cenário | Condição / Entrada | Resultado esperado | Testes |
|---|---|---|---|
| Resumo da noite | 47 tocadas, 12 comandas, mais pedida 6× | números do contrato; sem pedidos → `mostRequested=null` | IT-01 |
| Pular | toque em "Pular música" | TV vai para a próxima; o card atualiza | E2E-01 |
| Pular e bloquear | toque no botão com a música da comanda 12 | pula e abre o diálogo de bloqueio da 12 | UT-01, E2E-01 |
| Pausar/retomar | toque em "Pausar" e depois "Retomar" | TV pausa e retoma; o botão alterna o rótulo | E2E-01 |
| Remover da fila | confirmar a remoção | a música some da fila em todos os aparelhos | E2E-01 |
| Diálogo de bloqueio | comanda com 2 pendentes, votos e a música atual | efeitos listados: 2 removidas, votos anulados, atual pulada | UT-01 |
| Bloquear pela lista | confirmar no diálogo | comanda aparece como bloqueada em Comandas; cliente vê "Pedidos pausados" | E2E-01 |
| Comandas | filtros e busca "12" | lista filtrada com badges "Tocando agora"/"Próxima da fila" | UT-02, E2E-02 |
| Desbloquear | "Desbloquear comanda" | volta a ativa | E2E-02 |
| TV offline | `TvStatusChanged(offline)` | cabeçalho mostra "TV OFFLINE" em vermelho | UT-03 |
| TVs | gerar código e revogar | código e expiração exibidos; TV revogada some da lista | E2E-02 |
| Celular | viewport 390×844 | card "Tocando agora" fixo no topo, abas com contadores e navegação inferior | E2E-02 |
| PWA | carregar `/admin` | manifest válido com `start_url` `/admin` e service worker registrado | IT-02 |
| Reconexão do circuito | circuito Server cai | "Reconectando ao Juker…" e ações desabilitadas | UT-03 |

## 7. Artefato B — Plano de Testes (TDD)

### 7.1 Testes de Caracterização
- N/A — funcionalidade nova.

### 7.2 Testes Unitários
- **UT-01** — Dado o `BlockComandaDialog` (bUnit) para a comanda 12 com 2 pendentes, votos dados e a música atual dela, então lista "2 músicas na fila serão removidas", "Os votos dela deixam de contar" e "A música atual será pulada"; o "Pular e bloquear" chama o pulo e abre esse diálogo.
- **UT-02** — Dado o `ComandasPanel` (bUnit) com 3 comandas (uma bloqueada, uma tocando), quando o filtro "Bloqueadas" e a busca "12" são aplicados, então a lista e os badges seguem o contrato; o botão do diálogo tem contraste AA (verificado com o helper de contraste dos tokens).
- **UT-03** — Dado o cabeçalho com `TvStatusChanged(offline)` e o circuito em reconexão, então exibe "TV OFFLINE" e "Reconectando ao Juker…" com as ações desabilitadas.

### 7.3 Testes de Integração
- **IT-01** — Com Postgres real: após 47 músicas terminadas (Played/Skipped) na noite, 12 comandas com sessão e "Sweet Child O' Mine" pedida 6 vezes, `GET /admin/stats` retorna esses números com p95 < 100 ms; noite sem pedidos → `mostRequested=null`; gerente de outro bar não vê esses números.
- **IT-02** — `GET /admin/manifest.webmanifest` retorna um manifest válido (nome, ícones 192/512, `display: standalone`, `start_url: /admin`) e `/admin` registra o service worker, que não intercepta `/api/*`.

### 7.4 Testes de Contrato
- **CT-01** — Os clientes do painel validam o que consomem contra os snapshots OpenAPI de fila e votos (SPEC-0018/0019@1), reprodução (SPEC-0020), comandas e bloqueio (SPEC-0022), pareamento (SPEC-0021) e configurações (SPEC-0011@1).

### 7.5 Testes E2E
- **E2E-01** — Tablet 1280×800: o gerente entra, vê a fila com 4 músicas e a TV de teste tocando; pausa e retoma (a TV acompanha); pula (a TV vai para a próxima); remove uma música confirmando; usa "Pular e bloquear" na música da comanda 12, confirma o diálogo com os efeitos listados, e o celular da comanda 12 mostra "Pedidos pausados"; sem violações axe.
- **E2E-02** — Celular 390×844: o card "Tocando agora" com os botões fica visível no topo ao rolar; na aba Comandas, busca "12", vê a bloqueada e desbloqueia; em Ajustes → TVs, gera um código e revoga a TV de teste; sem violações axe.

### 7.6 Outros
- Revisão visual contra as telas do Stitch (Tablet Gerente, Confirmação Bloqueio, Mobile Gerente Fila & Emergência, Gestão de Comandas), com capturas no Relatório de Entrega; instalação do PWA testada num tablet Android e num iPhone.

**Dublês e dados de teste:** TV de teste (FakeMediaPlayer), pedidos e sessões pelas APIs e emissores de teste.

**Ambiente de execução:** bUnit; Testcontainers (IT); AppHost + Playwright (E2E); aparelhos reais (7.6).

## 8. Plano de Rollout
- **Estratégia:** atrás da flag `JukeboxEnabled` para as ações de fila; login e Ajustes já disponíveis.
- **Dados/schema:** N/A — só leitura agregada.
- **Compatibilidade:** N/A.
- **Observabilidade:** contador de ações do gerente por tipo (`skip`, `pause`, `remove`, `block`) — também é métrica de sucesso do piloto.
- **Rollback:** redeploy anterior.
- **Etapas de migração/coexistência:** N/A.

## 9. Questões em Aberto
- [x] Painel no celular — sim, a mesma tela responsiva, instalável como PWA (thomas, 2026-09-30)
- [x] Pausar/retomar — entra no MVP (thomas, 2026-09-30)

## 10. Aprovação (H1)
Registrada no frontmatter (`approved_by`, `approved_at`) somente depois que o humano responder "Aprovado". O arquiteto nunca aprova a própria spec.

## 11. Checklist de Implementação

**Fase 1: Regras e componentes (unitário)**
- [ ] Red: escrever UT-01, UT-02, UT-03 com a tag `SPEC-0023:<ID>` e confirmar que falham pelo motivo certo
- [ ] Green: implementar o mínimo para passar, seguindo os ADRs de fundação e o padrão de slices
- [ ] Refactor mantendo tudo verde
- [ ] Validar: build + lint + suíte de UT (G2)

**Fase 2: Integração e contrato**
- [ ] Red: escrever IT-01, IT-02, CT-01 com a tag `SPEC-0023:<ID>` (Postgres real, esquemas de auth reais, `DrainOutboxAsync`, WireMock — ADR-0012) e confirmar que falham pelo motivo certo
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
| G0 Spec | PASS | `spec_graph.py validate SPEC-0023` → G0 [SPEC-0023]: PASS — 0 erro(s), 0 aviso(s); checklist de julgamento do G0 revisado pelo Architect | 2026-09-30 |
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
