---
id: SPEC-0010
title: MVP Jukebox colaborativa
tier: epic
type: feature
status: approved
created: 2026-09-30
depends_on: [SPEC-0001]
adrs: [ADR-0001, ADR-0003, ADR-0005, ADR-0006, ADR-0007, ADR-0008, ADR-0012, ADR-0013]
external: []
approved_by: thomas
approved_at: 2026-09-30
---

# SPEC-0010 — MVP Jukebox colaborativa (Épico)

## 1. Visão
O Juker rodando numa sexta-feira real no bar piloto: o cliente lê o QR, digita a comanda e o código da TV e, sem cadastro, busca músicas (clipe ou karaokê), pede e vota nas músicas das outras mesas. A fila se organiza sozinha de forma justa, a TV toca tudo em sequência com o letreiro "Tocando agora – Pedido pela Comanda X" e as próximas 3, e o gerente tem o botão de pânico (pular, remover, bloquear comanda, pausar) no tablet ou no celular. Posicionamento: *"o único jukebox com vídeo, karaokê e fila justa automática — a galera vota, ninguém monopoliza e você não precisa aprovar nada"*.

## 2. Escopo
**Objetivos (dentro do escopo):**
- Configurações do bar e termos de uso aceitos pelo gerente (SPEC-0011).
- Tempo real por bar (SPEC-0012).
- Entrada do cliente com comanda + PIN rotativo, vários celulares por comanda e o shell do app do cliente (SPEC-0013).
- Catálogo próprio com busca local instantânea, "Mais pedidas neste bar" e "Em alta no Juker" (SPEC-0015).
- Busca externa no YouTube com ingestão no catálogo e controle de cota (SPEC-0014).
- Manutenção do catálogo: atualização de 30 dias, mídia indisponível, sinais de qualidade e semeadura (SPEC-0016).
- Modo karaokê (SPEC-0017).
- Fila justa com limite por comanda, sem duplicar e sem repetir em 60 min, remover o próprio pedido (SPEC-0018).
- Telas Fila, Minhas (com "Pedir de novo") e o pedido pela busca (SPEC-0024).
- Votos com auto-remoção (SPEC-0019).
- Reprodução: pareamento da TV, avanço automático, pular, pausar/retomar, erro de vídeo e TV offline (SPEC-0020).
- Tela da TV completa, pareamento da TV e link curto do bar (SPEC-0021).
- Moderação: bloqueio de comanda com modo ouvinte (SPEC-0022).
- Painel do gerente para tablet e celular, instalável como PWA (SPEC-0023).

**Não-objetivos (fora do escopo):**
- Música VIP / prioridade paga, créditos e qualquer pagamento.
- Spotify, Deezer, Soundtrack ou outro provedor (a interface existe; ADR-0007).
- Integração com PDV/comandas do bar.
- Autocadastro de bares, cobrança da mensalidade e planos.
- Motivo do bloqueio, alerta de "comanda suspeita" por votos negativos e correção manual do tipo de mídia (ideias registradas para depois do piloto).
- Push notifications e app de loja.

## 3. Arquitetura Alvo
**Contexto:** projeto novo; stack e arquitetura nos ADRs da fundação (SPEC-0001).

```
Cliente (/b/{slug}, WASM)     TV (/tv, WASM)           Painel (/admin, Server)
      │  HTTP + SignalR            │ HTTP + SignalR           │ HTTP + SignalR
      └────────────────────── Juker.Host ───────────────────────┘
   Venues ── settings, termos, equipe          Sessions ── PIN, comanda, sessão, bloqueio
   Catalog ── músicas, mídias, busca, karaokê  Queue ── pedidos, fila justa, votos
   Playback ── TV pareada, estado do player, avanço, pular/pausar
   (interfaces síncronas em *.Contracts + eventos via outbox — ADR-0006)
```

Fluxos principais:
1. **Pedido:** cliente → `Queue.AddRequest` (consulta Sessions: participante e não bloqueado; Catalog: mídia disponível e dentro da duração; Venues: limites) → `RequestAdded` → notificação → celulares, TV e painel releem a fila.
2. **Tocar:** TV reporta `ended` → `Playback` marca como tocada e pede `Queue.DequeueNext` → `NowPlayingChanged` → TV carrega a próxima mídia.
3. **Bloqueio:** gerente → `Sessions.BlockComanda` → `ComandaBlocked` (outbox) → Queue remove os pendentes e anula os votos; Playback pula se a música atual for da comanda; as sessões da comanda viram modo ouvinte.

