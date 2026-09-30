---
id: SPEC-0013
title: Entrada do cliente com comanda e PIN da TV
tier: full
type: feature
user_facing: true
status: approved
created: 2026-09-30
parent: SPEC-0010
depends_on: [SPEC-0008, SPEC-0009]
consumes_contract: [SPEC-0011@1, SPEC-0012@1, SPEC-0020@1]
contract_version: 1
touches: [src/Modules/Sessions/**, src/Web/Juker.Web.Client/Guest/Entry/**, src/Web/Juker.Web.Client/Guest/Shell/**, tests/Juker.Modules.Sessions.UnitTests/**, tests/Juker.IntegrationTests/Sessions/**, tests/Juker.Web.UnitTests/Guest/Entry/**, tests/Juker.E2ETests/Guest/Entry/**]
adrs: [ADR-0001, ADR-0003, ADR-0008]
external: []
size: M
approved_by: thomas
approved_at: 2026-09-30
---

# SPEC-0013 — Entrada do cliente com comanda e PIN da TV

## 1. Visão Geral
O cliente lê o QR, cai em `/b/{slug}`, digita o **número da comanda** e o **código de 4 dígitos da TV** e entra, sem cadastro. O código muda a cada 15 minutos (o anterior continua aceito), provando que a pessoa está no bar. Vários celulares da mesma comanda podem entrar; todos compartilham a mesma identidade (a comanda). A sessão dura até a virada da noite. Inclui o shell do app do cliente (cabeçalho com o chip "Comanda X" e a navegação Buscar / Fila / Minhas) e o "teaser" do que está tocando na tela de entrada.

## 2. Motivação & Escopo
**Motivação:** acabar com o tablet da parede sem criar atrito: entrar tem que levar segundos, e só quem está no bar pode pedir.

**Objetivos (dentro do escopo):**
- Módulo Sessions: PIN derivado por HMAC-SHA256 do segredo do bar e da janela de 15 min (`floor(unix/900)`), 4 dígitos; aceita a janela atual e a anterior.
- `GET /api/v1/tv/pin` (TvOfVenue) e `GET /api/v1/admin/pin` (ManagerOfVenue) com o PIN atual e a expiração; `PinRotated` notificado na troca (job leve por bar com TV conectada).
- Cookie de dispositivo `juker_device` (GUID aleatório, 1 ano, HttpOnly) criado na primeira visita.
- `POST /api/v1/b/{slug}/sessions` com comanda + PIN → sessão `GuestSession` (implementa o `IGuestSessionIssuer` da SPEC-0009) e reaproveita a sessão do mesmo dispositivo na mesma noite.
- Validações: bar existe, jukebox ligado, faixa de comandas configurada e a comanda dentro dela, PIN válido; 5 PINs errados por dispositivo em 10 min → bloqueio de 10 min.
- `GET /api/v1/b/{slug}/me` → comanda, modo (`participant` no MVP desta spec) e noite; `ISessionStatus` e `ISessionsModule` para os outros módulos.
- Tela de entrada (Stitch "Juker - Entrada"): logo, bar + "Jukebox ativa", "Você escolhe o som da noite", campo da comanda, 4 caixas do código, "Entrar na festa", teaser "Tocando agora" + "N na fila", "Sem cadastro. Sem baixar app."; estado de erro nas caixas.
- Shell do cliente: cabeçalho (logo, nome do bar, chip "Comanda X") e navegação inferior Buscar / Fila / Minhas (as páginas vêm das SPECs 0015 e 0018); sessão expirada → volta para a entrada.

**Não-objetivos (fora do escopo):**
- Bloqueio de comanda e modo ouvinte (SPEC-0022).
- Busca, fila e votos (SPECs 0015, 0018, 0019).
- Integração com o PDV para validar a comanda.

## 3. Dependências
- **Implementações necessárias:** SPEC-0009 — esquema Guest, `IGuestSessionIssuer`/`ISessionStatus`, rate limiting.
- **Contratos consumidos:** SPEC-0011@1 (`IVenuesModule`: faixa de comandas, virada, flag `JukeboxEnabled`); SPEC-0012@1 (`PinRotated`); SPEC-0020@1 (`GET /api/v1/b/{slug}/now-playing` para o teaser; até a SPEC-0020 existir, o teaser usa um stub do contrato e fica oculto se o endpoint responder 404).
- **Pré-requisitos externos:** N/A.

## 4. Decisão Arquitetural
**Contexto:** projeto novo; ADR-0001 (módulo Sessions), ADR-0003 (tela do cliente em WASM com prerender), ADR-0008 (sessão por comanda).

**Decisão:** PIN sem estado (derivado por HMAC, sem tabela de PINs); sessão persistida no schema `sessions` e emitida no cookie Guest; `ISessionsModule` em `Juker.Modules.Sessions.Contracts`.

**Justificativa:** o PIN derivado dispensa job de rotação com escrita no banco e é verificável em qualquer instância; a sessão persistida permite revogar, contar comandas conectadas e trocar o modo depois (SPEC-0022).

**Desvio do padrão existente:** Nenhum.

**Alternativas descartadas:** PIN aleatório gravado a cada rotação (estado extra sem ganho); um celular por comanda (o fundador escolheu compartilhar).

**ADRs:** ADR-0001, ADR-0003, ADR-0008.

## 5. Requisitos Não-Funcionais
- **Desempenho e escala:** `POST /sessions` p95 < 150 ms — medido no IT-01; tela de entrada interativa em ≤ 3 s no 4G emulado (Lighthouse, 7.6).
- **Segurança:** força bruta do PIN contida (5 erros por dispositivo em 10 min + limite de 10/min por IP da SPEC-0009); segredo do PIN por bar, 32 bytes aleatórios, nunca exposto; comparação de PIN em tempo constante — UT-02 e IT-03.
- **Privacidade e dados pessoais:** armazena comanda, ID de dispositivo aleatório e horário; nada de IP na tabela; sessões apagadas 30 dias após a noite (job diário) — IT-05.
- **Disponibilidade e resiliência:** o PIN não depende do banco (HMAC), e a TV continua exibindo o código mesmo com o banco lento.
- **Acessibilidade (UI):** caixas do código com `inputmode="numeric"`, `autocomplete="one-time-code"` e rótulo "Dígito N do código"; erros anunciados com `aria-live` — UT-03 e E2E-01 (axe).
- **Custo:** N/A.

## 6. Artefato A — Contrato
**Interface:** API de entrada, PIN, sessão atual, contratos do módulo e telas.

```text
POST /api/v1/b/{slug}/sessions   { "comanda": 12, "pin": "4821" }   (cookie juker_device)
  201 { "comanda": 12, "mode": "participant", "night": "2026-10-02" } + Set-Cookie (Guest)
  200 (mesmo corpo) quando o dispositivo já tem sessão para essa comanda nesta noite
  404 VENUE_NOT_FOUND
  403 JUKEBOX_DISABLED        "O jukebox deste bar está desligado agora."
  422 VENUE_NOT_CONFIGURED    "O bar ainda está configurando o jukebox."
  422 COMANDA_OUT_OF_RANGE    "Comanda inválida. Confira o número na sua comanda."
  422 PIN_INVALID             "Código expirado ou inválido. Confira o código na TV."
  429 TOO_MANY_ATTEMPTS       "Muitas tentativas. Tente de novo em alguns minutos."
GET /api/v1/b/{slug}/me  (GuestOfVenue) → 200 { comanda, mode, night } | 401
GET /api/v1/tv/pin       (TvOfVenue)      → 200 { "pin": "4821", "expiresAt": "..." }
GET /api/v1/admin/pin    (ManagerOfVenue) → 200 (idem)

// Juker.Modules.Sessions.Contracts
public enum ComandaMode { Participant, Listener }
public interface ISessionsModule {
  Task<GuestIdentity?> GetAsync(Guid sessionId, CancellationToken ct);
  Task<ComandaMode> GetModeAsync(VenueId v, DateOnly night, int comanda, CancellationToken ct);
  Task<int> CountConnectedComandasAsync(VenueId v, DateOnly night, CancellationToken ct);
}
public sealed record GuestIdentity(VenueId Venue, DateOnly Night, int Comanda, Guid DeviceId, ComandaMode Mode);

Telas:
  /b/{slug}         → entrada (se já tiver sessão válida, redireciona para /b/{slug}/fila)
  /b/{slug}/{aba}   → shell: cabeçalho + navegação "Buscar" "Fila" "Minhas"; sem sessão → /b/{slug}
```

**Arquivos/módulos afetados:** ver `touches` no frontmatter.

### 6.1 Mapa de Comportamentos
| Cenário | Condição / Entrada | Resultado esperado | Testes |
|---|---|---|---|
| PIN por janela | mesmo segredo, instantes na mesma janela de 15 min | mesmo PIN de 4 dígitos; janela seguinte → outro PIN | UT-01 |
| PIN anterior aceito | PIN da janela anterior logo após a troca | aceito; PIN de duas janelas atrás → recusado | UT-01, IT-01 |
| Entrada com sucesso | comanda 12 na faixa 1–300 e PIN atual | 201, cookie Guest e shell com o chip "Comanda 12" | IT-01, E2E-01 |
| Segundo celular da mesma comanda | outro dispositivo com comanda 12 e PIN válido | 201; ambos identificados como comanda 12 | IT-02, E2E-01 |
| Reentrada | mesmo dispositivo, mesma noite | 200 com a mesma sessão | IT-02 |
| Comanda fora da faixa | comanda 301 com faixa 1–300 | 422 `COMANDA_OUT_OF_RANGE`; mensagem no campo | IT-01, E2E-02 |
| PIN errado | PIN inválido | 422 `PIN_INVALID`; caixas em vermelho com a mensagem | IT-01, E2E-02 |
| Força bruta | 6º PIN errado do mesmo dispositivo em 10 min | 429 `TOO_MANY_ATTEMPTS` | IT-03 |
| Bar sem faixa | `configured=false` | 422 `VENUE_NOT_CONFIGURED` | IT-04 |
| Jukebox desligado | `JukeboxEnabled=false` | 403 `JUKEBOX_DISABLED` e tela "O jukebox deste bar está desligado agora." | IT-04 |
| Virada da noite | sessão de 02/10 às 06:01 de 03/10 | 401 em `/me`; o app volta para a entrada | IT-05, UT-04 |
| PIN para TV/gerente | `GET /tv/pin` e `/admin/pin` | PIN atual + expiração; `PinRotated` notificado na troca | IT-06 |
| Teaser | há música tocando | "Tocando agora" com título e comanda, e "N na fila" | UT-03 |
| Limpeza | sessões com mais de 30 dias | removidas pelo job diário | IT-05 |
| Contrato do módulo | `ISessionsModule` | semântica de modo e contagem de comandas conectadas | CT-01 |

## 7. Artefato B — Plano de Testes (TDD)

### 7.1 Testes de Caracterização
- N/A — funcionalidade nova.

### 7.2 Testes Unitários
- **UT-01** — Dado um segredo fixo, quando o PIN é calculado em 21:00:00 e 21:14:59 (mesma janela) e 21:15:00, então os dois primeiros são iguais e o terceiro difere; e `Verify` aceita a janela atual e a anterior, mas não a de duas janelas atrás.
- **UT-02** — Dado `PinVerifier`, quando compara PINs, então usa comparação em tempo constante (`CryptographicOperations.FixedTimeEquals`) e rejeita PINs com formato diferente de 4 dígitos.
- **UT-03** — Dado a tela de entrada (bUnit) com o teaser "Highway to Hell – Comanda 3" e "12 na fila", então os exibe; com o erro `PIN_INVALID`, as caixas ficam no estado de erro e a mensagem está numa região `aria-live`; as caixas têm `inputmode="numeric"` e rótulos.
- **UT-04** — Dado uma `GuestIdentity` da noite 02/10 e o relógio às 06:01 de 03/10 com virada 06:00, então `IsActive` retorna falso.

### 7.3 Testes de Integração
- **IT-01** — Com Postgres real e bar configurado (1–300): comanda 12 + PIN atual → 201 e cookie Guest aceito por `/me`; PIN da janela anterior → 201; comanda 301 → 422 `COMANDA_OUT_OF_RANGE`; PIN errado → 422 `PIN_INVALID`; mede o p95 do `POST`.
- **IT-02** — Dois dispositivos diferentes entram com a comanda 12 → ambos 201 e `ISessionsModule.CountConnectedComandasAsync` = 1; o mesmo dispositivo entrando de novo → 200 com o mesmo `sessionId`.
- **IT-03** — 5 PINs errados do mesmo dispositivo e o 6º → 429 `TOO_MANY_ATTEMPTS`; após 10 min (FakeTimeProvider), volta a aceitar.
- **IT-04** — Bar com `configured=false` → 422 `VENUE_NOT_CONFIGURED`; bar com `JukeboxEnabled=false` → 403 `JUKEBOX_DISABLED`.
- **IT-05** — Sessão da noite anterior após a virada → 401 em `/me`; o job de limpeza remove sessões com mais de 30 dias e mantém as recentes.
- **IT-06** — `GET /api/v1/tv/pin` (token Tv) e `GET /api/v1/admin/pin` (gerente) retornam o PIN atual e a expiração; ao cruzar a janela, `PinRotated` chega aos grupos `tv` e `staff`.

### 7.4 Testes de Contrato
- **CT-01** — Suíte de contrato de `ISessionsModule` contra a implementação real (modo `Participant` por padrão; contagem de comandas distintas por noite), consumida pelas SPECs 0018, 0021, 0022 e 0023; e verificação de que o cookie emitido cumpre o contrato `IGuestSessionIssuer` da SPEC-0009 (CT-01 de lá).

### 7.5 Testes E2E
- **E2E-01** — Celular A abre `/b/rock`, digita comanda 12 e o PIN exibido pelo endpoint da TV de teste, toca "Entrar na festa" e vê o shell com "Comanda 12" e a navegação Buscar / Fila / Minhas; o celular B (outro contexto de navegador) entra com a mesma comanda e também vê "Comanda 12"; ao recarregar A, continua dentro; a tela de entrada não tem violações axe.
- **E2E-02** — Celular digita a comanda 999 (fora da faixa) e vê "Comanda inválida. Confira o número na sua comanda."; corrige para 12, digita um código errado e vê "Código expirado ou inválido. Confira o código na TV." com as caixas em vermelho.

### 7.6 Outros
- Performance: Lighthouse mobile na tela de entrada em staging (tempo até interativo ≤ 3 s no 4G emulado).

**Dublês e dados de teste:** segredo de PIN fixo por teste; `FakeTimeProvider`; stub do endpoint `now-playing` (contrato da SPEC-0020) até ela existir.

**Ambiente de execução:** Testcontainers para IT; AppHost + Playwright para E2E; staging para o smoke.

## 8. Plano de Rollout
- **Estratégia:** entrega atrás da flag `JukeboxEnabled` (desligada no piloto até o épico estar pronto).
- **Dados/schema:** migration aditiva `sessions.Initial` (`guest_sessions`, `pin_secrets`, `entry_attempts`); o segredo do PIN é gerado para os bares existentes na migration.
- **Compatibilidade:** N/A.
- **Observabilidade:** contadores `juker.sessions.created`, `juker.sessions.pin_invalid`, `juker.sessions.rate_limited`.
- **Rollback:** redeploy anterior; desligar a flag do bar.
- **Etapas de migração/coexistência:** N/A.

## 9. Questões em Aberto
- [x] Rotação do PIN — a cada 15 min, aceitando o anterior (thomas, 2026-09-30)
- [x] Vários celulares por comanda — sim, dividem o limite e o voto (thomas, 2026-09-30)
- [x] Comanda válida — faixa configurável pelo bar (thomas, 2026-09-30)

## 10. Aprovação (H1)
Registrada no frontmatter (`approved_by`, `approved_at`) somente depois que o humano responder "Aprovado". O arquiteto nunca aprova a própria spec.

## 11. Checklist de Implementação

**Fase 1: Regras e componentes (unitário)**
- [ ] Red: escrever UT-01, UT-02, UT-03, UT-04 com a tag `SPEC-0013:<ID>` e confirmar que falham pelo motivo certo
- [ ] Green: implementar o mínimo para passar, seguindo os ADRs de fundação e o padrão de slices
- [ ] Refactor mantendo tudo verde
- [ ] Validar: build + lint + suíte de UT (G2)

**Fase 2: Integração e contrato**
- [ ] Red: escrever IT-01, IT-02, IT-03, IT-04, IT-05, IT-06, CT-01 com a tag `SPEC-0013:<ID>` (Postgres real, esquemas de auth reais, `DrainOutboxAsync`, WireMock — ADR-0012) e confirmar que falham pelo motivo certo
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
| G0 Spec | PASS | `spec_graph.py validate SPEC-0013` → G0 [SPEC-0013]: PASS — 0 erro(s), 0 aviso(s); checklist de julgamento do G0 revisado pelo Architect | 2026-09-30 |
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
