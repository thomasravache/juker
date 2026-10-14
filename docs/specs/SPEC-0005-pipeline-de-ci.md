---
id: SPEC-0005
title: Pipeline de CI
tier: full
type: foundation
user_facing: false
status: in-progress
created: 2026-09-30
parent: SPEC-0001
depends_on: [SPEC-0002, SPEC-0027, SPEC-0025]
consumes_contract: [SPEC-0003@2, SPEC-0025@3]
contract_version: 4
touches: [.github/workflows/ci.yml, .github/workflows/mutation.yml, .github/dependabot.yml, .gitleaks.toml, .config/dotnet-tools.json, tools/sdd/**, tools/ci/**, tests/Juker.CiTests/**, tests/**/stryker-config.json, docs/ci.md, Juker.slnx, tests/Juker.ArchitectureTests/Juker.ArchitectureTests.csproj, Directory.Packages.props]
adrs: [ADR-0009, ADR-0011, ADR-0013, ADR-0014]
external: []
size: M
approved_by: thomas
approved_at: 2026-09-30
---

# SPEC-0005 — Pipeline de CI

## 1. Visão Geral
Workflow do GitHub Actions que roda em todo PR e em todo push em `main`: build, lint, testes unitários, integração (com Docker), arquitetura, E2E (Playwright), auditoria de dependências, varredura de segredos, o job SDD (`validate` + `pr-check`) e o build da imagem do container. Os jobs viram checks obrigatórios na proteção da `main`, que fica sempre verde e implantável.

## 2. Motivação & Escopo
**Motivação:** o modelo não lembra do processo; o CI garante que nada chega à `main` sem spec aprovada, testes vermelhos antes dos verdes e todos os gates.

**Objetivos (dentro do escopo):**
- `.github/workflows/ci.yml` com os jobs `build`, `lint`, `unit`, `integration`, `architecture`, `e2e`, `security`, `sdd` e `image`, com cache do NuGet e dos navegadores do Playwright.
- `e2e`: instala Chromium e WebKit (`playwright install --with-deps chromium webkit`), roda o `test_e2e` e, **quando falha**, publica `TestResults/playwright/**` (trace, vídeo e screenshot) como artefato `playwright-traces` (ADR-0013).
- Qualidade (ADR-0014): `unit` e `integration` coletam cobertura com coverlet; o job `coverage` junta os relatórios com ReportGenerator (Cobertura) e roda `diff-cover` exigindo **≥ 80% das linhas alteradas** do PR (bloqueante, sem meta global); `.github/workflows/mutation.yml` roda o Stryker.NET (ferramenta local em `.config/dotnet-tools.json`) toda noite e sob demanda, só nas classes críticas listadas nos `stryker-config.json`, com `break` em 80% (não bloqueia PR; resultado publicado como artefato).
- `sdd`: `python3 tools/sdd/spec_graph.py validate --exclude-drafts` e `pr-check` com `PR_TITLE`/`PR_BODY` (via `spec_graph.py vendor --ci github`).
- `security`: `dotnet list package --vulnerable --include-transitive` (falha em High/Critical) e `gitleaks detect`.
- `image`: build da imagem do `Juker.Host` (Dockerfile gerado pelo Aspire/SDK container) com tag `sha`; publicação no GHCR só em push na `main` (usada por SPEC-0006).
- Resultados de teste publicados como artefato (TRX) e resumo no job.
- `dependabot.yml` para NuGet e GitHub Actions (semanal, com as atualizações agrupadas num PR por ecossistema e no máximo 2 PRs abertos por ecossistema).
- Consumo dentro da cota gratuita (emenda v3): `concurrency` com `cancel-in-progress` por branch/PR, artefatos com `retention-days: 7` e `actions/cache` para NuGet e navegadores.
- `docs/ci.md` com a lista de checks obrigatórios e a instrução para o fundador ativar a proteção de branch (ação humana).
- Preenche `ci`, `lint`, `security_scan`, `coverage` e `mutation` no `sdd-config.yml`.

**Não-objetivos (fora do escopo):**
- Deploy em qualquer ambiente (SPEC-0006).
- Ativar a proteção de branch (é configuração da conta, feita pelo fundador).

