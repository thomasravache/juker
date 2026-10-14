#!/usr/bin/env bash
# Cobertura com o coverlet (ADR-0014 regra 5; SPEC-0005, jobs unit e integration do ci.yml) em volta de um comando de
# teste já compilado:
#
#   tools/ci/coverage.sh <pasta de saída> -- dotnet test --solution Juker.slnx -c Release --no-build [...]
#
# Grava um <projeto de teste>.cobertura.xml por projeto de teste na pasta de saída; o job coverage junta todos com o
# ReportGenerator. O código de saída é o do comando de teste (diferente de zero se algum teste falhar).
#
# Por que encadeado: cada projeto de teste carrega a própria cópia dos assemblies de src/ (bin/Release/net10.0), e o
# coverlet.console instrumenta uma única cópia de cada assembly por execução, mesmo com --include-directory. Então o
# script aninha uma execução do coverlet por pasta de saída de projeto de teste, cada uma instrumentando só a sua, e a
# mais interna roda o comando uma única vez. O comando precisa de --no-build: recompilar desfaria a instrumentação.
set -euo pipefail

die() {
  echo "coverage: $*" >&2
  exit 2
}

script="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)/$(basename "${BASH_SOURCE[0]}")"
root="$(cd "$(dirname "$script")/../.." && pwd)"

if [[ "${1:-}" == "--level" ]]; then
  level="$2"
  state="${JUKER_COVERAGE_STATE:?estado ausente}"
  mapfile -t outputs < "$state/outputs"
  if ((level >= ${#outputs[@]})); then
    # Nível mais interno: todas as cópias estão instrumentadas; roda o comando de teste.
    mapfile -d '' -t command < "$state/command"
    cd "$root"
    exec "${command[@]}"
  fi
  output="${outputs[$level]}"
  project="$(basename "$(dirname "$(dirname "$(dirname "$output")")")")"
  cd "$root"
  exec dotnet tool run coverlet "$output" \
    --include "[Juker.*]*" --exclude "[*Tests*]*" --exclude "[Juker.TestKit*]*" --exclude "[Juker.AppHost]*" \
    --exclude-by-file "**/Migrations/*.cs" --exclude-by-file "**/obj/**" \
    --format cobertura --output "$(cat "$state/destination")/$project.cobertura.xml" --verbosity minimal \
    --target bash --targetargs "$script --level $((level + 1))"
fi

destination="${1:-}"
[[ -n "$destination" && "${2:-}" == "--" && $# -gt 2 ]] \
  || die "uso: $0 <pasta de saída> -- <comando de teste com --no-build>"
shift 2

cd "$root"
mkdir -p "$destination"
state="$(mktemp -d "${RUNNER_TEMP:-${TMPDIR:-/tmp}}/juker-coverage.XXXXXX")"
trap 'rm -rf "$state"' EXIT
(cd "$destination" && pwd) > "$state/destination"
printf '%s\0' "$@" > "$state/command"
# Pastas de saída dos projetos de teste (os executáveis do Microsoft.Testing.Platform têm runtimeconfig.json).
for config in tests/*/bin/Release/net10.0/*.runtimeconfig.json; do
  [[ -e "$config" ]] && dirname "$config"
done | sort -u > "$state/outputs"
[[ -s "$state/outputs" ]] || die "nenhum projeto de teste compilado em tests/*/bin/Release/net10.0 (rode o build antes)"

export JUKER_COVERAGE_STATE="$state"
status=0
bash "$script" --level 0 || status=$?
exit "$status"
