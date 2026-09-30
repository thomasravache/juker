---
id: SPEC-0008
title: Observabilidade básica
tier: full
type: foundation
user_facing: false
status: approved
created: 2026-09-30
parent: SPEC-0001
depends_on: [SPEC-0007]
consumes_contract: []
contract_version: 1
touches: [src/Aspire/Juker.ServiceDefaults/**, src/BuildingBlocks/Juker.Infrastructure/Observability/**, deploy/observability/**, tests/Juker.IntegrationTests/Observability/**, tests/Juker.BuildingBlocks.UnitTests/Observability/**, docs/operations.md]
adrs: [ADR-0010]
external: []
size: M
approved_by: thomas
approved_at: 2026-09-30
---

# SPEC-0008 — Observabilidade básica

## 1. Visão Geral
Liga a telemetria de ponta a ponta: OpenTelemetry (logs, métricas, traces) exportado por OTLP para o backend gerenciado escolhido, o `Meter` "Juker" com os instrumentos de negócio que os módulos vão alimentar, redação de dados sensíveis nos logs, uptime check externo e as primeiras regras de alerta (indisponibilidade e taxa de erro 5xx) versionadas como código.

## 2. Motivação & Escopo
**Motivação:** numa sexta-feira à noite, o fundador precisa saber que algo quebrou antes do dono do bar ligar para ele.

**Objetivos (dentro do escopo):**
- ServiceDefaults configurando o exporter OTLP a partir de `OTEL_EXPORTER_OTLP_ENDPOINT`/`OTEL_EXPORTER_OTLP_HEADERS`; amostragem de traces de 10% em produção, com 100% dos erros (tail por status de erro do span raiz).
- `JukerMetrics` (Meter "Juker") com os instrumentos definidos e documentados: `juker.tv.online` (gauge), `juker.queue.length` (gauge), `juker.requests.added` (counter), `juker.youtube.quota.used` (counter), `juker.playback.errors` (counter), `juker.outbox.pending`/`failed` (gauges da SPEC-0004).
- Processador de logs que remove IP, `Cookie`, `Authorization`, `Set-Cookie` e parâmetros `pin`/`token`.
- Verificar e registrar o plano gratuito vigente do backend (Grafana Cloud ou alternativa OTLP, ADR-0010), com fonte e data em `docs/operations.md`.
- Alertas como código em `deploy/observability/alerts.yaml` (versionados e validados agora; **ativados no backend quando houver ambiente, com a SPEC-0006**): `/health/ready` indisponível por 2 min; taxa de 5xx > 2% em 5 min. Notificação para o e-mail e o celular do fundador.
- Uptime check externo (a cada 1 min em `/health/ready` de staging e produção) **definido em `deploy/observability/uptime.yaml` e ativado com a SPEC-0006**.

**Não-objetivos (fora do escopo):**
- Alertas de negócio (TV offline, cota do YouTube): entram nas specs que emitem as métricas (SPEC-0014, SPEC-0020).
- Dashboards elaborados.

## 3. Dependências
- **Implementações necessárias:** SPEC-0007 — host publicado pelo pipeline com `/health/*` e o endpoint público do bar para gerar tráfego de teste.
- **Contratos consumidos:** N/A.
- **Pré-requisitos externos:** N/A nesta fase — os testes usam um OpenTelemetry Collector local em container; a conta no backend gerenciado e os segredos `OTLP_*` entram com a SPEC-0006.

## 4. Decisão Arquitetural
**Contexto:** projeto novo; ADR-0010.

**Decisão:** toda a instrumentação passa pelo ServiceDefaults e pelo `JukerMetrics` do Infrastructure; os módulos só chamam os instrumentos, nunca configuram exporters.

**Justificativa:** um ponto único de configuração permite trocar o fornecedor mudando só variáveis de ambiente.

**Desvio do padrão existente:** Nenhum.

**Alternativas descartadas:** SDK proprietário do fornecedor (lock-in; ADR-0010).

**ADRs:** ADR-0010.

## 5. Requisitos Não-Funcionais
- **Desempenho e escala:** overhead da telemetria abaixo de 5% de CPU no smoke — comparação com e sem exporter no IT-02.
- **Segurança:** a chave do OTLP só vem do ambiente — revisão G4.
- **Privacidade e dados pessoais:** nenhum IP, cookie, token ou PIN nos logs exportados — UT-01 e IT-03.
- **Disponibilidade e resiliência:** exporter indisponível não afeta as requisições (fila com descarte) — IT-02.
- **Acessibilidade (UI):** N/A.
- **Custo:** dentro do plano gratuito — volume diário de ingestão acompanhado na primeira semana de staging.

## 6. Artefato A — Contrato
**Interface:** variáveis de ambiente de telemetria, `JukerMetrics` e regras de alerta.

```text
Ambiente:
  OTEL_EXPORTER_OTLP_ENDPOINT, OTEL_EXPORTER_OTLP_HEADERS, OTEL_SERVICE_NAME=juker,
  OTEL_RESOURCE_ATTRIBUTES=deployment.environment=<staging|production>

public sealed class JukerMetrics            // Meter "Juker"
  void TvOnline(VenueId venue, bool online)
  void QueueLength(VenueId venue, int length)
  void RequestAdded(VenueId venue, string mediaKind)      // clip | karaoke
  void YouTubeQuotaUsed(int units, string operation)      // search | videos
  void PlaybackError(VenueId venue, string code)

deploy/observability/alerts.yaml
  - juker-ready-down:  probe /health/ready falhando por 2 min  → crítico
  - juker-5xx-rate:    rate(http.server.request.duration{status>=500}) / total > 2% em 5 min → alto
```

**Arquivos/módulos afetados:** ver `touches` no frontmatter.

### 6.1 Mapa de Comportamentos
| Cenário | Condição / Entrada | Resultado esperado | Testes |
|---|---|---|---|
| Redação de logs | log com IP, `Authorization`, `Cookie` e `?pin=1234` | campos removidos ou mascarados antes da exportação | UT-01, IT-03 |
| Health exposto | host de pé com Postgres | `/health/live` e `/health/ready` = 200 e span `health` exportado | IT-01 |
| Telemetria chega ao coletor | requisições 200 e 500 geradas | métricas `http.server.request.duration` com `status` e o counter `juker.requests.added` chegam a um OpenTelemetry Collector real | IT-02 |
| Exporter fora do ar | coletor parado | requisições continuam respondendo normalmente | IT-02 |
| Instrumentos de negócio | chamada a cada método do `JukerMetrics` | instrumento com o nome e as tags do contrato | UT-02, CT-01 |
| Alertas versionados | `alerts.yaml` | regras válidas no formato do backend e com os limiares do contrato | CT-01 |

## 7. Artefato B — Plano de Testes (TDD)

### 7.1 Testes de Caracterização
- N/A — projeto novo.

### 7.2 Testes Unitários
- **UT-01** — Dado um registro de log com `client.address=10.0.0.1`, cabeçalhos `Authorization`/`Cookie` e a URL `/api/v1/x?pin=1234&token=abc`, quando passa pelo processador de redação, então IP e cabeçalhos são removidos e `pin`/`token` ficam como `***`.
- **UT-02** — Dado um `MeterListener`, quando cada método do `JukerMetrics` é chamado, então o instrumento emitido tem o nome e as tags do contrato (`venue.id`, `media.kind`, `operation`, `code`).

### 7.3 Testes de Integração
- **IT-01** — Com o host e o Postgres reais, `/health/live` e `/health/ready` respondem 200 e o span do health check é exportado para um OpenTelemetry Collector em container (Testcontainers) com saída em arquivo.
- **IT-02** — Com o Collector real, gerar 20 requisições 200 e 5 com erro 500 (endpoint de teste só em `Testing`): as métricas de duração com `http.response.status_code` chegam ao Collector; com o Collector parado, as mesmas requisições continuam respondendo normalmente.
- **IT-03** — Com o Collector real, uma requisição com `Cookie` e `?pin=1234` gera logs exportados sem o cookie e com o PIN mascarado.

### 7.4 Testes de Contrato
- **CT-01** — `alerts.yaml` valida contra o schema de regras do backend escolhido (validador do fornecedor ou `promtool` para regras no formato Prometheus) e contém as duas regras com os limiares do contrato; os nomes dos instrumentos do `JukerMetrics` batem com a tabela usada pelas SPECs 0014 e 0020.

### 7.5 Testes E2E
- N/A — user_facing: false.

### 7.6 Outros
- Disparo real do alerta `juker-ready-down` no celular do fundador: **transferido para a SPEC-0006** (exige ambiente e backend gerenciado).

**Dublês e dados de teste:** endpoint `/_test/fail` registrado apenas no ambiente `Testing`.

**Ambiente de execução:** Testcontainers (Collector `otel/opentelemetry-collector-contrib`, Postgres 18) local e no CI; alerta real em staging.

## 8. Plano de Rollout
- **Estratégia:** deploy direto; os segredos OTLP entram primeiro no environment `staging`.
- **Dados/schema:** N/A.
- **Compatibilidade:** nomes de métricas são contrato (CT-01); renomear exige emenda.
- **Observabilidade:** esta spec é a observabilidade; as regras ficam em `deploy/observability/`.
- **Rollback:** remover as variáveis OTLP desliga a exportação sem novo deploy de código.
- **Etapas de migração/coexistência:** N/A.

## 9. Questões em Aberto
Nenhuma.

## 10. Aprovação (H1)
Registrada no frontmatter (`approved_by`, `approved_at`) somente depois que o humano responder "Aprovado". O arquiteto nunca aprova a própria spec.

## 11. Checklist de Implementação

**Fase 1: Regras e componentes (unitário)**
- [ ] Red: escrever UT-01, UT-02 com a tag `SPEC-0008:<ID>` e confirmar que falham pelo motivo certo
- [ ] Green: implementar o mínimo para passar, seguindo os ADRs de fundação e o padrão de slices
- [ ] Refactor mantendo tudo verde
- [ ] Validar: build + lint + suíte de UT (G2)

**Fase 2: Integração e contrato**
- [ ] Red: escrever IT-01, IT-02, IT-03, CT-01 com a tag `SPEC-0008:<ID>` (Postgres real, esquemas de auth reais, `DrainOutboxAsync`, WireMock — ADR-0012) e confirmar que falham pelo motivo certo
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
| G0 Spec | PASS | `spec_graph.py validate SPEC-0008` → G0 [SPEC-0008]: PASS — 0 erro(s), 0 aviso(s); checklist de julgamento do G0 revisado pelo Architect (revisão ADR-0012/0013) | 2026-09-30 |
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
| escopo | 2026-09-30 | Alertas e uptime check versionados agora e ativados com a SPEC-0006; conta do backend e segredos OTLP saem dos pré-requisitos; disparo real do alerta transferido para a SPEC-0006 | CD e ambientes adiados por decisão do fundador | SPEC-0006 | thomas — autorização prévia no chat (2026-09-30: "deixar a infra de fora… optar pela recomendação"), a confirmar no overview |