## 3. Dependências
- **Implementações necessárias:** SPEC-0002 — solução compilável e `tools/sdd` vendorizado.
- **Contratos consumidos:** SPEC-0003@2 — comandos `test`, `test_integration` e `arch_test` e categorias de teste; SPEC-0025@2 — comando `test_e2e`, navegadores e o diretório dos artefatos de falha.
- **Pré-requisitos externos:** repositório no GitHub com Actions habilitado; permissão `packages: write` para o GHCR.

## 4. Decisão Arquitetural
**Contexto:** projeto novo; ADR-0009 (GitHub Actions, GHCR), ADR-0011 (ferramentas de teste) e ADR-0013 (E2E com Chromium e WebKit e artefatos de falha).

**Decisão:** um único workflow de CI com jobs paralelos após o `build`, reutilizando os artefatos de build, e o job `image` dependente de todos os jobs de qualidade.

**Justificativa:** o feedback fica rápido com os jobs em paralelo, e a imagem só existe se tudo passou.

**Desvio do padrão existente:** Nenhum.

**Alternativas descartadas:** workflows separados por nível (mais difícil de tornar obrigatórios); CI de terceiros (ADR-0009).

**ADRs:** ADR-0009, ADR-0011, ADR-0013, ADR-0014.

## 5. Requisitos Não-Funcionais
- **Desempenho e escala:** duração total do CI num PR típico abaixo de 12 min — medida nas execuções do IT-04.
- **Segurança:** permissões mínimas por job (`contents: read`; `packages: write` só no `image` em `main`); actions de terceiros fixadas por SHA; nenhum segredo exposto em PR de fork — verificado por `actionlint` (UT-01) e no G4.
- **Privacidade e dados pessoais:** N/A — sem dados de usuários.
- **Disponibilidade e resiliência:** jobs idempotentes; falha de rede no restore tenta de novo uma vez (retry do `setup-dotnet`) — sem retry de testes (teste instável é defeito).
- **Acessibilidade (UI):** N/A.
- **Custo:** dentro da cota gratuita do GitHub Free para repositório privado (2.000 min/mês de Actions e 500 MB de armazenamento de artefatos, compartilhado com Packages; imagens no GHCR hoje sem cobrança — docs.github.com, consultado em 2026-10-13). Estimativa: ~40 min-runner por execução completa do CI; mutation semanal; Dependabot agrupado. O uso real é medido no IT-04 e acompanhado no resumo mensal; qualquer coisa que exija plano pago é decisão do fundador (ADR-0019).

## 6. Artefato A — Contrato
**Interface:** nomes dos checks obrigatórios, gatilhos e artefatos do workflow `ci.yml`.

```text
on: pull_request (branches: main) | push (branches: main)
jobs (checks obrigatórios na proteção da main):
  build          → dotnet build Juker.slnx -c Release -warnaserror
  lint           → dotnet format Juker.slnx --verify-no-changes
  unit           → dotnet test --solution Juker.slnx -c Release --filter-trait "Category=Unit"
  integration    → dotnet test --solution Juker.slnx -c Release --filter-trait "Category=Integration"   (Docker do runner)
  architecture   → dotnet test --project tests/Juker.ArchitectureTests -c Release
  e2e            → playwright install --with-deps chromium webkit ; dotnet test --project tests/Juker.E2ETests -c Release
                   (AppHost + Playwright) ; em falha: upload de TestResults/playwright/** como "playwright-traces"
  security       → dotnet list package --vulnerable --include-transitive ; gitleaks detect
  coverage       → needs: unit, integration ; reportgenerator (Cobertura) ; diff-cover --compare-branch=origin/main --fail-under=80
  sdd            → spec_graph.py validate --exclude-drafts ; spec_graph.py pr-check (só em PR)
  image          → needs: todos acima; push em main publica ghcr.io/<owner>/juker:<sha>
artefatos: test-results-<job> (TRX), coverage-report, playwright-traces (só em falha do e2e), image digest (output "image_digest")
mutation.yml (não obrigatório): on schedule (semanal, domingo 03:17 UTC) | workflow_dispatch
  → dotnet stryker -f tests/<Módulo>.UnitTests/stryker-config.json (break 80) ; artefato mutation-report
```

**Arquivos/módulos afetados:** ver `touches` no frontmatter.

