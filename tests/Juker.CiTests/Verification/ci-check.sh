#!/usr/bin/env bash
# Verificação do pipeline de CI no GitHub real — SPEC-0005:IT-01..IT-07 (seção 7.3 da spec).
#
# Cada cenário cria uma branch descartável ci-check/* a partir da branch da spec (origin/<--spec-branch>), aplica o
# fixture de tests/Juker.CiTests/Fixtures/CiCheck/, abre o PR para a main (ou dispara o mutation.yml), espera os checks
# com `gh pr checks --watch`, confere o resultado esperado, imprime a URL da execução (a evidência) e limpa tudo:
# fecha o PR, apaga a branch remota e local e remove o worktree temporário.
#
# Nada é mesclado na main, exceto no IT-04 com --merge-it04 (exige --it04-pr <número>: o PR de integração escolhido
# pelo Architect, mesclado com merge commit como manda o ADR-0015).
#
# Uso:
#   tests/Juker.CiTests/Verification/ci-check.sh --list
#   tests/Juker.CiTests/Verification/ci-check.sh --dry-run [all | IT-0N ...]
#   tests/Juker.CiTests/Verification/ci-check.sh [opções] all | IT-0N ...
#
# Opções:
#   --spec-branch <branch>   branch de origem dos cenários (padrão: sdd/SPEC-0005-pipeline-de-ci); precisa estar no origin
#   --repo <owner/repo>      repositório (padrão: o do `gh repo view`)
#   --base <branch>          branch alvo dos PRs (padrão: main)
#   --it04-pr <número>       IT-04 usa esse PR existente em vez de abrir ci-check/healthy (não é fechado no fim)
#   --merge-it04             IT-04 mescla o --it04-pr (merge commit) e confere a imagem no GHCR com a tag do SHA
#   --evidence <arquivo>     acrescenta uma linha TSV por cenário (id, resultado, URL, observação)
#   --keep                   não limpa (depuração): deixa PRs, branches e worktrees
#   --timeout-minutes <n>    prazo por espera (padrão: 60)
#   --dry-run                só mostra o plano de cada cenário e confere os fixtures; não chama git remoto nem gh
#   --list                   lista os cenários
#
# Pré-requisitos: bash 4+, git, python3, gh autenticado com acesso de escrita ao repositório (escopos repo e workflow; e
# read:packages para o IT-04 consultar o GHCR: `gh auth refresh -s read:packages`). O IT-07 dispara o mutation.yml
# por workflow_dispatch, que o GitHub só aceita quando o workflow já está na branch padrão (rode depois do merge do
# IT-04). Custo: cada cenário com PR consome uma execução completa do CI (~40 min-runner, SPEC-0005 seção 5).
set -euo pipefail

readonly SPEC="SPEC-0005"
readonly ALL_SCENARIOS=(IT-01 IT-02 IT-03 IT-04 IT-05 IT-06 IT-07)
readonly CI_WORKFLOW="ci.yml"
readonly MUTATION_WORKFLOW="mutation.yml"
readonly IT04_BUDGET_MS=720000 # 12 min (SPEC-0005, seção 5)

declare -A BRANCH=(
  [IT-01]="ci-check/red-test"
  [IT-02]="ci-check/no-spec"
  [IT-03]="ci-check/secret"
  [IT-04]="ci-check/healthy"
  [IT-05]="ci-check/e2e-fail"
  [IT-06]="ci-check/uncovered"
  [IT-07]="ci-check/weak-mutation"
)

declare -A TITLE=(
  [IT-01]="Teste vermelho: o job unit falha e o PR não pode ser mesclado"
  [IT-02]="PR sem spec: o job sdd falha no pr-check"
  [IT-03]="Segredo commitado: o job security falha (gitleaks)"
  [IT-04]="PR saudável: todos os checks verdes em até 12 min; após o merge, imagem no GHCR com a tag do SHA"
  [IT-05]="E2E falha: o job e2e fica vermelho e o artefato playwright-traces traz o trace.zip"
  [IT-06]="Cobertura do diff: o job coverage falha com a porcentagem; com o teste, passa"
  [IT-07]="Mutation: o disparo manual gera mutation-report; com teste fraco, termina vermelho"
)

declare -A FIXTURES=(
  [IT-01]="red-test/TesteVermelhoPropositalTests.cs"
  [IT-02]=""
  [IT-03]="secret/ci-check-fake-secret.txt.template"
  [IT-04]=""
  [IT-05]="e2e-fail/E2EFalhaPropositalTests.cs"
  [IT-06]="uncovered/UncoveredSample.cs uncovered/UncoveredSampleTests.cs"
  [IT-07]="weak-mutation/WeakMutationSample.cs weak-mutation/WeakMutationSampleTests.cs"
)

