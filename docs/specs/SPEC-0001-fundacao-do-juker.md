---
id: SPEC-0001
title: Fundação do Juker
tier: epic
type: foundation
status: approved
created: 2026-09-30
depends_on: []
adrs: [ADR-0001, ADR-0002, ADR-0003, ADR-0004, ADR-0005, ADR-0006, ADR-0007, ADR-0008, ADR-0009, ADR-0010, ADR-0011, ADR-0012, ADR-0013, ADR-0014]
external: []
approved_by: thomas
approved_at: 2026-09-30
---

# SPEC-0001 — Fundação do Juker (Épico)

## 1. Visão
Colocar de pé o caminho completo do código até a produção antes de qualquer funcionalidade de jukebox: solução .NET 10 com Aspire, módulos vazios com fronteiras testadas, harness com todos os níveis de teste, CI no GitHub Actions, staging e produção no Hetzner, e um **walking skeleton** (a página do bar abre no celular e a TV toca um vídeo) entregue pelo pipeline, com observabilidade e segurança básicas. A partir daí, cada funcionalidade do MVP (SPEC-0010) é um incremento pequeno e seguro sobre um caminho que já funciona.

Enquadramento do produto (respostas do fundador em 2026-09-29/30):
- **Domínio:** jukebox colaborativa + karaokê para bares de rock. Jornadas centrais: o cliente entra pela comanda + PIN e pede música; a galera vota; a TV toca sozinha; o gerente modera.
- **Usuários:** piloto em 1 bar; meta de dezenas de bares em 12 meses. Clientes web mobile (sem app de loja), TV (navegador em kiosk) e painel (tablet/celular).
- **Dados:** número da comanda, identificador de dispositivo anônimo, pedidos e votos da noite. Nenhum nome, e-mail ou telefone de cliente. Dados de staff: e-mail e senha (hash).
- **Compliance:** LGPD com dados mínimos. O ECAD e a conta do YouTube Premium são responsabilidade do bar (termos de uso). O fundador aceitou o risco dos termos do YouTube (ADR-0007).
- **Integrações:** YouTube Data API v3 e IFrame Player.
- **Metas:** disponível no horário de funcionamento dos bares; voto e pedido refletidos na TV e nos celulares em menos de 1 s (p95).
- **Time:** 1 desenvolvedor (.NET/C#), que também opera a produção.
- **Restrições:** custo mínimo; GitHub + Actions; Hetzner; domínio a definir antes do primeiro deploy.
- **Tracking:** somente o repositório (`tracker.provider: none`).

## 2. Escopo
**Objetivos (dentro do escopo):**
- Repositório, solução, build, lint, analisadores (Meziantou + APIs proibidas no build), convenções e o controle SDD instalado no projeto (SPEC-0002).
- Harness de testes de integração (banco por classe a partir de modelo migrado, auth real, tempo controlado) e a suíte de arquitetura ARQ-01..ARQ-08 (SPEC-0003).
- Harness de E2E: AppHost no ambiente `E2E`, Playwright em Chromium e WebKit, relógio e API de teste só no `E2E`, page objects e artefatos de falha (SPEC-0025).
- Building blocks: handlers, `Result`/`ProblemDetails`, validação, outbox/inbox, contexto de tenant, `TimeProvider` (SPEC-0004).
- Pipeline de CI com todos os gates, cobertura das linhas alteradas ≥ 80%, mutation testing noturno nas regras críticas e o job SDD obrigatório (SPEC-0005).
- Staging e produção como código no Hetzner, com segredos, migrations pelo pipeline, backup e rollback (SPEC-0006).
- Walking skeleton: página pública do bar e TV tocando um vídeo, com E2E, orçamento de payload e verificação manual do Premium (SPEC-0007).
- Observabilidade básica: health checks, métricas, logs sem dados pessoais, alertas e uptime (SPEC-0008).
- Segurança básica: login do gerente, sessão do cliente e da TV como esqueleto de autorização, isolamento entre bares, HTTPS, cabeçalhos e rate limit (SPEC-0009).

**Não-objetivos (fora do escopo):**
- Qualquer regra de jukebox (fila, votos, busca, karaokê): épico SPEC-0010.
- Autocadastro de bares, cobrança e planos.
- Alta disponibilidade, múltiplas réplicas e backplane (ADR-0005).
- App nativo de loja.

## 3. Arquitetura Alvo
**Contexto:** projeto novo. A stack e a arquitetura estão definidas nos ADRs de fundação ADR-0001..ADR-0011.

```
Juker.slnx
├── global.json · Directory.Build.props · Directory.Packages.props · .editorconfig
├── src/
│   ├── Aspire/Juker.AppHost · Aspire/Juker.ServiceDefaults
│   ├── Juker.Host                     # compõe módulos, SignalR, Blazor (1 container)
│   ├── BuildingBlocks/Juker.SharedKernel · BuildingBlocks/Juker.Infrastructure
│   ├── Modules/{Venues,Sessions,Catalog,Queue,Playback}/Juker.Modules.X(.Contracts)
│   └── Web/Juker.Web · Web/Juker.Web.Client   # /b (cliente), /tv, /admin
├── tests/ (UnitTests por módulo · IntegrationTests · E2ETests · ArchitectureTests · Web.UnitTests)
├── deploy/ (compose gerado pelo Aspire, Caddyfile, scripts de backup/restore)
└── .github/workflows/ (ci.yml, deploy.yml)
```

Fluxo de entrega: PR → CI (build, lint, UT, IT, arquitetura, E2E, scan, SDD) → merge em `main` → imagem `ghcr.io/<owner>/juker:<sha>` → staging automático + smoke → produção com aprovação humana (G6).

**Decisões (ADRs):** ADR-0001 monólito modular + slices; ADR-0002 .NET 10 + minimal APIs + handlers próprios; ADR-0003 Blazor com render mode por área + Tailwind v4 + tokens do Stitch; ADR-0004 PostgreSQL 18 + EF Core 10; ADR-0005 SignalR em instância única; ADR-0006 contratos síncronos + outbox; ADR-0007 fonte de mídia plugável (YouTube); ADR-0008 identidade e tenancy; ADR-0009 GitHub Actions + Hetzner + compose do Aspire; ADR-0010 OpenTelemetry → backend gerenciado; ADR-0011 ferramentas de teste; ADR-0012 estratégia de testes de integração; ADR-0013 estratégia de E2E e testes de interface; ADR-0014 qualidade de código.

**Regras de arquitetura a garantir (G3):**
- ARQ-01 módulo só referencia `.Contracts` de outro módulo — `ArchitectureTests.ModuleBoundaries`
- ARQ-02 `.Contracts` não referencia internos — `ArchitectureTests.ContractsAreLeaves`
- ARQ-03 um `DbContext` por módulo, não compartilhado — `ArchitectureTests.DbContextOwnership`
- ARQ-04 `Domain/` sem EF Core/ASP.NET/Infrastructure — `ArchitectureTests.DomainPurity`
- ARQ-05 `Web` só referencia `.Contracts` — `ArchitectureTests.WebBoundaries`
- ARQ-06 sem MediatR, MassTransit e FluentAssertions ≥ 8 — `ArchitectureTests.ForbiddenDependencies`
- ARQ-07 tipos YouTube só em `Catalog.Infrastructure` e `Web.Client.Tv` — `ArchitectureTests.MediaProviderIsolation`
- ARQ-08 testes sem EF Core InMemory/Sqlite nem mocks de `HttpMessageHandler` — `ArchitectureTests.TestingPolicy`

## 4. Decomposição
| Spec | Título | Tier | Tipo | Tamanho | Depende de | Consome contrato de |
|---|---|---|---|---|---|---|
| SPEC-0002 | Repositório, solução e tooling | full | foundation | M | — | — |
| SPEC-0003 | Harness de testes de integração e suíte de arquitetura | full | foundation | M | SPEC-0002 | — |
| SPEC-0025 | Harness de E2E e ambiente de teste controlável | full | foundation | M | SPEC-0003 | — |
| SPEC-0004 | Building blocks dos módulos | full | foundation | M | SPEC-0002 | SPEC-0003@1 |
| SPEC-0005 | Pipeline de CI | full | foundation | M | SPEC-0002 | SPEC-0003@1, SPEC-0025@1 |
| SPEC-0006 | Ambientes, IaC, segredos, migrations e backup | full | foundation | M | — | SPEC-0005@1 |
| SPEC-0007 | Walking skeleton: página do bar e TV tocando | full | foundation | M | SPEC-0003, SPEC-0004, SPEC-0005, SPEC-0025 | — |
| SPEC-0008 | Observabilidade básica | full | foundation | M | SPEC-0007 | — |
| SPEC-0009 | Segurança básica e autorização | full | foundation | M | SPEC-0007 | — |
| SPEC-0026 | Pendências do review da SPEC-0002 | lite | fix | S | SPEC-0002 | — |

## 5. Estratégia de Entrega
- **Ambientes:** **CD adiado por decisão do fundador (2026-09-30)**: nesta fase tudo é validado localmente (AppHost) e no CI do GitHub (SPEC-0005); a entrega de cada onda é o merge na `main` com o CI verde. A SPEC-0006 (staging/produção) fica estacionada com o impedimento IMP-01 e, quando destravada, assume o deploy e as verificações em ambiente real. Este épico só fecha depois dela.
- **Entrega por onda:** cada onda termina com merge na `main`, CI verde e smoke contra o host local; produção só depois da SPEC-0006.
- **Feature flags:** N/A na fundação. As flags nascem com as funcionalidades do SPEC-0010.
- **Rollback:** redeploy da imagem da versão anterior pelo `deploy.yml` (SPEC-0006), com migrations sempre compatíveis com a versão anterior.
- **Métricas de sucesso pós-release:** pipeline verde de ponta a ponta; tempo de deploy abaixo de 10 min; uptime do skeleton acima de 99% por 7 dias em staging.

## 6. Riscos & Mitigações
- Payload do WASM do cliente acima do orçamento — medido como teste (SPEC-0007:IT-03). Plano B: cliente em SSR + JS mínimo (ADR-0003).
- YouTube Premium não remover anúncios no player embutido da TV — verificação manual obrigatória no hardware real (SPEC-0007, seção 7.6) antes do SPEC-0010. Se falhar, o fundador decide antes de seguir.
- Domínio indefinido bloqueia TLS/produção — pré-requisito externo explícito de SPEC-0006.
- Operar um VPS sozinho (patches, backup) — checklist como código e restauração de backup testada (SPEC-0006:IT-03).

## 7. Critérios de Aceite do Épico
- [ ] A solução compila, passa no lint e sobe localmente com um comando pelo Aspire — SPEC-0002:IT-01
- [ ] As regras ARQ-01..ARQ-08 falham ao serem violadas — SPEC-0003:IT-02
- [ ] A API de teste e o relógio controlável não existem em staging nem em produção — SPEC-0025:IT-02
- [ ] Um evento de integração gravado na transação é entregue exatamente uma vez por consumidor — SPEC-0004:IT-02
- [ ] Um PR com teste vermelho ou sem spec aprovada é bloqueado pelo CI — SPEC-0005:IT-01
- [ ] Código com API proibida pelos ADRs (ex.: `DateTime.Now`) não compila — SPEC-0002:IT-03
- [ ] Código novo sem teste é bloqueado pela cobertura do diff — SPEC-0005:IT-06
- [ ] O deploy aplica migrations e troca a versão sem perda de dados, e o rollback volta à anterior — SPEC-0006:IT-02
- [ ] No celular, a página do bar abre com o nome do bar, e a TV toca um vídeo e passa para o próximo — SPEC-0007:E2E-01
- [ ] Métricas de disponibilidade e de erros 5xx chegam ao backend de observabilidade via OTLP, sem afetar as requisições se ele cair — SPEC-0008:IT-02
- [ ] Um gerente do bar A não acessa dados do bar B — SPEC-0009:IT-05

## 8. Questões em Aberto
- [x] Onde o trabalho é acompanhado? — Somente no repositório (thomas, 2026-09-30)
- [x] Onde fica o código e o CI? — GitHub + GitHub Actions (thomas, 2026-09-30)
- [x] Onde roda o piloto? — VPS Hetzner com Docker Compose gerado pelo Aspire (thomas, 2026-09-30)
- [x] Domínio do produto? — Ainda não definido; vira pré-requisito externo da SPEC-0006, a ser resolvido antes do primeiro deploy (thomas, 2026-09-30)
- [x] Stack e arquitetura — .NET 10, Aspire, Blazor, monólito modular com vertical slices, conforme as recomendações aceitas (thomas, 2026-09-30)
- [x] Qualidade de código — ADR-0014: Meziantou + BannedApiAnalyzers, cobertura do diff ≥ 80% sem meta global, Stryker só nas regras críticas (não bloqueante), teste de tokens do design, sem SonarCloud (thomas, 2026-09-30)
- [x] Estratégia de testes de integração e de E2E/interface — ADR-0012 e ADR-0013 (Aspire + Playwright com Chromium e WebKit, sem Testcontainers no E2E, sem regressão visual no MVP) (thomas, 2026-09-30)

## 9. Aprovação (H1)
Uma aprovação humana cobre o épico e as specs filhas apresentadas junto com ele. Registrada no frontmatter (`approved_by`, `approved_at`) do épico e de cada filha.

## 10. Registro de Impedimentos
| ID | Aberto em | Fase/Gate | Tipo | Descrição | Tentativas | Responsável | Resolução | Fechado em |
|---|---|---|---|---|---|---|---|---|

## 11. Relatório de Entrega

## 12. Emendas
| Versão | Data | Mudança | Motivo | Specs impactadas | Aprovado por |
|---|---|---|---|---|---|
| escopo | 2026-09-30 | CD adiado: entrega por onda passa a ser merge na main com CI verde; SPEC-0006 estacionada; SPEC-0007 não depende mais dela | Decisão do fundador: infra fora por enquanto, CI antes do CD | SPEC-0006, SPEC-0007, SPEC-0008, SPEC-0009 | thomas — autorização prévia no chat (2026-09-30: "deixar a infra de fora… optar pela recomendação"), a confirmar no overview |
