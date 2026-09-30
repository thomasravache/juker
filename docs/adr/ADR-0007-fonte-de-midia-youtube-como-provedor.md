---
id: ADR-0007
title: Fonte de mídia como provedor plugável; YouTube (IFrame Player + Data API v3) como primeiro provedor, com catálogo próprio
status: accepted
origin: decision
date: 2026-09-30
decision_makers: [thomas]
consulted: [claude]
informed: []
supersedes:
superseded_by:
enforced_by: ARQ-07 (somente Catalog/Infrastructure e o player da TV conhecem YouTube) e SPEC-0014:IT-01
---

# ADR-0007 — Fonte de mídia como provedor plugável; YouTube como primeiro provedor, com catálogo próprio

## Contexto e Problema
O produto depende de uma fonte de vídeo (clipes e karaokê). Analisamos YouTube, Spotify, Soundtrack e Deezer (conversa de 2026-09-29/30): o Spotify limita apps novos a 5 usuários e só permite adicionar ao fim da fila; o Soundtrack proíbe o controle da reprodução pelos visitantes; o Deezer for Business depende de parceria. O YouTube tem vídeo e karaokê, mas cota de busca baixa (10.000 unidades/dia, busca = 100) e termos voltados a uso pessoal. **O fundador aceitou conscientemente o risco dos termos**, com a conta do YouTube Premium e o ECAD sob responsabilidade do bar.

## Direcionadores da Decisão
- Ter vídeo e karaokê (diferencial contra o concorrente, que usa Spotify).
- Sobreviver a uma restrição da fonte: trocar de provedor sem reescrever fila, votos e TV.
- Economizar cota de busca.
- Não guardar áudio nem vídeo; respeitar a regra de 30 dias para dados da API.

## Opções Consideradas
- Interface `IMediaProvider` no Catalog + provedor YouTube (Data API v3 para busca/detalhes, IFrame Player na TV) + catálogo próprio com cache permanente de identidade da música
- YouTube acoplado diretamente nos módulos
- Automação via extensão do navegador no youtube.com

## Resultado da Decisão
**Opção escolhida:** "provedor plugável + catálogo próprio", porque isola a dependência e faz o catálogo absorver a maior parte das buscas. Regras:
- Somente `Catalog/Infrastructure/YouTube*` fala com a Data API. Somente o componente de player da TV (`src/Web/.../Tv/YouTubePlayer*` + JS interop) usa o IFrame Player.
- Tudo fora disso usa `MediaRef { Provider, ExternalId }`, sem tipos do YouTube.
- Busca: `search.list` com `type=video`, `videoEmbeddable=true`; detalhes/duração: `videos.list` (1 unidade por até 50 IDs).
- Dados da API são atualizados ou apagados a cada 30 dias (job em SPEC-0016). A identidade da música (título/artista normalizados, estatísticas, sinais de qualidade) é dado nosso.
- Nada sobreposto ao vídeo na TV. Letreiro e fila ficam fora da área do player.
- Extensão do navegador descartada (viola a proibição de automação e é frágil); fica só como plano de emergência documentado.

### Consequências
- **Boa**, porque perder o YouTube derruba a busca de músicas novas, não o produto: o catálogo segue tocando o que já existe e outro provedor entra no mesmo `MediaRef`.
- **Ruim**, porque o risco jurídico e de plataforma continua (aceito pelo fundador) e a cota exige o controle de SPEC-0014.

### Confirmação (G3)
ARQ-07: nenhum tipo cujo namespace contenha `YouTube` fora de `Juker.Modules.Catalog.Infrastructure` e `Juker.Web.Client.Tv`. SPEC-0014:IT-01 verifica o provedor contra um servidor HTTP fake com o contrato da Data API.

## Prós e Contras das Opções
| Critério (peso) | Plugável + catálogo | YouTube acoplado | Extensão |
|---|---|---|---|
| Resiliência a restrição da fonte (5) | 5 | 1 | 2 |
| Economia de cota (4) | 5 | 2 | 5 |
| Conformidade com os termos técnicos (4) | 3 | 3 | 1 |
| Custo de implementação (3) | 3 | 5 | 2 |
| **Total ponderado** | **66** | **38** | **40** |

## Mais Informações
- YouTube API Services — Developer Policies (retenção de 30 dias, um projeto por API Client) — https://developers.google.com/youtube/terms/developer-policies (2026-09-30).
- YouTube Termos de Serviço (uso pessoal/não comercial, player incorporado, proibição de automação) — https://www.youtube.com/static?template=terms (2026-09-30).
- Spotify Dev Mode (5 usuários por app, fev/2026) — https://developer.spotify.com/documentation/web-api/tutorials/february-2026-migration-guide (2026-09-30).
- Soundtrack API (jukebox proibido) — https://api.soundtrackyourbrand.com/v2/docs (2026-09-30).