### 6.1 Mapa de Comportamentos
| Cenário | Condição / Entrada | Resultado esperado | Testes |
|---|---|---|---|
| Workflow válido | `ci.yml` | `actionlint` sem erros; actions fixadas por SHA | UT-01 |
| Teste vermelho | PR com um teste unitário falhando | job `unit` falha e o PR fica bloqueado | IT-01 |
| PR sem spec | título/corpo sem `SPEC-NNNN` aprovada | job `sdd` falha | IT-02 |
| Segredo commitado | PR com uma chave de API fake | job `security` falha (gitleaks) | IT-03 |
| PR saudável | todos os gates atendidos | todos os checks verdes; imagem construída sem publicar | IT-04 |
| Push na main | merge de um PR verde | imagem publicada no GHCR com tag `sha` e digest no output | IT-04, CT-01 |
| Cobertura do diff | PR com código novo sem teste (cobertura das linhas alteradas < 80%) | job `coverage` falha e o PR fica bloqueado | IT-06 |
| Mutation testing | disparo manual do `mutation.yml` | relatório do Stryker publicado; score abaixo de 80% deixa o workflow vermelho sem bloquear PRs | IT-07 |
| E2E falha | PR com um E2E quebrado | job `e2e` falha e o artefato `playwright-traces` com o trace do teste fica disponível na execução | IT-05 |

## 7. Artefato B — Plano de Testes (TDD)

### 7.1 Testes de Caracterização
- N/A — projeto novo.

### 7.2 Testes Unitários
- **UT-01** — Dado `.github/workflows/ci.yml`, quando `actionlint` roda (teste em `tests/Juker.CiTests` que invoca o binário), então não há erros e toda `uses:` de terceiros está fixada por SHA de 40 caracteres.

### 7.3 Testes de Integração
<!-- Executados como PRs de verificação descartáveis no repositório real; evidência = URL da execução. -->
- **IT-01** — Branch `ci-check/red-test` com um teste unitário falhando: o job `unit` falha e o PR não pode ser mesclado.
- **IT-02** — Branch `ci-check/no-spec` com PR sem referência a spec aprovada: o job `sdd` falha no `pr-check`.
- **IT-03** — Branch `ci-check/secret` com uma chave fake no formato do gitleaks: o job `security` falha.
- **IT-04** — PR saudável: todos os checks verdes em até 12 min (E2E em Chromium e WebKit); após o merge, a imagem aparece no GHCR com a tag do SHA.
- **IT-06** — Branch `ci-check/uncovered` com um método novo sem teste: o job `coverage` falha informando a porcentagem das linhas alteradas; com um teste cobrindo o método, passa.
- **IT-07** — Disparo manual do `mutation.yml` gera o artefato `mutation-report`; com um `stryker-config.json` de verificação apontando para uma classe de exemplo com teste fraco, o workflow termina vermelho por score abaixo de 80%.
- **IT-05** — Branch `ci-check/e2e-fail` com um E2E que falha: o job `e2e` fica vermelho e a execução tem o artefato `playwright-traces` contendo o `trace.zip` do teste.

### 7.4 Testes de Contrato
- **CT-01** — O workflow publica o output `image_digest` e a tag `ghcr.io/<owner>/juker:<sha>` que o `deploy.yml` (SPEC-0006) consome; um teste em `tests/Juker.CiTests` lê o YAML e verifica os nomes dos jobs, do output e da tag.

### 7.5 Testes E2E
- N/A — pipeline de CI; nenhuma jornada de usuário muda (o CI só executa os E2E das outras specs).

### 7.6 Outros
- N/A.

**Dublês e dados de teste:** branches `ci-check/*` descartáveis, apagadas após a evidência.

**Ambiente de execução:** GitHub Actions (runner `ubuntu-latest`).

## 8. Plano de Rollout
- **Estratégia:** deploy direto do workflow; a proteção de branch é ativada pelo fundador depois do IT-04 verde.
- **Dados/schema:** N/A.
- **Compatibilidade:** mudar nomes de jobs exige atualizar a proteção de branch (documentado em `docs/ci.md`).
- **Observabilidade:** resumo de testes por job; notificações de falha do GitHub para o fundador.
- **Rollback:** reverter o commit do workflow.
- **Etapas de migração/coexistência:** N/A.

## 9. Questões em Aberto
Nenhuma.

## 10. Aprovação (H1)
Registrada no frontmatter (`approved_by`, `approved_at`) somente depois que o humano responder "Aprovado". O arquiteto nunca aprova a própria spec.

## 11. Checklist de Implementação

