---
id: SPEC-0009
title: Segurança básica e autorização
tier: full
type: foundation
user_facing: true
status: approved
created: 2026-09-30
parent: SPEC-0001
depends_on: [SPEC-0007]
consumes_contract: []
contract_version: 1
touches: [src/Modules/Venues/Juker.Modules.Venues/Features/Staff/**, src/Modules/Venues/Juker.Modules.Venues/Infrastructure/Identity/**, src/Modules/Venues/Juker.Modules.Venues.Contracts/**, src/BuildingBlocks/Juker.Infrastructure/Security/**, src/Juker.Host/Security/**, src/Web/Juker.Web/Components/Admin/Login/**, tests/Juker.IntegrationTests/Security/**, tests/Juker.Modules.Venues.UnitTests/Staff/**, tests/Juker.E2ETests/Admin/Login/**]
adrs: [ADR-0008]
external: []
size: M
approved_by: thomas
approved_at: 2026-09-30
---

# SPEC-0009 — Segurança básica e autorização

## 1. Visão Geral
Estabelece o esqueleto de segurança que todas as funcionalidades usam: login do gerente (ASP.NET Core Identity, cookie), os esquemas de autenticação do **cliente** (sessão de comanda) e da **TV** (token de dispositivo) com as políticas de autorização, a resolução do `ITenantContext` a partir do principal, HTTPS/HSTS, cabeçalhos de segurança (CSP por área), antiforgery e rate limiting. A emissão da sessão do cliente vem na SPEC-0013 e o pareamento da TV na SPEC-0021; aqui ficam a validação e as políticas, com emissores de teste.

## 2. Motivação & Escopo
**Motivação:** o bloqueio de comanda, o isolamento entre bares e o botão de pânico do gerente só valem se a identidade de cada ator for verificada do mesmo jeito em todo endpoint.

**Objetivos (dentro do escopo):**
- Identity no schema `venues` (tabelas do Identity com prefixo), entidade `StaffMember` ligada a um `VenueId`, papel `Manager`.
- CLI `juker admin create-manager --venue <slug> --email <e-mail>` gerando uma senha temporária exibida uma vez, com troca obrigatória no primeiro login.
- Página `/admin/login` (SSR com formulário e antiforgery), `/admin/logout`, troca de senha no primeiro acesso; política de senha (mínimo 10 caracteres); lockout após 5 falhas por 15 min.
- Esquemas: `Staff` (cookie do Identity), `Guest` (cookie de sessão protegido por Data Protection, com `venue_id`, `comanda`, `device_id` e `night`) e `Tv` (token de dispositivo opaco com hash no banco).
- Políticas: `ManagerOfVenue`, `GuestOfVenue`, `TvOfVenue`; `ITenantContext` resolvido do principal autenticado.
- `GuestSessionIssuer`/`TvTokenIssuer` como contrato, com emissores de teste; a validação consulta `ISessionStatus` (contrato implementado depois pela SPEC-0013) para sessões revogadas.
- HTTPS redirection + HSTS; cabeçalhos: `X-Content-Type-Options`, `Referrer-Policy: strict-origin-when-cross-origin`, `Permissions-Policy`, CSP por área (`/tv` libera `frame-src https://www.youtube-nocookie.com https://www.youtube.com` e o script da IFrame API; `/b` e `/admin` sem frames); `frame-ancestors 'none'`.
- Rate limiting: login 10/min por IP; política `per-guest` (60/min por sessão) disponível para os endpoints do cliente.
- Política de dependências documentada (sem pacotes com vulnerabilidade High/Critical; atualização semanal pelo Dependabot).

**Não-objetivos (fora do escopo):**
- Entrada do cliente com PIN e comanda (SPEC-0013).
- Pareamento da TV (SPEC-0021).
- Recuperação de senha por e-mail, SSO e MFA (fora do MVP; o fundador redefine pela CLI).

## 3. Dependências
- **Implementações necessárias:** SPEC-0007 — módulo Venues, entidade `Venue` e host com as áreas `/b`, `/tv` e `/admin`.
- **Contratos consumidos:** N/A.
- **Pré-requisitos externos:** N/A.

## 4. Decisão Arquitetural
**Contexto:** projeto novo; ADR-0008.

**Decisão:** o Identity e o `StaffMember` ficam no módulo Venues (dono do bar e da equipe); os esquemas e as políticas ficam no `Juker.Infrastructure/Security`, usados por todos os módulos.

**Justificativa:** a equipe pertence ao bar, e as políticas precisam ser idênticas em todos os módulos.

**Desvio do padrão existente:** Nenhum.

**Alternativas descartadas:** módulo Identity separado (mais um módulo para um único ator no MVP); JWT para o cliente (cookie HttpOnly protege melhor contra XSS no navegador).

**ADRs:** ADR-0008.

## 5. Requisitos Não-Funcionais
- **Desempenho e escala:** validação de sessão do cliente abaixo de 2 ms p95 (cache em memória do status por 30 s) — medida no IT-02.
- **Segurança:** OWASP A01 (controle de acesso: IT-01..IT-05), A07 (autenticação: lockout e política de senha, UT-01/IT-06), A05 (cabeçalhos, IT-07). Cookies `HttpOnly`, `Secure`, `SameSite=Lax`.
- **Privacidade e dados pessoais:** dados de staff: e-mail e hash da senha (LGPD — execução de contrato com o bar); nenhum dado pessoal do cliente.
- **Disponibilidade e resiliência:** N/A — sem dependências externas.
- **Acessibilidade (UI):** `/admin/login` sem violações axe A/AA — E2E-01.
- **Custo:** N/A.

## 6. Artefato A — Contrato
**Interface:** esquemas, políticas, endpoints de login e contratos de emissão.

```text
Políticas (uso: .RequireAuthorization(Policies.X)):
  ManagerOfVenue → esquema Staff, papel Manager, claim venue_id
  GuestOfVenue   → esquema Guest, sessão válida, não revogada, night == noite atual do bar
  TvOfVenue      → esquema Tv, token ativo (não revogado)
ITenantContext.VenueId ← claim venue_id do principal autenticado (qualquer esquema)

Contratos (Juker.Infrastructure.Security):
  interface IGuestSessionIssuer { Task IssueAsync(HttpContext ctx, GuestSession s); }
    record GuestSession(VenueId Venue, int Comanda, Guid DeviceId, DateOnly Night, Guid SessionId)
  interface ISessionStatus { Task<bool> IsActiveAsync(Guid sessionId, CancellationToken ct); }
  interface ITvTokenIssuer { Task<string> IssueAsync(VenueId venue, Guid deviceId); }

HTTP:
  GET  /admin/login                     → formulário
  POST /admin/login  (form + antiforgery) → 302 /admin | 200 com "E-mail ou senha inválidos"
                                          | 200 com "Conta bloqueada por 15 minutos" (lockout)
  POST /admin/logout                    → 302 /admin/login
  Rotas /api/v1/admin/**  sem cookie Staff → 401 · de outro bar → 404 (não revela existência)
  Rotas /api/v1/b/{slug}/me/** sem sessão Guest → 401 · sessão de outro bar → 403
  Rotas /api/v1/tv/**  sem token Tv → 401

CLI:
  juker admin create-manager --venue rock --email dono@bar.com → exit 0 + senha temporária
```

**Arquivos/módulos afetados:** ver `touches` no frontmatter.

### 6.1 Mapa de Comportamentos
| Cenário | Condição / Entrada | Resultado esperado | Testes |
|---|---|---|---|
| Senha fora da política | senha com 8 caracteres | rejeitada com `PASSWORD_TOO_WEAK` | UT-01 |
| Anônimo em rota protegida | sem cookie/token em `/api/v1/admin/probe`, `/api/v1/b/rock/me/probe`, `/api/v1/tv/probe` | 401 nas três | IT-01 |
| Cliente autorizado | sessão Guest válida do bar `rock` | 200 em `/api/v1/b/rock/me/probe`; 403 em `/api/v1/b/outro/me/probe` | IT-02 |
| Sessão revogada | `ISessionStatus` retorna inativa | 401 em até 30 s | IT-02 |
| Sessão de outra noite | `night` diferente da noite atual do bar | 401 | UT-02, IT-02 |
| TV autorizada | token Tv ativo | 200 em `/api/v1/tv/probe`; token revogado → 401 | IT-03 |
| Gerente autorizado | login válido | 200 em `/api/v1/admin/probe` com o `VenueId` dele no `ITenantContext` | IT-04, E2E-01 |
| Isolamento entre bares | gerente do bar A pede recurso do bar B | 404 | IT-05 |
| Lockout | 5 senhas erradas seguidas | 6ª tentativa (mesmo com a senha certa) → "Conta bloqueada por 15 minutos" | IT-06 |
| Login com erro | senha errada | mensagem "E-mail ou senha inválidos", sem revelar se o e-mail existe | E2E-01 |
| Cabeçalhos de segurança | GET em `/b/rock`, `/tv/rock` e `/admin/login` | HSTS, nosniff, Referrer-Policy e CSP da área (YouTube só em `/tv`) | IT-07 |
| Rate limit de login | 11 POSTs em 1 min do mesmo IP | 11º → 429 | IT-06 |
| Troca de senha obrigatória | primeiro login com senha temporária | redireciona para "Defina sua nova senha" | E2E-01 |

## 7. Artefato B — Plano de Testes (TDD)

### 7.1 Testes de Caracterização
- N/A — projeto novo.

### 7.2 Testes Unitários
- **UT-01** — Dado as senhas "curta123" e "senha-forte-do-bar", quando validadas pela política, então a primeira falha com `PASSWORD_TOO_WEAK` e a segunda passa.
- **UT-02** — Dado uma `GuestSession` com `night = 2026-10-02` e o relógio em 2026-10-03 07:00 com virada às 06:00, quando validada, então é considerada expirada.

### 7.3 Testes de Integração
- **IT-01** — Sem credenciais, as três rotas de teste (`/api/v1/admin/probe`, `/api/v1/b/rock/me/probe`, `/api/v1/tv/probe`, registradas só em `Testing`) retornam 401.
- **IT-02** — Com uma sessão Guest emitida pelo emissor de teste para o bar `rock`: 200 no próprio bar e 403 no bar `outro`; com o `ISessionStatus` fake marcando a sessão como inativa, 401 após a expiração do cache (tempo via `FakeTimeProvider`); com `night` antiga, 401. Mede o p95 da validação.
- **IT-03** — Um token Tv ativo acessa `/api/v1/tv/probe` (200); após a revogação no banco, 401.
- **IT-04** — Login real pelo formulário com antiforgery: o cookie Staff é emitido e `/api/v1/admin/probe` retorna o `VenueId` do gerente.
- **IT-05** — Gerente do bar A pede `/api/v1/admin/venues/{idDoBarB}/probe` → 404, e o banco não registra acesso aos dados do B.
- **IT-06** — 5 senhas erradas geram lockout (a 6ª, mesmo correta, falha com a mensagem de bloqueio); 11 POSTs de login em 1 min do mesmo IP → 429 no 11º.
- **IT-07** — Cabeçalhos de `/b/rock`, `/tv/rock` e `/admin/login`: HSTS (fora de Development), `nosniff`, `Referrer-Policy` e CSP com `frame-src` do YouTube apenas em `/tv/rock`.

### 7.4 Testes de Contrato
- **CT-01** — Suíte de contrato de `IGuestSessionIssuer` + esquema Guest: um cookie emitido pelo emissor real (a partir da SPEC-0013) ou pelo de teste é aceito pela política `GuestOfVenue` com as mesmas claims; consumida pela SPEC-0013 (emissor real da sessão) e pela SPEC-0021 (emissor real do `ITvTokenIssuer`).

### 7.5 Testes E2E
- **E2E-01** — Gerente criado pela CLI abre `/admin/login`, erra a senha e vê "E-mail ou senha inválidos"; entra com a senha temporária, é levado a "Defina sua nova senha", define a nova e chega a `/admin` com o nome "Bar do Rock"; a página de login não tem violações axe.

### 7.6 Outros
- Segurança: `dotnet list package --vulnerable` sem High/Critical (job `security` da SPEC-0005); cabeçalhos conferidos com `curl -I` no host local (IT-07 já os cobre); a verificação com securityheaders.com em staging (nota mínima A) foi transferida para a SPEC-0006.

**Dublês e dados de teste:** `TestGuestSessionIssuer`, `TestTvTokenIssuer`, `FakeSessionStatus`; rotas `*/probe` só no ambiente `Testing`.

**Ambiente de execução:** Testcontainers (Postgres 18) para IT; AppHost + Playwright para E2E; host local para a conferência manual dos cabeçalhos.

## 8. Plano de Rollout
- **Estratégia:** deploy direto; o primeiro gerente do piloto é criado pela CLI no servidor.
- **Dados/schema:** migration aditiva `venues.Identity` (tabelas do Identity + `staff_members`).
- **Compatibilidade:** N/A — primeira versão das políticas.
- **Observabilidade:** logs de falha de login (sem e-mail completo, só o domínio) e contador de lockouts.
- **Rollback:** redeploy da versão anterior; tabelas novas não afetam a anterior.
- **Etapas de migração/coexistência:** N/A.

## 9. Questões em Aberto
Nenhuma.

## 10. Aprovação (H1)
Registrada no frontmatter (`approved_by`, `approved_at`) somente depois que o humano responder "Aprovado". O arquiteto nunca aprova a própria spec.

## 11. Checklist de Implementação

**Fase 1: Regras e componentes (unitário)**
- [ ] Red: escrever UT-01, UT-02 com a tag `SPEC-0009:<ID>` e confirmar que falham pelo motivo certo
- [ ] Green: implementar o mínimo para passar, seguindo os ADRs de fundação e o padrão de slices
- [ ] Refactor mantendo tudo verde
- [ ] Validar: build + lint + suíte de UT (G2)

**Fase 2: Integração e contrato**
- [ ] Red: escrever IT-01, IT-02, IT-03, IT-04, IT-05, IT-06, IT-07, CT-01 com a tag `SPEC-0009:<ID>` (Postgres real, esquemas de auth reais, `DrainOutboxAsync`, WireMock — ADR-0012) e confirmar que falham pelo motivo certo
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
| G0 Spec | PASS | `spec_graph.py validate SPEC-0009` → G0 [SPEC-0009]: PASS — 0 erro(s), 0 aviso(s); checklist de julgamento do G0 revisado pelo Architect (revisão ADR-0012/0013) | 2026-09-30 |
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
| escopo | 2026-09-30 | Verificação de cabeçalhos com securityheaders.com transferida para a SPEC-0006; conferência local com curl | CD e ambientes adiados por decisão do fundador | SPEC-0006 | thomas — autorização prévia no chat (2026-09-30: "deixar a infra de fora… optar pela recomendação"), confirmada por thomas no chat em 2026-10-13 |
