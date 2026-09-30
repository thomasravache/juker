---
id: SPEC-0011
title: Bar, configurações da noite e termos de uso
tier: full
type: feature
user_facing: true
status: approved
created: 2026-09-30
parent: SPEC-0010
depends_on: [SPEC-0007, SPEC-0008, SPEC-0009]
consumes_contract: []
contract_version: 1
touches: [src/Modules/Venues/Juker.Modules.Venues/Features/Settings/**, src/Modules/Venues/Juker.Modules.Venues/Features/Terms/**, src/Modules/Venues/Juker.Modules.Venues/Domain/**, src/Modules/Venues/Juker.Modules.Venues/Infrastructure/Migrations/**, src/Modules/Venues/Juker.Modules.Venues.Contracts/**, src/Web/Juker.Web/Components/Admin/Settings/**, src/Web/Juker.Web/Components/Admin/Terms/**, tests/Juker.Modules.Venues.UnitTests/Settings/**, tests/Juker.IntegrationTests/Venues/Settings/**, tests/Juker.E2ETests/Admin/Settings/**]
adrs: [ADR-0001, ADR-0006, ADR-0008]
external: []
size: M
approved_by: thomas
approved_at: 2026-09-30
---

# SPEC-0011 — Bar, configurações da noite e termos de uso

## 1. Visão Geral
Dá ao bar as regras que o resto do jukebox usa: faixa de comandas válidas, limite de músicas por comanda, duração máxima do vídeo, saldo de auto-remoção, horário de virada da noite, limite de buscas no YouTube por comanda, janela anti-repetição e os avisos que aparecem na TV. Inclui a tela **Ajustes** do painel, o aceite dos **termos de uso** (o bar declara que é responsável pela conta do YouTube Premium e pelo ECAD) e a flag `JukeboxEnabled` por bar.

## 2. Motivação & Escopo
**Motivação:** todas as regras decididas pelo fundador são configuráveis por bar; a fila, a entrada e a busca precisam ler os mesmos valores, e o bar precisa aceitar formalmente as responsabilidades sobre o conteúdo antes de usar.

**Objetivos (dentro do escopo):**
- Entidade `VenueSettings` com os campos e padrões:
  | Campo | Padrão | Faixa |
  |---|---|---|
  | `ComandaMin` / `ComandaMax` | definido pelo gerente no primeiro acesso (sem padrão) | 1..9999, min ≤ max |
  | `MaxPendingPerComanda` | 3 | 1..10 |
  | `MaxVideoDurationMinutes` | 10 | 3..20 |
  | `AutoRemoveScore` | -5 | -20..-2 |
  | `NightCutoff` | 06:00 | 00:00..11:59 |
  | `ExternalSearchesPerComandaPerNight` | 5 | 0..50 |
  | `RepeatWindowMinutes` | 60 | 0..240 |
  | `Announcements` | vazio | até 5 textos de até 80 caracteres |
- Fuso fixo `America/Sao_Paulo` no MVP; cálculo da **noite de negócio**: data local de (agora − `NightCutoff`).
- Flag `JukeboxEnabled` (padrão falso) alterada pela CLI `juker admin set-jukebox --venue <slug> --enabled true|false`.
- Termos de uso versionados (`2026-10`): o gerente precisa aceitar no primeiro acesso antes de usar o painel; o aceite (quem, versão, quando) fica registrado.
- Endpoints do painel e contrato `IVenuesModule` para os outros módulos.
- Evento de integração `VenueSettingsChanged` (outbox) quando algo muda.
- Tela `/admin/ajustes` (formulário com validação e avisos) e tela `/admin/termos`.

**Não-objetivos (fora do escopo):**
- Pareamento da TV (SPEC-0020) e gestão de comandas (SPEC-0023), mesmo que apareçam no menu.
- Múltiplos fusos horários, horário de funcionamento e vários gerentes com papéis diferentes.
- Autocadastro de bar.

## 3. Dependências
- **Implementações necessárias:** SPEC-0007 (módulo Venues e entidade `Venue`), SPEC-0009 (login do gerente e política `ManagerOfVenue`).
- **Contratos consumidos:** N/A.
- **Pré-requisitos externos:** texto dos termos de uso revisado pelo fundador (idealmente com apoio jurídico) antes de ligar o piloto; a spec entrega a estrutura e um texto-base.

## 4. Decisão Arquitetural
**Contexto:** projeto novo; ADR-0001 (slices no módulo Venues), ADR-0006 (contrato síncrono + evento), ADR-0008 (tenant pelo principal do gerente).

**Decisão:** slices `Features/Settings/{GetSettings,UpdateSettings}` e `Features/Terms/{GetTerms,AcceptTerms}`; `IVenuesModule` em `Juker.Modules.Venues.Contracts` com cache em memória invalidado pelo `VenueSettingsChanged`.

**Justificativa:** os outros módulos leem as configurações a cada pedido; o contrato síncrono com cache mantém a leitura barata e coerente.

**Desvio do padrão existente:** Nenhum.

**Alternativas descartadas:** cada módulo com a própria cópia das configurações (divergência entre módulos); configuração em arquivo (o bar precisa ajustar sozinho).

**ADRs:** ADR-0001, ADR-0006, ADR-0008.

## 5. Requisitos Não-Funcionais
- **Desempenho e escala:** `IVenuesModule.GetSettingsAsync` p95 < 1 ms com cache quente — medido no IT-03.
- **Segurança:** só `ManagerOfVenue` altera; o `VenueId` vem do principal (ADR-0008); textos dos avisos são tratados como texto puro (sem HTML) — UT-02 e IT-02.
- **Privacidade e dados pessoais:** registro do aceite com o ID do membro da equipe e o horário (LGPD: prova de consentimento contratual).
- **Disponibilidade e resiliência:** mudança nas configurações chega aos consumidores em até 2 s pelo evento — IT-03.
- **Acessibilidade (UI):** `/admin/ajustes` e `/admin/termos` sem violações axe A/AA; todos os campos com rótulo — E2E-01.
- **Custo:** N/A.

## 6. Artefato A — Contrato
**Interface:** API do painel, contrato `IVenuesModule` e evento `VenueSettingsChanged`.

```text
GET  /api/v1/admin/settings                          (ManagerOfVenue)
  200 { comandaMin, comandaMax, maxPendingPerComanda, maxVideoDurationMinutes, autoRemoveScore,
        nightCutoff: "06:00", externalSearchesPerComandaPerNight, repeatWindowMinutes,
        announcements: [string], configured: bool }
PUT  /api/v1/admin/settings   (mesmo corpo, sem "configured")
  200 corpo atualizado
  400 VALIDATION (errors por campo: fora da faixa, comandaMin > comandaMax, aviso > 80, > 5 avisos)
  409 TERMS_NOT_ACCEPTED
GET  /api/v1/admin/terms  → 200 { version: "2026-10", text, acceptedAt: null|datetime }
POST /api/v1/admin/terms/accept { version } → 204 | 400 TERMS_VERSION_OUTDATED

// Juker.Modules.Venues.Contracts
public interface IVenuesModule {
  Task<VenueInfo?> FindBySlugAsync(string slug, CancellationToken ct);
  Task<VenueRules> GetRulesAsync(VenueId venue, CancellationToken ct);
  DateOnly GetBusinessNight(VenueRules rules, DateTimeOffset instant);
}
public sealed record VenueInfo(VenueId Id, string Slug, string Name, bool JukeboxEnabled);
public sealed record VenueRules(int ComandaMin, int ComandaMax, int MaxPendingPerComanda,
  TimeSpan MaxVideoDuration, int AutoRemoveScore, TimeOnly NightCutoff,
  int ExternalSearchesPerComandaPerNight, TimeSpan RepeatWindow,
  IReadOnlyList<string> Announcements, bool Configured, TimeZoneInfo TimeZone);
public sealed record VenueSettingsChanged(Guid Id, DateTimeOffset OccurredAt, VenueId Venue) : IIntegrationEvent;

Telas:
  /admin/termos  → texto + botão "Li e aceito" (bloqueia o resto do painel até aceitar)
  /admin/ajustes → seções "Comandas", "Fila", "Busca", "Noite", "Avisos na TV"; botão "Salvar ajustes";
                   sucesso: toast "Ajustes salvos"; erro: mensagem no campo
  Primeiro acesso sem comandaMin/Max → /admin/ajustes com o aviso "Defina a faixa de comandas do bar"
```

**Arquivos/módulos afetados:** ver `touches` no frontmatter.

### 6.1 Mapa de Comportamentos
| Cenário | Condição / Entrada | Resultado esperado | Testes |
|---|---|---|---|
| Noite de negócio | 02:00 de 03/10 com virada 06:00 | noite = 02/10; às 06:00 de 03/10 → 03/10 | UT-01 |
| Validação de faixa | cada campo fora da faixa; comandaMin > comandaMax | erro de validação no campo | UT-02, IT-02 |
| Avisos inválidos | 6 avisos ou um com 81 caracteres; texto com `<script>` | 400 para quantidade/tamanho; HTML guardado e exibido como texto puro | UT-02, IT-02 |
| Termos não aceitos | gerente sem aceite chama `PUT /settings` | 409 `TERMS_NOT_ACCEPTED`; no painel, redirecionado a `/admin/termos` | IT-01, E2E-01 |
| Aceite dos termos | `POST /terms/accept` com a versão atual | 204; aceite registrado com membro, versão e horário | IT-01 |
| Versão antiga | aceite com versão diferente da atual | 400 `TERMS_VERSION_OUTDATED` | IT-01 |
| Salvar ajustes | gerente altera limite para 4 | 200; evento `VenueSettingsChanged` publicado; `GetRulesAsync` retorna 4 em até 2 s | IT-03, CT-01 |
| Primeiro acesso | bar sem faixa de comandas | painel abre em Ajustes com "Defina a faixa de comandas do bar"; `configured=false` | E2E-01 |
| Isolamento | gerente do bar A lê settings | só vê as do próprio bar | IT-02 |
| Flag do jukebox | `set-jukebox --enabled true` | `VenueInfo.JukeboxEnabled = true` | IT-04 |

## 7. Artefato B — Plano de Testes (TDD)

### 7.1 Testes de Caracterização
- N/A — funcionalidade nova.

### 7.2 Testes Unitários
- **UT-01** — Dado virada 06:00 e fuso America/Sao_Paulo, quando `GetBusinessNight` recebe 2026-10-03T02:00-03:00, 2026-10-03T05:59-03:00 e 2026-10-03T06:00-03:00, então retorna 02/10, 02/10 e 03/10.
- **UT-02** — Dado `VenueSettings.Update` com cada campo fora da faixa, `comandaMin > comandaMax`, 6 avisos ou um aviso de 81 caracteres, então retorna erro de validação com o campo correspondente; e com valores válidos, aplica todos.

### 7.3 Testes de Integração
- **IT-01** — Gerente sem aceite recebe 409 no `PUT /settings`; aceita a versão `2026-10` (204, linha de aceite gravada); aceite com versão `2025-01` → 400 `TERMS_VERSION_OUTDATED`.
- **IT-02** — `PUT /settings` inválido retorna 400 com `errors` por campo; um aviso com `<script>` é salvo como texto; gerentes de bares diferentes só leem as próprias configurações.
- **IT-03** — Após `PUT` com `maxPendingPerComanda=4`, a mensagem `VenueSettingsChanged` é entregue pelo outbox e `IVenuesModule.GetRulesAsync` retorna 4 em até 2 s; com cache quente, o p95 da leitura fica abaixo de 1 ms.
- **IT-04** — A CLI `set-jukebox --venue rock --enabled true` altera `JukeboxEnabled`, visto por `FindBySlugAsync`.

### 7.4 Testes de Contrato
- **CT-01** — A suíte de contrato de `IVenuesModule` (tipos e semântica de `GetBusinessNight`, `GetRulesAsync` e `FindBySlugAsync`) e o schema do evento `VenueSettingsChanged` são verificados contra a implementação real; consumida pelas SPECs 0013, 0015, 0018 e 0021.

### 7.5 Testes E2E
- **E2E-01** — Gerente do bar novo faz login, é levado a "Termos de uso", aceita, cai em "Ajustes" com o aviso "Defina a faixa de comandas do bar", preenche 1 a 300, adiciona o aviso "Chopp em dobro até 21h" e salva; vê "Ajustes salvos" e, ao recarregar, os valores persistem; as duas telas sem violações axe.

### 7.6 Outros
- N/A.

**Dublês e dados de teste:** bar e gerente criados por builders/CLI por teste; `FakeTimeProvider` para a noite.

**Ambiente de execução:** Testcontainers (IT); AppHost + Playwright (E2E) local, e smoke em staging.

## 8. Plano de Rollout
- **Estratégia:** deploy direto; a flag `JukeboxEnabled` começa desligada em produção.
- **Dados/schema:** migration aditiva `venues.Settings` (tabelas `venue_settings`, `terms_acceptances`; coluna `jukebox_enabled` com padrão falso).
- **Compatibilidade:** N/A — primeiro uso.
- **Observabilidade:** log de auditoria "configurações alteradas" com os campos alterados (sem dados pessoais).
- **Rollback:** redeploy anterior; colunas novas com padrão não afetam a versão anterior.
- **Etapas de migração/coexistência:** N/A.

## 9. Questões em Aberto
- [x] Padrões das regras — limite 3, vídeo até 10 min, auto-remoção em -5, virada 06:00, 5 buscas no YouTube por comanda por noite, janela anti-repetição de 60 min; todos configuráveis (thomas, 2026-09-30)
- [x] Faixa de comandas — configurável pelo bar; sem padrão, o gerente define no primeiro acesso (thomas, 2026-09-30)

## 10. Aprovação (H1)
Registrada no frontmatter (`approved_by`, `approved_at`) somente depois que o humano responder "Aprovado". O arquiteto nunca aprova a própria spec.

## 11. Checklist de Implementação

**Fase 1: Regras e componentes (unitário)**
- [ ] Red: escrever UT-01, UT-02 com a tag `SPEC-0011:<ID>` e confirmar que falham pelo motivo certo
- [ ] Green: implementar o mínimo para passar, seguindo os ADRs de fundação e o padrão de slices
- [ ] Refactor mantendo tudo verde
- [ ] Validar: build + lint + suíte de UT (G2)

**Fase 2: Integração e contrato**
- [ ] Red: escrever IT-01, IT-02, IT-03, IT-04, CT-01 com a tag `SPEC-0011:<ID>` (Postgres real, esquemas de auth reais, `DrainOutboxAsync`, WireMock — ADR-0012) e confirmar que falham pelo motivo certo
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
| G0 Spec | PASS | `spec_graph.py validate SPEC-0011` → G0 [SPEC-0011]: PASS — 0 erro(s), 0 aviso(s); checklist de julgamento do G0 revisado pelo Architect | 2026-09-30 |
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