**Fase 1: Regras e componentes (unitário)**
- [ ] Red: escrever UT-01 com a tag `SPEC-0005:<ID>` e confirmar que falham pelo motivo certo
- [ ] Green: implementar o mínimo para passar, seguindo os ADRs de fundação e o padrão de slices
- [ ] Refactor mantendo tudo verde
- [ ] Validar: build + lint + suíte de UT (G2)

**Fase 2: Integração e contrato**
- [ ] Red: escrever IT-01, IT-02, IT-03, IT-04, IT-05, IT-06, IT-07, CT-01 com a tag `SPEC-0005:<ID>` (Postgres real, esquemas de auth reais, `DrainOutboxAsync`, WireMock — ADR-0012) e confirmar que falham pelo motivo certo
- [ ] Green: implementar endpoints, persistência, migrations e integrações até passar
- [ ] Refactor mantendo tudo verde
- [ ] Validar: build + suíte completa + arquitetura + cobertura do diff ≥ 80% (G2/G3)

**Fase final: Integração, entrega e documentação**
- [ ] Review independente (G4)
- [ ] Integração + CI verde (G5) e aprovação (H2)
- [ ] Deploy via pipeline com smoke/E2E no ambiente (G6)
- [ ] Relatório de Entrega, docs raiz e CHANGELOG (G7)