Regra da fila justa (decidida pelo fundador em 2026-09-30): a "vez" (rodada) de cada pedido = músicas da comanda já tocadas na noite + posição do pedido entre os pendentes da comanda. A fila ordena por (vez ↑, saldo de votos ↓, horário do pedido ↑). **Os votos só reordenam dentro da mesma vez.**

**Decisões (ADRs):** ADR-0001, ADR-0003, ADR-0005, ADR-0006, ADR-0007, ADR-0008; testes de todas as filhas seguem o ADR-0012 (integração) e o ADR-0013 (E2E: pré-condições pela API `/_test/*`, jornada pela interface, cliente em Chromium e WebKit). Todas da fundação; nenhuma decisão transversal nova.

**Regras de arquitetura a garantir (G3):** ARQ-01..ARQ-07 da SPEC-0001 continuam valendo para cada módulo novo; ARQ-07 garante que só `Catalog.Infrastructure` e o player da TV conhecem o YouTube.

## 4. Decomposição
| Spec | Título | Tier | Tipo | Tamanho | Depende de | Consome contrato de |
|---|---|---|---|---|---|---|
| SPEC-0011 | Bar, configurações da noite e termos de uso | full | feature | M | SPEC-0007, SPEC-0008, SPEC-0009 | — |
| SPEC-0012 | Tempo real por bar | full | feature | M | SPEC-0008, SPEC-0009 | — |
| SPEC-0013 | Entrada do cliente com comanda e PIN da TV | full | feature | M | SPEC-0008, SPEC-0009 | SPEC-0011@1, SPEC-0012@1, SPEC-0020@1 |
| SPEC-0015 | Catálogo e busca local | full | feature | M | SPEC-0007, SPEC-0008, SPEC-0009 | SPEC-0011@1, SPEC-0013@1, SPEC-0018@1 |
| SPEC-0014 | Busca externa no YouTube com controle de cota | full | feature | M | SPEC-0015 | SPEC-0008@1 |
| SPEC-0016 | Manutenção do catálogo | full | feature | M | SPEC-0014 | SPEC-0020@1, SPEC-0018@1 |
| SPEC-0018 | Fila justa (domínio e API) | full | feature | M | SPEC-0013, SPEC-0015 | SPEC-0011@1, SPEC-0012@1 |
| SPEC-0024 | Telas da fila, Minhas e pedido pela busca | full | feature | M | SPEC-0018 | SPEC-0020@1, SPEC-0012@1 |
| SPEC-0019 | Votos e auto-remoção | full | feature | M | SPEC-0024 | — |
| SPEC-0020 | Reprodução e controle do player | full | feature | M | SPEC-0007, SPEC-0008, SPEC-0009 | SPEC-0018@1, SPEC-0012@1, SPEC-0022@1 |
| SPEC-0021 | Tela da TV (e pareamento) | full | feature | M | SPEC-0020 | SPEC-0013@1, SPEC-0011@1, SPEC-0018@1 |
| SPEC-0017 | Modo karaokê | full | feature | M | SPEC-0014, SPEC-0020, SPEC-0024 | — |
| SPEC-0022 | Bloqueio de comanda e modo ouvinte | full | feature | M | SPEC-0013, SPEC-0018 | SPEC-0020@1 |
| SPEC-0023 | Painel do gerente | full | feature | M | SPEC-0021, SPEC-0022 | SPEC-0019@1, SPEC-0011@1 |

## 5. Estratégia de Entrega
- **Ambientes:** staging automático a cada merge; produção pelo pipeline com aprovação (G6), ao fim de cada onda.
- **Entrega por onda:** cada onda vai para produção, mas a jornada do cliente só fica visível para o bar piloto quando a flag do bar for ligada.
- **Feature flags:** `Venue.JukeboxEnabled` (por bar, padrão desligado) — controla se `/b/{slug}` aceita entrada e se a TV pareada toca a fila; ligada em staging desde o início; ligada no piloto quando os critérios de aceite estiverem verdes e o teste de Premium (SPEC-0007) aprovado; removida quando houver autocadastro de bares.
- **Rollback:** redeploy da versão anterior (SPEC-0006); as migrations de cada spec são aditivas; desligar a flag do bar tira o jukebox do ar sem deploy.
- **Métricas de sucesso pós-release:** no piloto, durante 4 sextas/sábados: % de comandas da noite que entram no Juker; músicas pedidas por noite; nenhuma comanda com mais de 1 música seguida enquanto outras esperam; nº de intervenções do gerente; TV sem queda maior que 2 min; feedback do dono ("acabaram as brigas?").

