---
id: SPEC-0024
title: Telas da fila, Minhas e pedido pela busca
tier: full
type: feature
user_facing: true
status: approved
created: 2026-09-30
parent: SPEC-0010
depends_on: [SPEC-0018]
consumes_contract: [SPEC-0020@1, SPEC-0012@1]
contract_version: 1
touches: [src/Web/Juker.Web.Client/Guest/Queue/**, src/Web/Juker.Web.Client/Guest/Mine/**, src/Web/Juker.Web.Client/Guest/Search/Request/**, src/Web/Juker.Web.Client/Guest/Shared/**, src/Web/Juker.Web.Client/Api/Queue*, tests/Juker.Web.UnitTests/Guest/Queue/**, tests/Juker.Web.UnitTests/Guest/Mine/**, tests/Juker.E2ETests/Guest/Queue/**]
adrs: [ADR-0003, ADR-0005]
external: []
size: M
approved_by: thomas
approved_at: 2026-09-30
---

# SPEC-0024 — Telas da fila, Minhas e pedido pela busca

## 1. Visão Geral
As telas do cliente que dão vida à fila justa, seguindo o design aprovado no Stitch: **Fila** (card "Tocando agora" e a lista "Próximas na fila", com "Sua música · em ~X min" e o aviso "Fila justa: cada mesa tem sua vez"), **Minhas** (barra "Limite da comanda 2/3", "Na fila" com remover e "Já tocaram hoje" com "Pedir de novo") e o **pedido pela busca** ("Pedir", estado "Na fila", banner do limite, bottom sheet "Calma, roqueiro! 🤘" e o toast "Adicionada! Você é o 5º da fila"). Tudo atualiza sozinho pelo tempo real.

## 2. Motivação & Escopo
**Motivação:** a justiça da fila só convence o cliente se ele enxergar a posição, a vez e o tempo estimado, e se pedir for um toque.

**Objetivos (dentro do escopo):**
- Página `/b/{slug}/fila`: card "Tocando agora" (miniatura, título, artista, badge, "Pedido pela Comanda X", barra de progresso com tempo; usa `GET /api/v1/b/{slug}/now-playing` da SPEC-0020 e o evento `NowPlayingChanged`), chip "Fila justa: cada mesa tem sua vez" com explicação ao tocar, lista com posição, miniatura, título, artista, "Comanda X", badge; itens da própria comanda destacados com "Sua música · em ~X min" (X = restante da atual + `secondsAhead`, arredondado para cima em minutos) sem botões de voto; estado vazio "A fila está vazia. Que tal pedir a primeira?".
- Página `/b/{slug}/minhas`: barra do limite ("2 de 3 músicas na fila", "1 vaga disponível para pedir agora"), grupo "Na fila" com posição e "~X min" e a ação remover (com confirmação "Remover esta música da fila?"), grupo "Já tocaram hoje" com "Tocou às 21:42" e "Pedir de novo".
- Busca: botão "Pedir" em cada resultado; "✓ Na fila" desabilitado para música já pendente na fila; banner "Você tem N músicas na fila (máx. L)"; ao atingir o limite, bottom sheet "Calma, roqueiro! 🤘 — Você já tem 3 músicas na fila. Assim que uma tocar, você pode pedir outra." com "Ver minha fila"; toast "Adicionada! Você é o Nº da fila".
- Mensagens de erro do contrato da SPEC-0018 exibidas como toast (tocou há pouco, indisponível, longa demais).
- Toasts de remoção recebidos pelo evento `RequestRemoved` para a comanda: `manager` → "O bar removeu sua música da fila."; `votes` → "Sua música saiu da fila pelos votos da galera."; `unavailable` → "Sua música ficou indisponível e saiu da fila.".
- Recarga do estado em `QueueChanged`, `NowPlayingChanged` e `Resynced` (SPEC-0012), com o banner "Reconectando…".

**Não-objetivos (fora do escopo):**
- Botões de voto e seus estados (SPEC-0019).
- Tela de comanda bloqueada / modo ouvinte (SPEC-0022).
- Toggle do karaokê (SPEC-0015/0017).

## 3. Dependências
- **Implementações necessárias:** SPEC-0018 — API da fila e "Minhas".
- **Contratos consumidos:** SPEC-0020@1 (`GET /api/v1/b/{slug}/now-playing` e `NowPlayingChanged`); SPEC-0012@1 (`VenueConnection` e eventos).
- **Pré-requisitos externos:** N/A.

## 4. Decisão Arquitetural
**Contexto:** projeto novo; ADR-0003 (cliente em WASM com prerender, tokens do Stitch) e ADR-0005 (notifica e relê).

**Decisão:** páginas e componentes em `Juker.Web.Client/Guest/*`, com um `QueueStore` por página que relê a API em cada notificação e expõe o estado aos componentes.

**Justificativa:** o estado vem sempre da API (fonte da verdade); os componentes ficam simples e testáveis com bUnit.

**Desvio do padrão existente:** Nenhum.

**Alternativas descartadas:** aplicar diffs vindos do hub (risco de divergência; ADR-0005).

**ADRs:** ADR-0003, ADR-0005.

## 5. Requisitos Não-Funcionais
- **Desempenho e escala:** mudança na fila refletida na tela do cliente em < 1,5 s após o pedido de outro aparelho — E2E-01 (espera por condição com limite).
- **Segurança:** nenhuma decisão de regra no cliente (limite e duplicidade só exibidos a partir da API).
- **Privacidade e dados pessoais:** N/A — só comanda e músicas.
- **Disponibilidade e resiliência:** sem conexão, a tela mantém o último estado com o banner "Reconectando…" e relê ao voltar — UT-04.
- **Acessibilidade (UI):** lista com `role="list"`, posição anunciada ("posição 5"), toasts em `aria-live="polite"`, bottom sheet com foco preso e fechamento por Esc; contraste AA — UT-02 e E2E-01 (axe).
- **Custo:** N/A.

## 6. Artefato A — Contrato
**Interface:** telas, estados e textos (fluxo de tela).

```text
/b/{slug}/fila
  [Tocando agora] miniatura · TÍTULO · Artista · badge · "Pedido pela Comanda 3" · barra 2:18 / 3:28
  [chip] "Fila justa: cada mesa tem sua vez"  → ao tocar: "As primeiras músicas de cada mesa tocam antes das segundas. Os votos mudam a ordem dentro da mesma vez."
  PRÓXIMAS NA FILA · "N músicas na sequência"
    item: "#pos" · miniatura · título · artista · badge · "Comanda X"
    item meu: destaque magenta · "Sua música · em ~7 min"
  vazio: "A fila está vazia. Que tal pedir a primeira?" + botão "Buscar música"
/b/{slug}/minhas
  "LIMITE DA COMANDA  2 / 3 MÚSICAS" · "1 vaga disponível para pedir agora" · "MÁX. 3 POR VEZ"
  NA FILA (2): badge · "3º · ~9 min" · título · artista · [remover] → confirmação "Remover esta música da fila?" [Cancelar] [Remover]
  JÁ TOCARAM HOJE (2): "Tocou às 21:42" · título · artista · [Pedir de novo]
/b/{slug}/buscar (acréscimos)
  banner "Você tem 2 músicas na fila (máx. 3)" · por item: [+ Pedir] | [✓ Na fila] (desabilitado)
  bottom sheet no limite: "Calma, roqueiro! 🤘" · texto · [Ver minha fila] · "Regra da casa para que todo mundo possa tocar seu som."
  toast sucesso: "Adicionada! Você é o 5º da fila"
Toasts por RequestRemoved: manager | votes | unavailable (textos da seção 2)
```

**Arquivos/módulos afetados:** ver `touches` no frontmatter.

### 6.1 Mapa de Comportamentos
| Cenário | Condição / Entrada | Resultado esperado | Testes |
|---|---|---|---|
| Tempo estimado | atual com 70 s restantes e `secondsAhead` = 350 | "em ~7 min" | UT-01 |
| Lista da fila | itens de várias comandas e 2 meus | destaque e "Sua música · em ~X min" só nos meus, sem votos | UT-02, E2E-01 |
| Fila vazia | nenhum item | "A fila está vazia. Que tal pedir a primeira?" | UT-02 |
| Pedir pela busca | toque em "Pedir" | toast "Adicionada! Você é o Nº da fila" e o item aparece em Fila e Minhas | E2E-01 |
| Já na fila | música pendente | botão "✓ Na fila" desabilitado | UT-03 |
| Limite atingido | 422 `COMANDA_LIMIT_REACHED` | bottom sheet "Calma, roqueiro! 🤘" com "Ver minha fila" | UT-03, E2E-02 |
| Outros erros | 422 `RECENTLY_PLAYED` / `MEDIA_UNAVAILABLE` / `MEDIA_TOO_LONG` | toast com o texto do contrato | UT-03 |
| Remover o meu | confirmar a remoção | some da fila e de Minhas; vaga liberada | E2E-02 |
| Pedir de novo | item em "Já tocaram hoje" | cria o pedido (ou mostra "Essa música tocou há pouco…") | E2E-02 |
| Tempo real | outro celular pede uma música | a lista atualiza sozinha em < 1,5 s | E2E-01 |
| Removido pelo bar/votos | evento `RequestRemoved` para a comanda | toast com o motivo | UT-04 |
| Reconexão | hub cai e volta | banner "Reconectando…" e releitura ao reconectar | UT-04 |

## 7. Artefato B — Plano de Testes (TDD)

### 7.1 Testes de Caracterização
- N/A — funcionalidade nova.

### 7.2 Testes Unitários
- **UT-01** — Dado o restante da atual de 70 s e `secondsAhead` de 350 s (e de 0 s com 20 s restantes), quando `EtaFormatter.Format` é aplicado, então retorna "em ~7 min" (e "em ~1 min").
- **UT-02** — Dado a página Fila (bUnit) com itens de várias comandas, dois da comanda 7 (a do usuário) e o now-playing, então os meus têm destaque e "Sua música · em ~X min" sem botões de voto, os demais mostram "Comanda X", e a lista vazia mostra o texto do estado vazio; a lista usa `role="list"`.
- **UT-03** — Dado a Busca (bUnit) com uma música pendente e o cliente de API fake, então o item pendente mostra "✓ Na fila" desabilitado; o 422 `COMANDA_LIMIT_REACHED` abre o bottom sheet com foco preso; os demais códigos mostram o toast com o texto do contrato.
- **UT-04** — Dado o `QueueStore` com `VenueConnection` fake, quando chegam `QueueChanged`, `Resynced` e `RequestRemoved(votes)` para a comanda, então a API é relida duas vezes e o toast "Sua música saiu da fila pelos votos da galera." é exibido; com `Reconnecting` por 5 s, aparece o banner.

### 7.3 Testes de Integração
- **IT-01** — Com o host real (TestServer + Postgres), o cliente de API tipado `QueueApiClient` do Web.Client desserializa as respostas reais de `GET /queue`, `GET /me/requests` e `POST /queue` (incluindo os `ProblemDetails` de erro) sem perda de campos.

### 7.4 Testes de Contrato
- **CT-01** — O `QueueApiClient` e o cliente de now-playing validam o que consomem contra os schemas OpenAPI publicados pela SPEC-0018@1 e pela SPEC-0020@1, e os handlers de eventos contra o snapshot de eventos da SPEC-0012@1.

### 7.5 Testes E2E
- **E2E-01** — Comandas 12 e 7 em dois celulares: a 12 pede 3 músicas pela busca (toast "Adicionada! Você é o 1º/2º/3º da fila"); a 7 pede 1; no celular da 12, a Fila mostra a ordem 12, 7, 12, 12 (sem recarregar), com as da 12 destacadas e "em ~X min"; as telas não têm violações axe.
- **E2E-02** — A comanda 12, com 3 pendentes, tenta pedir a 4ª e vê "Calma, roqueiro! 🤘"; em "Minhas", remove uma (confirma) e vê "1 vaga disponível para pedir agora"; depois que uma música dela toca (avanço via API da TV de teste), ela aparece em "Já tocaram hoje" e o "Pedir de novo" mostra "Essa música tocou há pouco. Tente de novo mais tarde.".

### 7.6 Outros
- Revisão visual contra as telas do Stitch (Fila Colaborativa, Minhas Músicas, Buscar Músicas, Limite Atingido): capturas no Relatório de Entrega; sem o ícone de cadeado nem as caixas "#1/#2/#3 NA FILA" (ajustes decididos na revisão do design).

**Dublês e dados de teste:** clientes de API fake e `VenueConnection` fake (bUnit); TV de teste para avançar a reprodução nos E2E.

**Ambiente de execução:** bUnit; host real para IT; AppHost + Playwright para E2E.

## 8. Plano de Rollout
- **Estratégia:** atrás da flag `JukeboxEnabled`.
- **Dados/schema:** N/A.
- **Compatibilidade:** N/A.
- **Observabilidade:** erros de chamada da API reportados no log do cliente via endpoint de telemetria do navegador (OTel web, amostrado).
- **Rollback:** redeploy anterior.
- **Etapas de migração/coexistência:** N/A.

## 9. Questões em Aberto
Nenhuma.

## 10. Aprovação (H1)
Registrada no frontmatter (`approved_by`, `approved_at`) somente depois que o humano responder "Aprovado". O arquiteto nunca aprova a própria spec.

## 11. Checklist de Implementação

**Fase 1: Regras e componentes (unitário)**
- [ ] Red: escrever UT-01, UT-02, UT-03, UT-04 com a tag `SPEC-0024:<ID>` e confirmar que falham pelo motivo certo
- [ ] Green: implementar o mínimo para passar, seguindo os ADRs de fundação e o padrão de slices
- [ ] Refactor mantendo tudo verde
- [ ] Validar: build + lint + suíte de UT (G2)

**Fase 2: Integração e contrato**
- [ ] Red: escrever IT-01, CT-01 com a tag `SPEC-0024:<ID>` (Postgres real, esquemas de auth reais, `DrainOutboxAsync`, WireMock — ADR-0012) e confirmar que falham pelo motivo certo
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
| G0 Spec | PASS | `spec_graph.py validate SPEC-0024` → G0 [SPEC-0024]: PASS — 0 erro(s), 0 aviso(s); checklist de julgamento do G0 revisado pelo Architect | 2026-09-30 |
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
