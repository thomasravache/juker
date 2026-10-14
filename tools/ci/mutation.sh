#!/usr/bin/env bash
# Mutation testing com o Stryker.NET (ADR-0014 regra 6; SPEC-0005) a partir da raiz do repositório:
#
#   tools/ci/mutation.sh -f tests/<Projeto>.UnitTests/stryker-config.json [outras opções do dotnet stryker]
#
# O Stryker escolhe o modo pela pasta de trabalho: na raiz, onde está a Juker.slnx, ele entra em modo solução e
# testa cada mutante com todos os projetos de teste que referenciam o projeto mutado (inclusive integração e E2E,
# com Docker). Por isso o script entra na pasta do stryker-config.json (a do projeto de teste) antes de rodar, como o
# job mutation do .github/workflows/mutation.yml. O dotnet-stryker é a ferramenta local de .config/dotnet-tools.json.
#
# Relatórios (html, json e md) em artifacts/stryker/<projeto de teste>/reports/. Score abaixo de thresholds.break
# (80) termina com código diferente de zero.
set -euo pipefail

die() {
  echo "mutation: $*" >&2
  exit 2
}

[[ "${1:-}" == "-f" || "${1:-}" == "--config-file" ]] && [[ -n "${2:-}" ]] \
  || die "uso: $0 -f tests/<Projeto>.UnitTests/stryker-config.json [opções do dotnet stryker]"
config="$2"
shift 2

root="$(cd "$(dirname "${BASH_SOURCE[0]}")/../.." && pwd)"
[[ -f "$root/$config" ]] || die "$config não existe (caminho relativo à raiz do repositório)"
project_dir="$(cd "$root/$(dirname "$config")" && pwd)"
output="$root/artifacts/stryker/$(basename "$project_dir")"

cd "$root"
dotnet tool restore > /dev/null
cd "$project_dir"
exec dotnet stryker -f "$(basename "$config")" --output "$output" --skip-version-check "$@"