## 6. Riscos & Mitigações
- Cota do YouTube esgota numa noite cheia — catálogo semeado antes do piloto (SPEC-0016), cache de consultas, limite de buscas externas por comanda e degradação para "busque no catálogo" (SPEC-0014).
- YouTube restringe o uso ou revoga a chave — catálogo continua tocando o que já existe; fonte plugável (ADR-0007); risco aceito pelo fundador.
- Wi-Fi/4G ruim no bar — WASM com cache, reconexão do SignalR com releitura do estado (SPEC-0012), telas que funcionam com a rede lenta.
- Comanda falsa ou de fora do bar — PIN rotativo (15 min, aceita o anterior) e faixa de comandas configurada (SPEC-0013).
- Monopólio pelos votos — votos só reordenam dentro da vez (regra do fundador).

## 7. Critérios de Aceite do Épico
- [ ] O cliente entra sem cadastro, só com a comanda e o código da TV, e vários celulares da mesma comanda compartilham o limite — SPEC-0013:E2E-01
- [ ] A busca mostra resultados do catálogo enquanto o cliente digita — SPEC-0015:E2E-01
- [ ] "Não achou? Buscar no YouTube" traz músicas novas e elas passam a existir no catálogo — SPEC-0014:E2E-01
- [ ] Com o modo karaokê ligado, o pedido toca a versão karaokê na TV — SPEC-0017:E2E-01
- [ ] Uma comanda com 3 pedidos não toca a 2ª música antes da 1ª de quem chegou depois — SPEC-0024:E2E-01
- [ ] Votos reordenam dentro da mesma vez e saldo -5 tira a música da fila — SPEC-0019:E2E-01
- [ ] A TV toca a fila em sequência sozinha e o gerente consegue pular e pausar — SPEC-0020:E2E-01
- [ ] A TV mostra "Tocando agora – Pedido pela Comanda X", as próximas 3, o QR e o código — SPEC-0021:E2E-01
- [ ] A comanda bloqueada vira ouvinte, perde os pedidos pendentes e os votos dela deixam de contar — SPEC-0022:E2E-01
- [ ] O gerente modera tudo pelo painel no tablet e no celular — SPEC-0023:E2E-01
- [ ] Mudanças na fila chegam a todos os aparelhos em menos de 1 s (p95) — SPEC-0012:IT-02

## 8. Questões em Aberto
- [x] Limite por comanda — 3 músicas pendentes por comanda, compartilhado entre os celulares dela (thomas, 2026-09-30)
- [x] Duração máxima do vídeo — 10 min, configurável pelo bar (thomas, 2026-09-30)
- [x] Auto-remoção por votos — sim, saldo ≤ -5, configurável (thomas, 2026-09-30)
- [x] Rotação do PIN — a cada 15 min, aceitando o anterior (thomas, 2026-09-30)
- [x] Vários celulares por comanda — sim, dividem o limite e 1 voto por música por comanda (thomas, 2026-09-30)
- [x] Peso do voto — só reordena dentro da mesma vez (thomas, 2026-09-30)
- [x] Fim da noite — horário de virada configurável, padrão 06:00 (thomas, 2026-09-30)
- [x] Comanda válida — faixa configurável pelo bar (thomas, 2026-09-30)
- [x] Bloqueado vota? — não; modo ouvinte só leitura (thomas, 2026-09-30)
- [x] Bloqueio remove os pedidos pendentes? — sim (thomas, 2026-09-30)
- [x] Bloqueio anula os votos já dados? — sim (thomas, 2026-09-30)
- [x] Pausar/retomar do gerente — entra no MVP (thomas, 2026-09-30)
- [x] "Pedir de novo" — entra no MVP e conta no limite normalmente (thomas, 2026-09-30)
- [x] Fonte de mídia — YouTube Premium do bar, player embutido, risco dos termos aceito (thomas, 2026-09-30)
- [x] Buscas no YouTube por comanda — 5 por noite, configurável (thomas, 2026-09-30)
- [x] Repetição — sem duplicar na fila e bloqueio de 60 min após tocar, configurável (thomas, 2026-09-30)
- [x] Música pulada pelo gerente conta como a vez da comanda — sim (thomas, 2026-09-30)

## 9. Aprovação (H1)
Uma aprovação humana cobre o épico e as specs filhas apresentadas junto com ele. Registrada no frontmatter (`approved_by`, `approved_at`) do épico e de cada filha.

## 10. Registro de Impedimentos
| ID | Aberto em | Fase/Gate | Tipo | Descrição | Tentativas | Responsável | Resolução | Fechado em |
|---|---|---|---|---|---|---|---|---|

## 11. Relatório de Entrega

## 12. Emendas
| Versão | Data | Mudança | Motivo | Specs impactadas | Aprovado por |
|---|---|---|---|---|---|
