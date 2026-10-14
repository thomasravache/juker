# CI do Juker

Pipeline de integração contínua no GitHub Actions (SPEC-0005; ADR-0009, ADR-0011, ADR-0013, ADR-0014, ADR-0016 e
ADR-0017). São dois workflows:

| Workflow | Quando roda | Bloqueia PR? |
|---|---|---|
| `.github/workflows/ci.yml` | todo PR para a `main` e todo push na `main` | pela política: só há merge com todos os checks verdes (ver abaixo) |
| `.github/workflows/mutation.yml` | domingo às 03:17 UTC e sob demanda (Actions → mutation → Run workflow) | não |

Hoje o bloqueio é de processo, não da plataforma: pela política `h2_policy: auto-on-green` do
`docs/specs/sdd-config.yml`, um PR só é mesclado com todos os checks do `ci.yml` verdes. A proteção da `main` pelo
GitHub (branch protection ou ruleset) espera a decisão de custo do fundador, porque no GitHub Free um repositório
privado não tem esse recurso (a API responde 403); veja "Ativar a proteção da main".

As atualizações de dependência chegam pelo Dependabot (`.github/dependabot.yml`): NuGet e GitHub Actions toda
segunda, um PR agrupado por ecossistema e no máximo 2 PRs abertos de cada.

## Checks obrigatórios

O nome de cada check é o id do job no `ci.yml`. São estes 10 os checks que a política de merge exige verdes e que a proteção da `main` vai exigir quando for ativada:

| Check | O que roda | Falha quando |
|---|---|---|
| `build` | `dotnet build Juker.slnx -c Release -warnaserror` | erro de compilação ou qualquer aviso (analisadores, APIs proibidas) |
| `lint` | `dotnet format Juker.slnx --verify-no-changes` | arquivo fora da formatação ou do estilo do `.editorconfig` |
| `unit` | `dotnet test --solution Juker.slnx -c Release --filter-trait "Category=Unit"`, com cobertura do coverlet | teste de unidade vermelho |
| `integration` | `dotnet test --solution Juker.slnx -c Release --filter-trait "Category=Integration"` (Postgres no Docker do runner; Chromium do Playwright para os testes do harness de E2E), com cobertura | teste de integração vermelho |
| `architecture` | `dotnet test --project tests/Juker.ArchitectureTests -c Release` | violação de ARQ-01..ARQ-08 ou das convenções de teste |
| `e2e` | `playwright install --with-deps chromium webkit` e `dotnet test --project tests/Juker.E2ETests -c Release` | jornada E2E vermelha (Chromium ou WebKit) |
| `security` | `dotnet list package --vulnerable --include-transitive` e `gitleaks detect` | pacote com vulnerabilidade High/Critical ou segredo no histórico |
| `coverage` | ReportGenerator junta a cobertura do `unit` e do `integration`; `diff-cover --compare-branch=origin/main --fail-under=80` | menos de 80% das linhas alteradas do PR cobertas |
| `sdd` | `spec_graph.py validate --exclude-drafts` e, no PR, `spec_graph.py pr-check` | spec inválida; PR sem spec aprovada, sem gates, fora do `touches` ou com título fora do padrão |
| `image` | imagem do `Juker.Host` pelo SDK container; no push na `main`, publica `ghcr.io/<owner>/juker:<sha>` | falha no publish do container (só roda depois de todos os outros) |

Renomear ou remover um job exige atualizar a lista de checks obrigatórios na proteção da branch (seção
"Ativar a proteção da main"); um check obrigatório que não existe mais deixa todo PR esperando para sempre.

O `mutation.yml` não é check obrigatório.

## Como ler uma falha

Abra o PR → aba **Checks** (ou `gh pr checks <número>`), clique no job vermelho e no passo que falhou.
`gh run view <id-da-execução> --log-failed` mostra só os passos que falharam. Cada job de teste também escreve um
resumo na página da execução (aba **Summary**).

| Job | Onde olhar |
|---|---|
| `build`, `lint` | o log do passo traz o arquivo, a linha e a regra (`CA…`, `MA…`, `RS0030`, `IDE…`). Para corrigir a formatação: `dotnet format Juker.slnx` |
| `unit`, `integration`, `architecture`, `e2e` | o log lista `failed <teste>` com a mensagem; o TRX completo está no artefato `test-results-<job>` |
| `e2e` | com falha, o artefato `playwright-traces` traz `trace.zip`, `video.webm` e `screenshot.png` de cada teste que falhou. Abra o trace com `npx -y playwright@1.63.0 show-trace trace.zip` |
| `security` | pacotes vulneráveis: tabela no resumo do job, com o aviso de cada um (atualize a versão no `Directory.Packages.props`). Segredo: o gitleaks mostra arquivo, linha, commit e regra (o valor sai mascarado) |
| `coverage` | o log e as anotações do PR mostram os arquivos com a porcentagem e as linhas alteradas sem teste; o artefato `coverage-report` tem o `diff-cover.html` e o resumo geral (`index.html`) |
| `sdd` | cada linha `FAIL` do `pr-check` diz o que falta (spec não aprovada, gate sem evidência, arquivo fora do `touches`). Gere título e corpo com `python3 tools/sdd/spec_graph.py pr SPEC-NNNN` |
| `image` | o digest da imagem fica no resumo do job e no output `image_digest` |