declare -A PLAN=(
  [IT-01]="copia o teste vermelho para tests/Juker.BuildingBlocks.UnitTests/CiCheck/; abre PR; espera: job 'unit' = failure; com a proteção da main ativa, mergeStateStatus = BLOCKED"
  [IT-02]="commit vazio; abre PR com título e corpo sem SPEC-NNNN; espera: job 'sdd' = failure e o log cita 'não referencia nenhuma spec'"
  [IT-03]="gera um token FALSO ghp_FAKE<aleatório> em ci-check-fake-secret.txt (só nesta branch); abre PR; espera: job 'security' = failure e o log do gitleaks cita 'leaks found'"
  [IT-04]="commit vazio (ou o PR de --it04-pr); confere o pr-check local; abre PR com título/corpo do 'spec_graph.py pr SPEC-0005'; espera: todos os jobs = success, duração <= 12 min, nenhuma tag do SHA no GHCR; com --merge-it04: merge commit, push na main verde e ghcr.io/<owner>/juker:<sha> publicado (digest impresso)"
  [IT-05]="copia o E2E vermelho para tests/Juker.E2ETests/CiCheck/; abre PR; espera: job 'e2e' = failure e artefato 'playwright-traces' com trace.zip"
  [IT-06]="fase 1: UncoveredSample.cs no módulo Queue sem teste; espera: job 'coverage' = failure com a porcentagem no log; fase 2: acrescenta o teste (e tira JukerAllowZeroTests do Queue.UnitTests); espera: job 'coverage' = success"
  [IT-07]="precondição: mutation.yml na main; A: dispara o mutation.yml na branch da spec, espera o artefato 'mutation-report'; B: classe com teste fraco no projeto do stryker-config.json do sdd-config (mutate só nela), dispara na branch, espera conclusão failure e o artefato 'mutation-report'"
)

spec_branch="sdd/SPEC-0005-pipeline-de-ci"
repo=""
base="main"
it04_pr=""
merge_it04=false
evidence=""
keep=false
dry_run=false
timeout_minutes=60
selected=()

script_dir="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
fixtures_dir="$(cd "$script_dir/../Fixtures/CiCheck" && pwd)"
repo_root="$(git -C "$script_dir" rev-parse --show-toplevel)"

log() { printf '[%s] %s\n' "${current:-ci-check}" "$*" >&2; }
die() {
  printf 'ci-check: %s\n' "$*" >&2
  exit 2
}

usage() {
  sed -n '2,/^set -euo pipefail/p' "${BASH_SOURCE[0]}" | sed -e '/^set -euo/d' -e 's/^# \{0,1\}//'
}

list_scenarios() {
  local id
  for id in "${ALL_SCENARIOS[@]}"; do
    printf '%s:%s\t%s\t%s\n' "$SPEC" "$id" "${BRANCH[$id]}" "${TITLE[$id]}"
  done
}

while (($# > 0)); do
  case "$1" in
    --spec-branch) spec_branch="${2:?--spec-branch exige um valor}"; shift 2 ;;
    --repo) repo="${2:?--repo exige um valor}"; shift 2 ;;
    --base) base="${2:?--base exige um valor}"; shift 2 ;;
    --it04-pr) it04_pr="${2:?--it04-pr exige um número}"; shift 2 ;;
    --merge-it04) merge_it04=true; shift ;;
    --evidence) evidence="${2:?--evidence exige um arquivo}"; shift 2 ;;
    --keep) keep=true; shift ;;
    --timeout-minutes) timeout_minutes="${2:?--timeout-minutes exige um número}"; shift 2 ;;
    --dry-run) dry_run=true; shift ;;
    --list) list_scenarios; exit 0 ;;
    -h | --help) usage; exit 0 ;;
    all) selected+=("${ALL_SCENARIOS[@]}"); shift ;;
    IT-0[1-7]) selected+=("$1"); shift ;;
    *) die "argumento desconhecido: $1 (use --help)" ;;
  esac
done

