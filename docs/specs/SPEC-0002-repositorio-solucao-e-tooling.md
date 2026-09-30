---
id: SPEC-0002
title: Repositório, solução e tooling
tier: full
type: foundation
user_facing: false
status: in-progress
created: 2026-09-30
parent: SPEC-0001
depends_on: []
consumes_contract: []
contract_version: 2
touches: [global.json, Directory.Build.props, Directory.Packages.props, .editorconfig, BannedSymbols.txt, tests/fixtures/**, .gitignore, .gitattributes, Juker.slnx, .config/**, src/Aspire/**, src/Juker.Host/**, src/BuildingBlocks/Juker.SharedKernel/**, src/Modules/**, src/Web/**, tests/Juker.IntegrationTests/Tooling/**, README.md, CLAUDE.md, AGENTS.md, CHANGELOG.md, tools/sdd/**, .githooks/**, .github/pull_request_template.md]
adrs: [ADR-0001, ADR-0002, ADR-0003, ADR-0009, ADR-0014]
external: []
size: M
approved_by: thomas
approved_at: 2026-09-30
---

# SPEC-0002 — Repositório, solução e tooling

## 1. Visão Geral
Cria a solução `Juker.slnx` com o AppHost do Aspire, o host único, os cinco módulos vazios (cada um com projeto interno e `.Contracts`) e os projetos web, com build determinístico, lint, gerenciamento central de pacotes e o controle SDD instalado no repositório. Ao final, `dotnet run --project src/Aspire/Juker.AppHost` sobe Postgres + host localmente, e o host responde saudável.

## 2. Motivação & Escopo
**Motivação:** todas as outras specs precisam de uma estrutura única e previsível para trabalhar em paralelo sem conflito, e de um comando único para rodar tudo localmente.

**Objetivos (dentro do escopo):**
- `global.json` fixando o SDK .NET 10 (feature band estável mais recente no momento da implementação, `rollForward: latestFeature`).
- `Directory.Build.props`: `Nullable=enable`, `ImplicitUsings=enable`, `TreatWarningsAsErrors=true`, `AnalysisLevel=latest-recommended`, `EnforceCodeStyleInBuild=true`, `InvariantGlobalization=false` (pt-BR), `LangVersion=latest`.
- Qualidade de código (ADR-0014): **Meziantou.Analyzer** e **Microsoft.CodeAnalysis.BannedApiAnalyzers** em todos os projetos via `Directory.Build.props`; `BannedSymbols.txt` na raiz com as APIs proibidas do ADR-0014 (relógio do sistema, `Thread.Sleep`/`Task.Delay` em testes, `new HttpClient()`, bloqueio síncrono de `Task`, `Console.*` fora de `src/Juker.Host/Cli/**`); `.editorconfig` com as severidades revisadas, cada regra desligada comentada com o motivo e `dotnet_diagnostic.MA0001.severity = error` (comparação de texto sem `StringComparison`, que é Info por padrão no Meziantou); `tests/fixtures/QualityViolations` (projeto fora da solução) usado pelo IT-03.
- `Directory.Packages.props` com gerenciamento central de versões (Aspire 13.x estável, EF Core 10, Npgsql, FluentValidation).
- `.editorconfig` com as convenções de estilo; `dotnet format --verify-no-changes` como lint.
- Projetos: `Juker.AppHost`, `Juker.ServiceDefaults`, `Juker.Host`, `Juker.SharedKernel` (vazio além de `AssemblyMarker`), `Juker.Modules.{Venues,Sessions,Catalog,Queue,Playback}` + `.Contracts`, `Juker.Web`, `Juker.Web.Client`.
- Cada módulo expõe `Add<Módulo>Module(IServiceCollection, IConfiguration)` e `Map<Módulo>Endpoints(IEndpointRouteBuilder)`, chamados pelo `Juker.Host` a partir do `ModuleCatalog` (forma no contrato).
- Endpoint de diagnóstico `/_diag/modules`, registrado só nos ambientes `Development` e `Testing`.
- AppHost: recurso `postgres` (imagem `postgres:18`) com um banco `juker`, host com `WithReference` e `WaitFor`.
- Health checks `/health/live` (processo) e `/health/ready` (inclui o Postgres) via ServiceDefaults.
- `spec_graph.py vendor --agents --pr-template github --hooks --changelog` (CI fica para SPEC-0005); `README.md` com o comando de execução local; `CLAUDE.md`/`AGENTS.md` com o bloco SDD e as convenções (Conventional Commits com `Refs: SPEC-NNNN`, Keep a Changelog, SemVer).

**Não-objetivos (fora do escopo):**
- Projetos de teste além do teste de tooling desta spec (SPEC-0003).
- Qualquer endpoint de negócio, migração de banco ou página (SPEC-0007 em diante).
- Workflows do GitHub Actions (SPEC-0005).

## 3. Dependências
- **Implementações necessárias:** N/A
- **Contratos consumidos:** N/A
- **Pré-requisitos externos:** .NET 10 SDK e Docker (ou Podman) na máquina de desenvolvimento; repositório GitHub criado pelo fundador para o push.

## 4. Decisão Arquitetural
**Contexto:** projeto novo; padrão definido nos ADRs de fundação ADR-0001, ADR-0002, ADR-0003 e ADR-0009.

**Decisão:** estrutura de pastas e projetos exatamente como no ADR-0001, com o registro de cada módulo por métodos de extensão e o host como único ponto de composição.

**Justificativa:** um ponto de composição único mantém os módulos independentes, e cada spec seguinte toca só a pasta do próprio módulo.

**Desvio do padrão existente:** Nenhum.

**Alternativas descartadas:** `.sln` clássico (o `.slnx` é o formato XML suportado pelo SDK atual e gera menos conflitos de merge); um projeto por camada por módulo (descartado no ADR-0001).

**ADRs:** ADR-0001, ADR-0002, ADR-0003, ADR-0009, ADR-0014.

## 5. Requisitos Não-Funcionais
- **Desempenho e escala:** build limpo da solução em até 3 min no runner do GitHub — medido no CI (SPEC-0005).
- **Segurança:** nenhum segredo versionado; `.gitignore` cobre `appsettings.*.local.json`, `.env` e `*.user`. Senha do Postgres local gerada pelo Aspire (parâmetro secreto) — verificado no G4.
- **Privacidade e dados pessoais:** N/A — nenhum dado é tratado nesta spec.
- **Disponibilidade e resiliência:** `/health/ready` retorna 503 enquanto o Postgres não responde — IT-02.
- **Acessibilidade (UI):** N/A — sem telas.
- **Custo:** N/A — somente desenvolvimento local.

## 6. Artefato A — Contrato
**Interface:** estrutura da solução + comandos de desenvolvimento + endpoints de saúde.

```text
Comandos (raiz do repositório):
  dotnet build Juker.slnx -c Release -warnaserror          → exit 0
  dotnet format Juker.slnx --verify-no-changes              → exit 0 (≠0 se houver desvio de estilo)
  dotnet run --project src/Aspire/Juker.AppHost             → sobe postgres:18 + juker-host
  dotnet build tests/fixtures/QualityViolations             → exit ≠ 0 com RS0030 (DateTime.Now) e MA0001 (string.Equals(a, b) sem StringComparison)

BannedSymbols.txt (ADR-0014), aplicado a src/ e tests/:
  P:System.DateTime.Now · P:System.DateTime.UtcNow · P:System.DateTime.Today
  P:System.DateTimeOffset.Now · P:System.DateTimeOffset.UtcNow          → use TimeProvider
  M:System.Threading.Thread.Sleep(…) · M:System.Threading.Tasks.Task.Delay(…) (só projetos de teste)
  M:System.Net.Http.HttpClient.#ctor                                      → IHttpClientFactory
  P:System.Threading.Tasks.Task`1.Result · M:System.Threading.Tasks.Task.Wait(…)
  T:System.Console (exceto src/Juker.Host/Cli/**)                         → ILogger

Registro de módulo (cada Juker.Modules.X):
  public static IServiceCollection AddXModule(this IServiceCollection s, IConfiguration c)
  public static IEndpointRouteBuilder MapXEndpoints(this IEndpointRouteBuilder e)

Catálogo (Juker.Host):
  public sealed record ModuleDescriptor(string Name,
    Func<IServiceCollection, IConfiguration, IServiceCollection> Add,
    Func<IEndpointRouteBuilder, IEndpointRouteBuilder> Map);
  public static class ModuleCatalog { public static IReadOnlyList<ModuleDescriptor> All { get; } }
    // exatamente 5 itens, nesta ordem: Venues, Sessions, Catalog, Queue, Playback;
    // Add/Map apontam para os métodos AddXModule/MapXEndpoints do módulo

HTTP (Juker.Host):
  GET /health/live   → 200 "Healthy"   (sempre que o processo está de pé)
  GET /health/ready  → 200 "Healthy"   (Postgres acessível)
                     → 503 "Unhealthy" (Postgres inacessível)
  GET /_diag/modules → 200 application/json ["Venues","Sessions","Catalog","Queue","Playback"]
                       (somente Development e Testing; nos demais ambientes → 404)
```

**Arquivos/módulos afetados:** ver `touches` no frontmatter.

### 6.1 Mapa de Comportamentos
| Cenário | Condição / Entrada | Resultado esperado | Testes |
|---|---|---|---|
| Solução sobe pelo Aspire | AppHost iniciado com Docker disponível | host em execução, `/health/ready` = 200 | IT-01 |
| Banco indisponível | Postgres parado depois da subida | `/health/ready` = 503, `/health/live` = 200 | IT-02 |
| Todos os módulos registrados | host iniciado | os cinco `Add*Module` executados e a rota de diagnóstico lista os cinco módulos | UT-01, IT-01 |
| Versões centralizadas | projeto declara `Version` em `PackageReference`; `Directory.Packages.props` sem o gerenciamento central estrito | nenhum `Version` nos projetos; `ManagePackageVersionsCentrally=true` e `CentralPackageVersionOverrideEnabled=false` | UT-02 |
| API proibida | código usa `DateTime.Now` | build falha com RS0030 | IT-03 |
| Regra de cultura | `string.Equals(a, b)` sem `StringComparison` | build falha com MA0001 (elevada a erro no `.editorconfig`) | IT-03 |
| Analisadores ativos na solução | build da solução real | nenhum aviso suprimido fora do `.editorconfig` (sem `#pragma warning disable` sem justificativa) | UT-03 |

## 7. Artefato B — Plano de Testes (TDD)

### 7.1 Testes de Caracterização
- N/A — projeto novo.

### 7.2 Testes Unitários
- **UT-01** — Dado o catálogo de módulos do host (`ModuleCatalog.All`), quando consultado, então contém exatamente 5 `ModuleDescriptor` com os nomes Venues, Sessions, Catalog, Queue e Playback, nessa ordem, e cada `Add`/`Map` não é nulo e, ao ser invocado num `ServiceCollection`/`WebApplication` de teste, executa sem exceção.
- **UT-02** — Dado qualquer `.csproj` sob `src/` e `tests/`, quando lido como XML, então nenhum `PackageReference` declara o atributo `Version`; e o `Directory.Packages.props` define `ManagePackageVersionsCentrally=true` e `CentralPackageVersionOverrideEnabled=false`.
- **UT-03** — Dado todos os arquivos `.cs` sob `src/` e `tests/`, quando lidos, então todo `#pragma warning disable` vem acompanhado de um comentário de justificativa na mesma linha ou na anterior; e o `Directory.Build.props` referencia o Meziantou e o BannedApiAnalyzers.

### 7.3 Testes de Integração
- **IT-01** — Com `Aspire.Hosting.Testing`, subir o AppHost (Postgres real em container), aguardar o host e chamar `/health/ready` → 200, e `/_diag/modules` (apenas em Development) → os cinco módulos.
- **IT-02** — Com o AppHost em execução, parar o recurso `postgres` e chamar `/health/ready` → 503 e `/health/live` → 200.
- **IT-03** — `dotnet build tests/fixtures/QualityViolations` (projeto que importa o `Directory.Build.props` e contém `DateTime.Now` e `string.Equals(a, b)`) termina com código ≠ 0 e a saída contém `RS0030` e `MA0001`; uma cópia do fixture sem essas duas linhas, construída a partir da raiz do repositório (para valer o `global.json`) com `-p:DirectoryBuildPropsPath` e `-p:DirectoryPackagesPropsPath` apontando para os arquivos da raiz e `--artifacts-path` num diretório temporário, compila com sucesso.

### 7.4 Testes de Contrato
- N/A — não há contrato entre specs fornecido ou consumido nesta spec; os métodos de registro são verificados por UT-01.

### 7.5 Testes E2E
- N/A — user_facing: false.

### 7.6 Outros
- Lint: `dotnet format --verify-no-changes` executado no G2 (evidência: saída do comando).

**Dublês e dados de teste:** nenhum; IT usa o Postgres real do AppHost.

**Ambiente de execução:** local com Docker; no CI com o Docker do runner Ubuntu (SPEC-0005).

## 8. Plano de Rollout
- **Estratégia:** deploy direto (não há nada publicado ainda).
- **Dados/schema:** N/A — sem migrations.
- **Compatibilidade:** N/A.
- **Observabilidade:** ServiceDefaults com OpenTelemetry apontando para o painel local do Aspire.
- **Rollback:** reverter o commit.
- **Etapas de migração/coexistência:** N/A.

## 9. Questões em Aberto
Nenhuma. A criação do repositório no GitHub é pré-requisito externo (seção 3); nenhum push acontece sem a confirmação do fundador.

## 10. Aprovação (H1)
Registrada no frontmatter (`approved_by`, `approved_at`) somente depois que o humano responder "Aprovado". O arquiteto nunca aprova a própria spec.

## 11. Checklist de Implementação

**Fase 1: Regras e componentes (unitário)**
- [ ] Red: escrever UT-01, UT-02, UT-03 com a tag `SPEC-0002:<ID>` e confirmar que falham pelo motivo certo
- [ ] Green: implementar o mínimo para passar, seguindo os ADRs de fundação e o padrão de slices
- [ ] Refactor mantendo tudo verde
- [ ] Validar: build + lint + suíte de UT (G2)

**Fase 2: Integração e contrato**
- [ ] Red: escrever IT-01, IT-02, IT-03 com a tag `SPEC-0002:<ID>` (Postgres real, esquemas de auth reais, `DrainOutboxAsync`, WireMock — ADR-0012) e confirmar que falham pelo motivo certo
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
| G0 Spec | PASS | `spec_graph.py validate SPEC-0002` → G0 [SPEC-0002]: PASS — 0 erro(s), 0 aviso(s); checklist de julgamento do G0 revisado pelo Architect (revisão ADR-0014) | 2026-09-30 |
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
| IMP-01 | 2026-09-30 | G1 | spec | IT-03 e o contrato (seção 6) divergem: string.Equals(a, b) não dispara MA0006 (dispara MA0001, que é Info por padrão; MA0006 é para a == b). Também faltam no contrato: resposta de /_diag/modules, forma de ModuleCatalog; UT-02 não bate com a linha do Mapa; IT-03 precisa definir como construir a cópia limpa sem perder o Directory.Build.props | Test-writer leu spec, ADRs e referência de testes e verificou empiricamente com Meziantou 3.0.290 no SDK 10.0.101; nenhum teste escrito | Architect + thomas (aprovação da emenda) | Emenda v2 — MA0001 elevada a erro no IT-03, ModuleCatalog e /_diag/modules no contrato, UT-02 e IT-03 ajustados (aprovada por thomas, 2026-09-30) | 2026-09-30 |

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
| 2 | 2026-09-30 | IT-03 verifica MA0001 (elevada a erro no `.editorconfig`) em vez de MA0006; forma do `ModuleCatalog`/`ModuleDescriptor` e de `/_diag/modules` definidas no contrato; UT-02 passa a verificar também o gerenciamento central estrito; IT-03 define como construir a cópia limpa | IMP-01 (SPEC_DEFECT do Test-writer): `string.Equals(a, b)` dispara MA0001 (Info por padrão), não MA0006; lacunas no contrato | Nenhuma (nenhuma spec consome o contrato da SPEC-0002) | thomas, 2026-09-30 |