Segredo encontrado pelo `security`: trate como vazado. Revogue e troque a credencial antes de qualquer outra coisa
(ADR-0016); depois remova o arquivo do PR. Falso positivo comprovado: comentário `gitleaks:allow` na linha ou uma
exceção estreita no `.gitleaks.toml`, com o motivo, revisada no PR.

Depois de editar o título ou o corpo do PR, rode de novo só o job `sdd` (**Re-run jobs** → `sdd`, ou
`gh run rerun <id> --job <id-do-job>`): o passo relê o título e o corpo atuais, e o `image` roda junto em seguida.

### Artefatos

Todos ficam guardados por 7 dias.

| Artefato | Job | Conteúdo |
|---|---|---|
| `test-results-unit`, `test-results-integration`, `test-results-architecture`, `test-results-e2e` | o job de teste | TRX de cada projeto de teste (sempre, inclusive com falha) |
| `playwright-traces` | `e2e` | só quando o `e2e` falha: `TestResults/playwright/<teste>/` com trace, vídeo e screenshot |
| `coverage-unit`, `coverage-integration` | `unit`, `integration` | um Cobertura por projeto de teste (entrada do job `coverage`) |
| `coverage-report` | `coverage` | `Cobertura.xml` juntado, `diff-cover.html`/`.md` e o resumo HTML da cobertura total (informativo: não há meta global) |
| `mutation-report` | `mutation.yml` | relatório do Stryker (`mutation-report.html`, `.json` e `.md`) |

Para baixar: `gh run download <id> --name <artefato>`.

## Rodar localmente

Os mesmos comandos dos jobs, na raiz do repositório:

| Para | Comando |
|---|---|
| actionlint dos workflows | `bash tools/ci/ci-tools.sh actionlint .github/workflows/*.yml` (baixa o actionlint e o shellcheck fixados por SHA-256) |
| testes de contrato do CI | `dotnet test --project tests/Juker.CiTests -c Release` |
| segredos | `bash tools/ci/ci-tools.sh gitleaks detect --source . --config .gitleaks.toml --log-opts=HEAD --redact` |
| pacotes vulneráveis | `dotnet list Juker.slnx package --vulnerable --include-transitive` |
| cobertura | `dotnet build Juker.slnx -c Release`, depois `bash tools/ci/coverage.sh artifacts/coverage/unit -- dotnet test --solution Juker.slnx -c Release --filter-trait "Category=Unit" --no-build` e `dotnet tool run reportgenerator "-reports:artifacts/coverage/**/*.cobertura.xml" -targetdir:artifacts/coverage-report -reporttypes:Cobertura`; o diff-cover sai de `pip install --require-hashes --no-deps -r tools/ci/requirements-diff-cover.txt` num venv |
| mutation testing | `bash tools/ci/mutation.sh -f tests/Juker.BuildingBlocks.UnitTests/stryker-config.json` (relatório em `artifacts/stryker/`) |

O `tools/ci/coverage.sh` aninha uma execução do coverlet por projeto de teste, porque cada projeto carrega a própria
cópia dos assemblies de `src/` e o coverlet.console instrumenta uma cópia de cada assembly por execução. O comando de
teste roda uma vez só, com `--no-build` (recompilar desfaria a instrumentação).

## Mutation testing

O Stryker.NET (`dotnet-stryker`, ferramenta local em `.config/dotnet-tools.json`) muta só as classes críticas listadas
no `mutate` de cada `tests/**/stryker-config.json`, com `thresholds.break` em 80 (ADR-0014, regra 6). Score abaixo de
80% deixa o `mutation.yml` vermelho, sem bloquear PR; o caminho é abrir uma spec `lite` para reforçar os testes.

Hoje há um único `tests/Juker.BuildingBlocks.UnitTests/stryker-config.json`. As classes críticas nomeadas no
ADR-0014 (`FairQueuePolicy`, `VotePolicy`, `PlaybackStateMachine`, `PinVerifier`) ainda não existem; até lá, o alvo
são as regras dos building blocks de que todo módulo depende:

- `ResultHttpExtensions` — a tabela `ErrorType` → status HTTP e o ProblemDetails com `code` (contrato de erro da API);
- `ValidationCommandHandlerDecorator` e `ValidationQueryHandlerDecorator` — nenhum comando ou consulta inválido
  chega ao handler;
- `TenantQueryFilterConvention` — o filtro de tenant que isola os dados de cada bar (multi-tenancy, ADR-0008).

A spec que criar uma classe crítica acrescenta um `stryker-config.json` no projeto de teste do módulo (ou o arquivo
ao `mutate` de um existente) e o caminho no `STRYKER_CONFIG` do `mutation.yml`. O Stryker roda na pasta do
`stryker-config.json` (o `tools/ci/mutation.sh` e o job já fazem isso): na raiz, ao lado da `Juker.slnx`, ele entra em
modo solução e testaria cada mutante com todos os projetos de teste, inclusive integração e E2E. O runner de testes
é o do Microsoft.Testing.Platform (`"test-runner": "mtp"`, em preview no Stryker 5.0); com o VSTest a captura de
cobertura falha e todo mutante sobrevive.