((${#selected[@]} > 0)) || die "nenhum cenário escolhido (use 'all' ou IT-01..IT-07; --list mostra os cenários)"
[[ "$timeout_minutes" =~ ^[0-9]+$ ]] || die "--timeout-minutes precisa ser um número"
[[ -z "$it04_pr" || "$it04_pr" =~ ^[0-9]+$ ]] || die "--it04-pr precisa ser o número do PR"
if [[ "$merge_it04" == true && -z "$it04_pr" ]]; then
  die "--merge-it04 exige --it04-pr <número>: só o PR de integração escolhido pelo Architect é mesclado na main (ADR-0015)"
fi

check_fixtures() {
  local id="$1" fixture
  # shellcheck disable=SC2086 # a lista de fixtures é separada por espaço de propósito
  for fixture in ${FIXTURES[$id]}; do
    [[ -f "$fixtures_dir/$fixture" ]] || die "$SPEC:$id: fixture ausente: $fixtures_dir/$fixture"
  done
}

if [[ "$dry_run" == true ]]; then
  echo "ci-check (dry-run): nada é criado, enviado ou fechado."
  echo "origem: origin/$spec_branch | alvo dos PRs: $base | repositório: ${repo:-<gh repo view>}"
  for id in "${selected[@]}"; do
    check_fixtures "$id"
    echo
    echo "$SPEC:$id — ${TITLE[$id]}"
    if [[ "$id" == "IT-04" && -n "$it04_pr" ]]; then
      echo "  branch:   (usa o PR #$it04_pr existente)"
    else
      echo "  branch:   ${BRANCH[$id]} (de origin/$spec_branch, apagada no fim)"
    fi
    echo "  fixtures: ${FIXTURES[$id]:-(nenhum)}"
    echo "  plano:    ${PLAN[$id]}"
    if [[ "$id" == "IT-04" ]]; then
      echo "  merge:    $([[ "$merge_it04" == true ]] && echo "sim, PR #$it04_pr com merge commit" || echo "não (precisa de --it04-pr N --merge-it04)")"
    fi
  done
  exit 0
fi

for tool in git gh python3; do
  command -v "$tool" > /dev/null 2>&1 || die "pré-requisito ausente: $tool"
done
gh auth status > /dev/null 2>&1 || die "gh não autenticado (gh auth login)"
if [[ -z "$repo" ]]; then
  repo="$(gh repo view --json nameWithOwner --jq .nameWithOwner)"
fi
owner="${repo%%/*}"
for id in "${selected[@]}"; do
  check_fixtures "$id"
done

git -C "$repo_root" fetch --quiet origin "$spec_branch" "$base" \
  || die "não foi possível buscar origin/$spec_branch e origin/$base (a branch da spec precisa estar no GitHub)"

deadline_seconds=$((timeout_minutes * 60))

# ----------------------------------------------------------------------------------------------- utilitários

iso_utc_seconds_ago() {
  local epoch=$(($(date -u +%s) - $1))
  date -u -d "@$epoch" +%Y-%m-%dT%H:%M:%SZ 2> /dev/null || date -u -r "$epoch" +%Y-%m-%dT%H:%M:%SZ
}

# Espera até que o comando imprima algo não vazio; imprime esse valor.
wait_for_output() {
  local description="$1"
  shift
  local waited=0 value
  while ((waited < deadline_seconds)); do
    value="$("$@" 2> /dev/null || true)"
    if [[ -n "$value" ]]; then
      printf '%s\n' "$value"
      return 0
    fi
    sleep 15
    waited=$((waited + 15))
  done
  log "prazo esgotado esperando: $description"
  return 1
}

# Pela API de runs (campo path), que acha o workflow mesmo antes de ele existir na branch padrão.
ci_run_for() { # <sha> <evento>
  gh api "repos/$repo/actions/runs?head_sha=$1&event=$2&per_page=20" \
    --jq "[.workflow_runs[] | select(.path == \".github/workflows/$CI_WORKFLOW\")][0].id // empty"
}

wait_run_completed() { # <run id>
  gh run watch "$1" --repo "$repo" --interval 30 > /dev/null 2>&1 || true
  local status
  status="$(gh run view "$1" --repo "$repo" --json status --jq .status)"
  [[ "$status" == "completed" ]] || {
    log "execução $1 ainda '$status' depois da espera"
    return 1
  }
}

run_url() { gh run view "$1" --repo "$repo" --json url --jq .url; }

run_conclusion() { gh run view "$1" --repo "$repo" --json conclusion --jq .conclusion; }

job_conclusion() { # <run id> <job>
  gh run view "$1" --repo "$repo" --json jobs --jq ".jobs[] | select(.name == \"$2\") | .conclusion"
}

non_successful_jobs() { # <run id>
  gh run view "$1" --repo "$repo" --json jobs --jq '.jobs[] | select(.conclusion != "success") | "\(.name)=\(.conclusion)"'
}

artifact_names() { # <run id>
  gh api "repos/$repo/actions/runs/$1/artifacts" --paginate --jq '.artifacts[].name'
}

failed_log() { gh run view "$1" --repo "$repo" --log-failed 2> /dev/null || true; }

# Versões do pacote container juker no GHCR com a tag informada (imprime o digest de cada uma).
ghcr_digests_with_tag() { # <tag>
  local endpoint output
  if [[ "$(gh api user --jq .login)" == "$owner" ]]; then
    endpoint="/user/packages/container/juker/versions"
  else
    endpoint="/orgs/$owner/packages/container/juker/versions"
  fi
  if ! output="$(gh api "$endpoint" --paginate --jq ".[] | select(.metadata.container.tags | index(\"$1\")) | .name" 2>&1)"; then
    if [[ "$output" == *"404"* || "$output" == *"Not Found"* ]]; then
      return 0 # pacote ainda não existe: nenhuma versão com a tag
    fi
    log "não foi possível consultar o GHCR ($output); o token precisa de read:packages (gh auth refresh -s read:packages)"
    return 1
  fi
  printf '%s\n' "$output"
}

# Proteção clássica da branch ou regras de ruleset com checks obrigatórios.
# Qualquer falha das chamadas conta como "sem proteção": num repositório privado do GitHub Free as duas APIs respondem
# 403, e o `gh api --jq` imprime o JSON do erro no stdout antes de sair com código != 0. Por isso a contagem só é lida
# quando o comando teve sucesso, e só vale se for um inteiro > 0.
branch_protected() {
  local required_checks
  if gh api "repos/$repo/branches/$base/protection" > /dev/null 2>&1; then
    return 0
  fi
  required_checks="$(gh api "repos/$repo/rules/branches/$base" \
    --jq '[.[] | select(.type == "required_status_checks")] | length' 2> /dev/null)" || return 1
  [[ "$required_checks" =~ ^[0-9]+$ ]] && ((required_checks > 0))
}

# ------------------------------------------------------------------------- branch, PR e limpeza por cenário

worktree=""
pr_number=""
created_branch=""
close_pr=false
current=""

cleanup() {
  if [[ "$keep" == true ]]; then
    log "--keep: PR '${pr_number:-}', branch '${created_branch:-}' e worktree '${worktree:-}' ficam como estão"
    return
  fi
  if [[ -n "$pr_number" && "$close_pr" == true ]]; then
    gh pr close "$pr_number" --repo "$repo" --delete-branch \
      --comment "Verificação $SPEC:$current concluída pelo ci-check.sh; PR descartável fechado sem merge." > /dev/null 2>&1 || true
  fi
  if [[ -n "$created_branch" ]]; then
    if git -C "$repo_root" ls-remote --exit-code --heads origin "$created_branch" > /dev/null 2>&1; then
      git -C "$repo_root" push --quiet origin --delete "$created_branch" > /dev/null 2>&1 || log "não consegui apagar origin/$created_branch"
    fi
  fi
  if [[ -n "$worktree" ]]; then
    git -C "$repo_root" worktree remove --force "$worktree" > /dev/null 2>&1 || true
    rm -rf "$(dirname "$worktree")"
  fi
  if [[ -n "$created_branch" ]]; then
    git -C "$repo_root" branch -D "$created_branch" > /dev/null 2>&1 || true
  fi
}

# Cria a branch descartável num worktree temporário, a partir de origin/<spec-branch>.
start_branch() { # <id>
  created_branch="${BRANCH[$1]}"
  if git -C "$repo_root" ls-remote --exit-code --heads origin "$created_branch" > /dev/null 2>&1; then
    local leftover="$created_branch"
    created_branch="" # não é nossa: não apagar
    log "origin/$leftover já existe (sobra de outra execução?); apague-a antes: git push origin --delete $leftover"
    return 1
  fi
  if git -C "$repo_root" show-ref --verify --quiet "refs/heads/$created_branch"; then
    local leftover="$created_branch"
    created_branch=""
    log "a branch local $leftover já existe; apague-a antes: git branch -D $leftover"
    return 1
  fi
  local tmp
  tmp="$(mktemp -d "${TMPDIR:-/tmp}/juker-ci-check.XXXXXX")"
  worktree="$tmp/worktree"
  git -C "$repo_root" worktree add --quiet -b "$created_branch" "$worktree" "origin/$spec_branch"
}

wt() { git -C "$worktree" "$@"; }

# Commit no worktree descartável (sem hooks: o fixture não é código de produção e a branch é apagada no fim).
commit_fixture() { # <mensagem>
  wt add -A
  wt commit --quiet --no-verify --allow-empty -m "$1" -m "Branch descartável da verificação do CI; nunca entra na main." -m "Refs: $SPEC"
}

push_branch() { wt push --quiet -u origin "$created_branch"; }

open_pr() { # <título> <corpo>
  local body_file
  body_file="$(dirname "$worktree")/pr-body.md"
  printf '%s\n' "$2" > "$body_file"
  local url
  url="$(gh pr create --repo "$repo" --base "$base" --head "$created_branch" --title "$1" --body-file "$body_file")"
  pr_number="${url##*/}"
  close_pr=true
  log "PR #$pr_number aberto ($url)"
}

disposable_body() { # <id>
  printf 'Refs: %s\n\nPR de verificação descartável do CI (%s:%s — %s), aberto e fechado pelo tests/Juker.CiTests/Verification/ci-check.sh. Não mesclar.\n' \
    "$SPEC" "$SPEC" "$1" "${TITLE[$1]}"
}

# Espera a execução do ci.yml do commit, observa os checks do PR e imprime o id da execução.
watch_pr_ci() { # <sha>
  local run
  run="$(wait_for_output "execução do $CI_WORKFLOW para $1" ci_run_for "$1" pull_request)" || return 1
  log "execução $(run_url "$run")"
  gh pr checks "$pr_number" --repo "$repo" --watch --interval 30 > /dev/null 2>&1 || true
  wait_run_completed "$run" || return 1
  printf '%s\n' "$run"
}

expect_job() { # <run> <job> <conclusão>
  local actual
  actual="$(job_conclusion "$1" "$2")"
  if [[ "$actual" != "$3" ]]; then
    log "esperado job '$2' = $3, obtido '${actual:-ausente}'"
    return 1
  fi
  log "job '$2' = $3 (como esperado)"
}

result_note=""
result_url=""
result_partial=false
outcome=""

# Grava o resultado do cenário (chamado pelo trap EXIT do subshell, inclusive quando o cenário falha).
save_outcome() { printf '%s\n%s\n%s\n' "$result_partial" "$result_url" "$result_note" > "$outcome"; }

# ------------------------------------------------------------------------------------------------ cenários

# SPEC-0005:IT-01 — Teste vermelho: o job unit falha e o PR não pode ser mesclado
scenario_IT_01() {
  start_branch IT-01
  mkdir -p "$worktree/tests/Juker.BuildingBlocks.UnitTests/CiCheck"
  cp "$fixtures_dir/red-test/TesteVermelhoPropositalTests.cs" "$worktree/tests/Juker.BuildingBlocks.UnitTests/CiCheck/"
  commit_fixture "test(ci): teste vermelho proposital da verificação $SPEC:IT-01"
  push_branch
  open_pr "test(ci): verificação descartável IT-01, teste vermelho [$SPEC]" "$(disposable_body IT-01)"
  local run
  run="$(watch_pr_ci "$(wt rev-parse HEAD)")"
  result_url="$(run_url "$run")"
  expect_job "$run" unit failure
  if branch_protected; then
    local state
    state="$(gh pr view "$pr_number" --repo "$repo" --json mergeStateStatus --jq .mergeStateStatus)"
    [[ "$state" == "BLOCKED" ]] || {
      log "esperado mergeStateStatus = BLOCKED, obtido '$state'"
      return 1
    }
    result_note="unit=failure; merge bloqueado (BLOCKED)"
  else
    result_partial=true
    result_note="unit=failure; proteção da $base ainda não ativada: bloqueio do merge não verificável (repita depois de ativar, docs/ci.md)"
  fi
}

# SPEC-0005:IT-02 — PR sem spec: o job sdd falha no pr-check
scenario_IT_02() {
  start_branch IT-02
  commit_fixture "chore(ci): commit vazio da verificação $SPEC:IT-02"
  push_branch
  # Título e corpo sem nenhuma referência SPEC-NNNN e sem [no-spec].
  open_pr "chore(ci): verificação descartável do job sdd sem spec" \
    "PR de verificação descartável do CI: cenário sem referência a spec (o job sdd deve falhar no pr-check). Aberto e fechado pelo ci-check.sh. Não mesclar."
  local run
  run="$(watch_pr_ci "$(wt rev-parse HEAD)")"
  result_url="$(run_url "$run")"
  expect_job "$run" sdd failure
  if ! failed_log "$run" | grep -q "não referencia nenhuma spec"; then
    log "o log do job sdd não cita 'não referencia nenhuma spec'"
    return 1
  fi
  result_note="sdd=failure no pr-check (PR não referencia nenhuma spec)"
}

# SPEC-0005:IT-03 — Segredo commitado: o job security falha (gitleaks)
scenario_IT_03() {
  start_branch IT-03
  local token
  # Token FALSO: prefixo ghp_FAKE + 32 caracteres aleatórios (formato do github-pat do gitleaks); só nesta branch.
  token="ghp_FAKE$(python3 -I -c 'import secrets, string; print("".join(secrets.choice(string.ascii_letters + string.digits) for _ in range(32)))')"
  sed "s/@@FAKE_TOKEN@@/$token/" "$fixtures_dir/secret/ci-check-fake-secret.txt.template" > "$worktree/ci-check-fake-secret.txt"
  commit_fixture "test(ci): chave falsa da verificação $SPEC:IT-03"
  push_branch || {
    log "o push foi recusado (proteção de push do GitHub?); o cenário não pôde ser verificado"
    return 1
  }
  open_pr "test(ci): verificação descartável IT-03, segredo falso [$SPEC]" "$(disposable_body IT-03)"
  local run
  run="$(watch_pr_ci "$(wt rev-parse HEAD)")"
  result_url="$(run_url "$run")"
  expect_job "$run" security failure
  if ! failed_log "$run" | grep -qi "leaks found"; then
    log "o log do job security não traz o achado do gitleaks ('leaks found')"
    return 1
  fi
  result_note="security=failure (gitleaks: leaks found)"
}

# SPEC-0005:IT-04 — PR saudável: todos os checks verdes em até 12 min; após o merge, imagem no GHCR com a tag do SHA
scenario_IT_04() {
  local head_sha
  if [[ -n "$it04_pr" ]]; then
    pr_number="$it04_pr"
    close_pr=false
    head_sha="$(gh pr view "$pr_number" --repo "$repo" --json headRefOid --jq .headRefOid)"
    log "usando o PR #$pr_number (head $head_sha)"
  else
    start_branch IT-04
    local title body_file
    title="$(cd "$worktree" && python3 tools/sdd/spec_graph.py pr "$SPEC" --title)"
    body_file="$(dirname "$worktree")/pr-body.md"
    (cd "$worktree" && python3 tools/sdd/spec_graph.py pr "$SPEC" --out "$body_file" > /dev/null)
    if ! (cd "$worktree" && python3 tools/sdd/spec_graph.py pr-check --title "$title" --body-file "$body_file" --base "origin/$base"); then
      log "precondição: o pr-check local falha (os gates G0..G4 da $SPEC precisam estar PASS para o PR saudável)"
      return 1
    fi
    commit_fixture "chore(ci): commit vazio da verificação $SPEC:IT-04"
    push_branch
    open_pr "$title" "$(cat "$body_file")"
    head_sha="$(wt rev-parse HEAD)"
  fi

  local run
  run="$(watch_pr_ci "$head_sha")"
  result_url="$(run_url "$run")"
  local failing
  failing="$(non_successful_jobs "$run")"
  if [[ -n "$failing" ]]; then
    log "jobs que não passaram: $(tr '\n' ' ' <<< "$failing")"
    return 1
  fi
  local duration_ms billable_ms
  duration_ms="$(gh api "repos/$repo/actions/runs/$run/timing" --jq '.run_duration_ms // empty')"
  billable_ms="$(gh api "repos/$repo/actions/runs/$run/timing" --jq '[.billable[]?.total_ms] | add // 0')"
  log "duração: $((duration_ms / 1000)) s; minutos-runner faturáveis: $((billable_ms / 60000))"
  if ((duration_ms > IT04_BUDGET_MS)); then
    log "duração acima de 12 min ($((duration_ms / 1000)) s)"
    return 1
  fi
  local published
  published="$(ghcr_digests_with_tag "$head_sha")" || return 1
  if [[ -n "$published" ]]; then
    log "o PR publicou imagem no GHCR com a tag $head_sha (só o push na main publica)"
    return 1
  fi
  result_note="todos os checks verdes em $((duration_ms / 1000)) s ($((billable_ms / 60000)) min-runner); imagem construída sem publicar"

  if [[ "$merge_it04" != true ]]; then
    result_partial=true
    result_note="$result_note; fase pós-merge não executada (use --it04-pr N --merge-it04)"
    return 0
  fi

  log "mesclando o PR #$pr_number na $base com merge commit (--merge-it04)"
  gh pr merge "$pr_number" --repo "$repo" --merge
  close_pr=false
  local merge_sha push_run digest
  merge_sha="$(wait_for_output "merge commit do PR #$pr_number" gh pr view "$pr_number" --repo "$repo" --json mergeCommit --jq '.mergeCommit.oid // empty')"
  push_run="$(wait_for_output "execução do $CI_WORKFLOW no push $merge_sha" ci_run_for "$merge_sha" push)"
  log "execução do push na $base: $(run_url "$push_run")"
  wait_run_completed "$push_run"
  failing="$(non_successful_jobs "$push_run")"
  if [[ -n "$failing" ]]; then
    log "jobs do push na $base que não passaram: $(tr '\n' ' ' <<< "$failing")"
    return 1
  fi
  digest="$(ghcr_digests_with_tag "$merge_sha")" || return 1
  if [[ -z "$digest" ]]; then
    log "nenhuma imagem ghcr.io/$owner/juker:$merge_sha no GHCR depois do push na $base"
    return 1
  fi
  result_url="$result_url $(run_url "$push_run")"
  result_note="$result_note; após o merge: ghcr.io/$owner/juker:$merge_sha publicado (digest $digest)"
}

# SPEC-0005:IT-05 — E2E falha: o job e2e fica vermelho e o artefato playwright-traces traz o trace.zip
scenario_IT_05() {
  start_branch IT-05
  mkdir -p "$worktree/tests/Juker.E2ETests/CiCheck"
  cp "$fixtures_dir/e2e-fail/E2EFalhaPropositalTests.cs" "$worktree/tests/Juker.E2ETests/CiCheck/"
  commit_fixture "test(ci): E2E vermelho proposital da verificação $SPEC:IT-05"
  push_branch
  open_pr "test(ci): verificação descartável IT-05, E2E vermelho [$SPEC]" "$(disposable_body IT-05)"
  local run
  run="$(watch_pr_ci "$(wt rev-parse HEAD)")"
  result_url="$(run_url "$run")"
  expect_job "$run" e2e failure
  if ! artifact_names "$run" | grep -qx "playwright-traces"; then
    log "a execução não tem o artefato playwright-traces"
    return 1
  fi
  local download
  download="$(dirname "$worktree")/playwright-traces"
  gh run download "$run" --repo "$repo" --name playwright-traces --dir "$download"
  local trace
  trace="$(find "$download" -name trace.zip -path '*Falha_de_proposito*' -print -quit)"
  [[ -n "$trace" ]] || trace="$(find "$download" -name trace.zip -print -quit)"
  if [[ -z "$trace" ]]; then
    log "o artefato playwright-traces não contém trace.zip"
    return 1
  fi
  result_note="e2e=failure; playwright-traces contém ${trace#"$download"/}"
}

# SPEC-0005:IT-06 — Cobertura do diff: o job coverage falha com a porcentagem; com o teste, passa
scenario_IT_06() {
  start_branch IT-06
  local module="$worktree/src/Modules/Queue/Juker.Modules.Queue"
  local tests="$worktree/tests/Juker.Modules.Queue.UnitTests"
  [[ -d "$module" && -d "$tests" ]] || {
    log "módulo Queue ou Juker.Modules.Queue.UnitTests não encontrado na branch"
    return 1
  }
  mkdir -p "$module/CiCheck"
  cp "$fixtures_dir/uncovered/UncoveredSample.cs" "$module/CiCheck/"
  commit_fixture "test(ci): método novo sem teste da verificação $SPEC:IT-06"
  push_branch
  open_pr "test(ci): verificação descartável IT-06, cobertura do diff [$SPEC]" "$(disposable_body IT-06)"

  # Fase 1: sem teste, o coverage falha informando a porcentagem das linhas alteradas.
  local run
  run="$(watch_pr_ci "$(wt rev-parse HEAD)")"
  result_url="$(run_url "$run")"
  expect_job "$run" coverage failure
  local coverage_log
  coverage_log="$(failed_log "$run")"
  if ! grep -q "UncoveredSample.cs" <<< "$coverage_log" || ! grep -Eq '[0-9]+(\.[0-9]+)?%' <<< "$coverage_log"; then
    log "o log do coverage não cita UncoveredSample.cs com a porcentagem das linhas alteradas"
    return 1
  fi

  # Fase 2: com o teste cobrindo o método, o coverage passa.
  mkdir -p "$tests/CiCheck"
  cp "$fixtures_dir/uncovered/UncoveredSampleTests.cs" "$tests/CiCheck/"
  sed -i.bak '/<JukerAllowZeroTests>true<\/JukerAllowZeroTests>/d' "$tests/Juker.Modules.Queue.UnitTests.csproj"
  rm -f "$tests/Juker.Modules.Queue.UnitTests.csproj.bak"
  commit_fixture "test(ci): teste que cobre o método da verificação $SPEC:IT-06"
  wt push --quiet origin "$created_branch"
  local covered_run
  covered_run="$(watch_pr_ci "$(wt rev-parse HEAD)")"
  expect_job "$covered_run" coverage success
  result_url="$result_url $(run_url "$covered_run")"
  result_note="sem teste: coverage=failure com a porcentagem; com teste: coverage=success"
}

mutation_run_after() { # <branch> <desde (ISO)>
  gh api "repos/$repo/actions/runs?branch=$1&event=workflow_dispatch&per_page=20" \
    --jq "[.workflow_runs[] | select(.path == \".github/workflows/$MUTATION_WORKFLOW\" and .created_at >= \"$2\")][0].id // empty"
}

dispatch_mutation() { # <ref>; imprime o id da execução concluída
  local since run
  since="$(iso_utc_seconds_ago 30)"
  gh workflow run "$MUTATION_WORKFLOW" --repo "$repo" --ref "$1" > /dev/null
  run="$(wait_for_output "execução do $MUTATION_WORKFLOW em $1" mutation_run_after "$1" "$since")" || return 1
  log "execução $(run_url "$run")"
  wait_run_completed "$run" || return 1
  printf '%s\n' "$run"
}

# SPEC-0005:IT-07 — Mutation: o disparo manual gera mutation-report; com teste fraco, termina vermelho
scenario_IT_07() {
  if ! gh api "repos/$repo/contents/.github/workflows/$MUTATION_WORKFLOW?ref=$base" > /dev/null 2>&1; then
    log "precondição: .github/workflows/$MUTATION_WORKFLOW precisa estar na $base (workflow_dispatch); rode depois do merge do IT-04"
    return 1
  fi

  # A: disparo manual na branch da spec gera o mutation-report (o score real não é asserção deste cenário).
  local real_run
  real_run="$(dispatch_mutation "$spec_branch")"
  result_url="$(run_url "$real_run")"
  if ! artifact_names "$real_run" | grep -qx "mutation-report"; then
    log "o disparo manual na $spec_branch não gerou o artefato mutation-report"
    return 1
  fi
  local real_conclusion
  real_conclusion="$(run_conclusion "$real_run")"

  # B: classe com teste fraco, mutate só nela: o workflow termina vermelho por score abaixo de 80%.
  start_branch IT-07
  local layout
  layout="$(python3 -I - "$worktree" "${IT07_STRYKER_CONFIG:-}" "$fixtures_dir/weak-mutation" << 'PY'
import json, pathlib, re, shlex, sys

root, override, fixtures = pathlib.Path(sys.argv[1]), sys.argv[2], pathlib.Path(sys.argv[3])

def fail(message):
    print(message, file=sys.stderr)
    sys.exit(1)

config_path = override
if not config_path:
    for line in (root / "docs/specs/sdd-config.yml").read_text(encoding="utf-8").splitlines():
        match = re.match(r'^mutation:\s*"(.*)"', line)
        if match:
            args = shlex.split(match.group(1))
            for flag in ("-f", "--config-file"):
                if flag in args:
                    config_path = args[args.index(flag) + 1]
if not config_path:
    fail("sem stryker-config.json: preencha 'mutation' no sdd-config.yml (dotnet stryker -f <config>) ou defina IT07_STRYKER_CONFIG")

config_file = root / config_path
if not config_file.is_file():
    fail(f"{config_path} não existe na branch")
config = json.loads(config_file.read_text(encoding="utf-8"))
stryker = config.setdefault("stryker-config", {})
if stryker.get("thresholds", {}).get("break") != 80:
    fail(f"{config_path}: thresholds.break deveria ser 80")

test_dir = config_file.parent
test_projects = sorted(test_dir.glob("*.csproj"))
if len(test_projects) != 1:
    fail(f"esperado um .csproj de teste em {test_dir.relative_to(root)}")
test_project = test_projects[0]

project_name = stryker.get("project")
if project_name:
    candidates = [p for p in (root / "src").rglob(project_name) if "bin" not in p.parts and "obj" not in p.parts]
else:
    text = test_project.read_text(encoding="utf-8")
    refs = re.findall(r'ProjectReference Include="([^"]*src[^"]*\.csproj)"', text)
    candidates = [(test_dir / ref.replace("\\", "/")).resolve() for ref in refs]
if len(candidates) != 1:
    fail(f"não foi possível achar o projeto mutado de {config_path} (project: {project_name!r})")
source_project = candidates[0]

namespace = source_project.stem + ".CiCheck"
test_namespace = test_project.stem + ".CiCheck"
source_dir = source_project.parent / "CiCheck"
target_test_dir = test_dir / "CiCheck"
source_dir.mkdir(exist_ok=True)
target_test_dir.mkdir(exist_ok=True)
for name, target in (("WeakMutationSample.cs", source_dir), ("WeakMutationSampleTests.cs", target_test_dir)):
    text = (fixtures / name).read_text(encoding="utf-8")
    text = text.replace("@@TEST_NAMESPACE@@", test_namespace).replace("@@NAMESPACE@@", namespace)
    (target / name).write_text(text, encoding="utf-8")

stryker["mutate"] = ["**/CiCheck/WeakMutationSample.cs"]
config_file.write_text(json.dumps(config, indent=2) + "\n", encoding="utf-8")

csproj = test_project.read_text(encoding="utf-8")
test_project.write_text(re.sub(r"\s*<JukerAllowZeroTests>true</JukerAllowZeroTests>", "", csproj), encoding="utf-8")
print(f"{config_path} -> {source_project.relative_to(root)}")
PY
)" || {
    log "não foi possível montar o cenário de teste fraco"
    return 1
  }
  log "teste fraco: $layout"
  commit_fixture "test(ci): classe com teste fraco da verificação $SPEC:IT-07"
  push_branch
  local weak_run
  weak_run="$(dispatch_mutation "$created_branch")"
  result_url="$result_url $(run_url "$weak_run")"
  local weak_conclusion
  weak_conclusion="$(run_conclusion "$weak_run")"
  if [[ "$weak_conclusion" != "failure" ]]; then
    log "esperado mutation.yml = failure com o teste fraco, obtido '$weak_conclusion'"
    return 1
  fi
  if ! artifact_names "$weak_run" | grep -qx "mutation-report"; then
    log "a execução com teste fraco não gerou o artefato mutation-report"
    return 1
  fi
  if ! failed_log "$weak_run" | grep -Eqi "break|threshold"; then
    log "aviso: o log não cita o limite de break; confira na execução que a falha é o score abaixo de 80%"
  fi
  result_note="disparo na $spec_branch: mutation-report gerado (conclusão $real_conclusion); teste fraco: failure com mutation-report"
}

# --------------------------------------------------------------------------------------------- execução

declare -A results=()
declare -A urls=()
declare -A notes=()
failures=0

for id in "${selected[@]}"; do
  current="$id"
  log "== $SPEC:$id — ${TITLE[$id]}"
  status=0
  # Cada cenário roda num subshell com a própria limpeza; o resultado volta por um arquivo temporário.
  outcome="$(mktemp)"
  # errexit vale dentro do subshell só se ele não estiver numa lista com || ou &&: por isso o set +e em volta.
  set +e
  (
    set -e
    trap 'save_outcome; cleanup' EXIT
    "scenario_${id//-/_}"
  )
  status=$?
  set -e
  partial="$(sed -n 1p "$outcome")"
  urls[$id]="$(sed -n 2p "$outcome")"
  notes[$id]="$(sed -n 3p "$outcome")"
  rm -f "$outcome"
  if ((status != 0)); then
    results[$id]="FAIL"
    failures=$((failures + 1))
  elif [[ "$partial" == true ]]; then
    results[$id]="PARTIAL"
  else
    results[$id]="PASS"
  fi
  printf '%s:%s\t%s\t%s\t%s\n' "$SPEC" "$id" "${results[$id]}" "${urls[$id]:--}" "${notes[$id]:--}"
  if [[ -n "$evidence" ]]; then
    printf '%s:%s\t%s\t%s\t%s\n' "$SPEC" "$id" "${results[$id]}" "${urls[$id]:--}" "${notes[$id]:--}" >> "$evidence"
  fi
done

current=""
echo
echo "Resumo ($repo, origem origin/$spec_branch):"
for id in "${selected[@]}"; do
  printf '  %s:%s  %-7s  %s\n' "$SPEC" "$id" "${results[$id]}" "${urls[$id]:--}"
done
((failures == 0))