## 12. Registro de Gates
| Gate | Status | Evidência | Data |
|---|---|---|---|
| G0 Spec | PASS | `spec_graph.py validate SPEC-0005` → G0 [SPEC-0005]: PASS — 0 erro(s), 0 aviso(s); checklist de julgamento do G0 revisado pelo Architect (revisão ADR-0014) | 2026-09-30 |
| G1 Red | PASS | verify G1: PASS; `dotnet test --solution Juker.slnx -c Release` exit 2 (red: Test run completed with non-success exit code: 2 (see: https://aka.ms/testingplatform/exit) — 531ea2c | 2026-10-13 |
| G2 Green | PASS | build exit 0 (Time Elapsed 00:00:05.20); test exit 0 (duration: 1m 52s 196ms); lint exit 0 () — 3e42d6b | 2026-10-14 |
| G3 Arquitetura | PASS | arch_test exit 0 (duration: 1s 254ms) — 3e42d6b | 2026-10-14 |
| G4 Review | FAIL | Reviewer independente: CHANGES_REQUESTED em 3e42d6b — major: job integration sem os navegadores do Playwright (IT-03/CT-01 da SPEC-0025 rodam nele); major de processo: código segue o contrato v4 ainda pendente; minors: packages: write no job image inteiro, pull_request sem edited, comentário do dependabot sobre global.json, cabeçalhos v3 e docs/ci.md linha 8, comandos do sdd-config vazios | 2026-10-14 |
| G5 Integração & CI | PENDING | | |
| H2 Integração aprovada | PENDING | | |
| G6 Deploy | PENDING | | |
| G7 Pronto & Docs | PENDING | | |

## 13. Registro de Impedimentos
| ID | Aberto em | Fase/Gate | Tipo | Descrição | Tentativas | Responsável | Resolução | Fechado em |
|---|---|---|---|---|---|---|---|---|
| IMP-01 | 2026-10-13 | G1 | spec | touches não inclui Juker.slnx nem tests/Juker.ArchitectureTests/Juker.ArchitectureTests.csproj, necessários para registrar o projeto novo tests/Juker.CiTests (convenção da SPEC-0003/0027); os testes do CT-01 leem YAML com YamlDotNet, que chega só transitivamente pelo Aspire.Hosting.Testing e não está em nenhum ADR nem fixado no Directory.Packages.props | Test-writer escreveu UT-01, CT-01 e o script IT-01..IT-07 dentro do touches; verify PASS; não editou os dois arquivos | Architect (emenda de escopo) + thomas (aprovação) | Emenda de escopo de 2026-10-13 (touches com Juker.slnx, csproj da suíte de arquitetura e Directory.Packages.props; YamlDotNet explícito e no ADR-0011) — aprovada por thomas | 2026-10-13 |
| IMP-02 | 2026-10-14 | G4 | decisão | GitHub Free não permite proteção de branch nem rulesets em repositório privado (API: 403 'Upgrade to GitHub Pro or make this repository public'); o contrato pede os jobs como checks obrigatórios na proteção da main, o IT-01 espera 'o PR não pode ser mesclado' e o rollout prevê o fundador ativando a proteção | Implementer e Architect consultaram repos/thomasravache/juker/branches/main/protection e /rulesets (403); docs/ci.md já traz o passo a passo para quando houver | thomas (GitHub Pro, repositório público, ou seguir só com a política auto-on-green + pr-check até a decisão de custos no fim) |  |  |

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
| 4 | 2026-10-14 | (a) Proteção de branch adiada para a decisão de custos do fim (ADR-0019): até lá, os checks rodam em todo PR e o merge só acontece com todos verdes pela política `auto-on-green` (ADR-0015), sem bloqueio pela plataforma; o IT-01 passa a exigir só o job `unit` vermelho e o PR sem merge; `docs/ci.md` mantém o passo a passo para ativar a proteção. (b) Decisão Arquitetural: cada job compila o que usa, em vez de reaproveitar os artefatos do `build` (a saída de build tem ~900 MB, acima dos 500 MB de artefatos do plano gratuito); `coverage` depende de `unit` e `integration` e `image` de todos. (c) O `mutation.yml` começa vermelho (score 46,94% no `Juker.Infrastructure`, abaixo do `break` 80) sem bloquear PRs; o reforço dos testes vira spec própria. ADR-0014 alinhado: Stryker semanal. (d) Desvios aceitos do review: `packages: write` vale para o job `image` inteiro (permissão de job não depende do evento; o login e a publicação só rodam em push na `main` e PR de fork recebe token só de leitura); `pull_request` sem o tipo `edited` (corrigir título/corpo do PR exige rodar o `sdd` de novo à mão, documentado em `docs/ci.md`, para não repetir o CI inteiro a cada edição e economizar cota) | IMP-02: GitHub Free não permite proteção de branch em repositório privado (403); Implementer: artefatos de build excedem a cota; Stryker real com 23 mortos, 11 sobreviventes e 15 sem cobertura | SPEC-0006 (consome @3 — só nomes de jobs/saída da imagem, sem mudança; repinar @4) | pendente — thomas |
| escopo | 2026-10-13 | `touches` inclui `Juker.slnx` e `tests/Juker.ArchitectureTests/Juker.ArchitectureTests.csproj` (registrar o projeto `tests/Juker.CiTests`) e `Directory.Packages.props` (fixar YamlDotNet, referenciado explicitamente pelo `Juker.CiTests` em vez de chegar só pelo Aspire.Hosting.Testing); ADR-0011 passa a listar o YamlDotNet para leitura de YAML nos testes de contrato do CI | IMP-01 (SPEC_DEFECT do Test-writer): projeto de teste novo precisa estar na solução e nas referências da suíte de arquitetura (UT-02/UT-03 da SPEC-0027); dependência indireta não declarada quebraria sem aviso numa atualização do Aspire (ADR-0017) | Nenhuma | thomas — no chat em 2026-10-13 ("aprovado") |
| 3 | 2026-10-13 | `mutation.yml` passa de diário a semanal (domingo 03:17 UTC) + manual; Dependabot agrupado por ecossistema com no máximo 2 PRs abertos; `concurrency` com `cancel-in-progress`, artefatos com `retention-days: 7` e cache de NuGet e navegadores; NFR de custo com os limites do GitHub Free | Revisão do Architect antes do despacho: com mutation diário e Dependabot sem agrupamento o consumo estimado se aproxima dos 2.000 min/mês do GitHub Free, e os artefatos do Playwright e da cobertura disputam os 500 MB; o fundador decide custos só no fim, então o CI precisa caber na cota gratuita | Nenhuma | thomas — no chat em 2026-10-13 ("aprovado") |
| 2 | 2026-09-30 | Comandos de teste migrados para o Microsoft.Testing.Platform (xUnit v3 no .NET 10): `dotnet test --solution/--project … --filter-trait "Category=…"` e `--list-tests`, no lugar da sintaxe VSTest (`dotnet test <sln> --filter Category=…`) | O xUnit v3 4.x roda no MTP e o SDK do .NET 10 recusa a sintaxe VSTest (constatado na SPEC-0002; `global.json` com `test.runner = Microsoft.Testing.Platform`) | Nenhuma (nenhuma spec consome o contrato da SPEC-0005 além da SPEC-0006, que usa só o output/tag da imagem, inalterados) | thomas, 2026-09-30 |
