#!/usr/bin/env bash
# SPEC-0027:IT-02 — regressão do teste instável SPEC-0003:CT-01
# (TestKitContractTests.Comando_test_integration_lista_somente_os_testes_Integration, que roda um `dotnet test`
# aninhado dentro da suíte e falhou de forma intermitente em execuções completas).
#
# Roda N execuções completas seguidas do comando `test` do sdd-config.yml e guarda a saída de cada uma.
# Passa só se todas terminarem com sucesso; ao falhar, diz se o CT-01 está entre os testes que falharam.
# A correção só é aceita com 10 execuções seguidas sem falha (SPEC-0027, seção 3).
#
# Uso (na raiz do repositório ou em qualquer pasta dele; Docker necessário):
#   tests/Juker.IntegrationTests/Harness/repeat-test-suite.sh [execuções=10] [pasta de saída=artifacts/spec-0027-it-02]
set -euo pipefail

runs="${1:-10}"
root="$(cd "$(dirname "${BASH_SOURCE[0]}")/../../.." && pwd)"
output="${2:-$root/artifacts/spec-0027-it-02}"
ct01="Comando_test_integration_lista_somente_os_testes_Integration"

mkdir -p "$output"
cd "$root"

failures=0
for run in $(seq 1 "$runs"); do
  log="$output/run-$run.log"
  if dotnet test --solution Juker.slnx -c Release >"$log" 2>&1; then
    echo "execução $run/$runs: ok ($(grep -E '^\s+(total|failed|succeeded):' "$log" | tr -s ' ' | tr '\n' ' '))"
  else
    failures=$((failures + 1))
    if grep -E "failed .*$ct01" "$log" >/dev/null; then
      echo "execução $run/$runs: FALHOU — o SPEC-0003:CT-01 ($ct01) falhou; saída em $log"
    else
      echo "execução $run/$runs: FALHOU sem o CT-01 entre as falhas; saída em $log"
    fi
  fi
done

if [ "$failures" -ne 0 ]; then
  echo "SPEC-0027:IT-02 falhou: $failures de $runs execuções com falha (saídas em $output)"
  exit 1
fi

echo "SPEC-0027:IT-02 ok: $runs execuções completas seguidas sem falha (saídas em $output)"