## Cota e custo

O repositório é privado no GitHub Free: 2.000 minutos de Actions por mês e 500 MB de armazenamento de artefatos
(compartilhados com o Packages). As imagens no GHCR hoje não são cobradas (docs.github.com, consultado em
2026-10-13). O que mantém o CI dentro da cota (emenda v3 da SPEC-0005):

- `concurrency` com `cancel-in-progress`: um push novo num PR cancela a execução anterior; na `main` cada merge vai
  até o fim (cada SHA tem a sua imagem);
- artefatos com `retention-days: 7`; o `playwright-traces` só existe quando o `e2e` falha;
- `actions/cache` para `~/.nuget/packages` e `~/.cache/ms-playwright` (o cache do Actions tem cota própria de 10 GB e
  não conta nos 500 MB);
- a saída do build (~900 MB) não vira artefato: cada job compila o que usa;
- mutation semanal, e não diário; Dependabot agrupado, no máximo 2 PRs por ecossistema.

Estimativa por execução completa do `ci.yml` (runner de 2 núcleos, cobrança por job arredondada para cima):

| Job | min-runner |
|---|---|
| `build` | 3 |
| `lint` | 3 |
| `unit` | 3 |
| `integration` | 6 |
| `architecture` | 3 |
| `e2e` | 7 |
| `security` | 2 |
| `coverage` | 2 |
| `sdd` | 1 |
| `image` | 3 |
| **Total** | **~33** (a SPEC-0005 estima ~40) |

Com ~40 execuções por mês (PRs, pushes por PR e merges) são ~1.300 min, mais ~4 execuções do `mutation.yml`
(~5 min cada) e os PRs do Dependabot. O consumo real fica em Settings → Billing and plans → Usage (ou na página
`https://github.com/settings/billing`); a duração e os minutos de cada execução, em
`gh api repos/<owner>/juker/actions/runs/<id>/timing`. Qualquer coisa que exija plano pago é decisão do fundador
(ADR-0019).

## Ativar a proteção da main (ação do fundador)

A proteção é configuração da conta, fora do repositório; ative depois que um PR saudável passar com todos os checks
verdes (IT-04 da SPEC-0005).

**Pré-requisito de plano:** no GitHub Free, a proteção de branch e os rulesets só existem em repositório **público**.
Para o repositório privado, a API responde "Upgrade to GitHub Pro or make this repository public to enable this
feature" (verificado em 2026-10-14). As opções são assinar o GitHub Pro, tornar o repositório público ou seguir sem
proteção, contando só com a política de merge dos agentes (`h2_policy: auto-on-green`: merge só com o CI inteiro
verde). É decisão de custo do fundador (ADR-0019).

Com o recurso disponível, por ruleset (recomendado):

1. No repositório, abra **Settings → Rules → Rulesets → New ruleset → New branch ruleset**.
2. **Ruleset name:** `main`. **Enforcement status:** `Active`.
3. **Target branches → Add target → Include default branch** (a `main`).
4. Marque **Restrict deletions** e **Block force pushes**.
5. Marque **Require a pull request before merging**. Em **Allowed merge methods**, deixe só **Merge** (merge commit,
   ADR-0015).
6. Marque **Require status checks to pass**. Em **Add checks**, acrescente um por um, com a fonte
   **GitHub Actions**: `build`, `lint`, `unit`, `integration`, `architecture`, `e2e`, `security`, `coverage`, `sdd` e
   `image`. Um check só aparece na busca depois de rodar uma vez no repositório. **Require branches to be up to date
   before merging** é opcional: deixa a `main` mais segura, mas cada atualização da branch do PR roda o CI inteiro de
   novo (~30 min-runner).
7. Em **Bypass list**, não acrescente ninguém (nem administradores), para que a regra valha também para o fundador.
8. **Create**.
9. Confira: `gh api repos/<owner>/juker/rules/branches/main --jq '.[].type'` lista `required_status_checks` e
   `pull_request`. Abra um PR com um teste vermelho (ou rode o IT-01 do
   `tests/Juker.CiTests/Verification/ci-check.sh`): o botão de merge fica bloqueado.

Pela proteção clássica, o caminho equivalente é **Settings → Branches → Add classic branch protection rule**, com
**Branch name pattern** `main`, **Require a pull request before merging**, **Require status checks to pass before
merging** (com os mesmos 10 checks) e **Do not allow bypassing the above settings**.

Ao mudar um nome de job no `ci.yml`, atualize a lista de checks no mesmo PR (e o `docs/ci.md`).

## Pacote no GHCR

O job `image` publica `ghcr.io/<owner>/juker:<sha>` só no push na `main`, com o `GITHUB_TOKEN` (`packages: write`
apenas nesse job). O label `org.opencontainers.image.source` liga o pacote ao repositório. O pacote nasce privado,
como o repositório; o deploy (SPEC-0006) usa o output `image_digest` e a tag do SHA.
