#!/usr/bin/env bash
# Regrava as respostas da YouTube Data API v3 servidas pelo FakeHttpServer (SPEC-0003, ADR-0012 regra 8).
#
# Uso (na raiz do repositório, fora do CI, com uma chave real):
#   YOUTUBE_API_KEY=... tools/recordings/refresh-youtube.sh
#
# Cada chamada listada em RECORDINGS vira um mapeamento estático do WireMock.Net em
# tests/Juker.TestKit/Recordings/youtube-<nome>.json. O mapeamento casa o método, o caminho (/youtube/v3/...) e os
# parâmetros da chamada, sem a chave. A chave nunca é gravada: ela não entra nos matchers, e o script falha se ela
# aparecer em alguma resposta.
#
# Dependências: bash, curl e jq. Nenhum teste executa este script: os testes só leem as gravações.
# As chamadas usadas pelo Catalog chegam com a SPEC-0014. Acrescente-as em RECORDINGS e revise o diff antes do commit.
set -euo pipefail

API_BASE="https://www.googleapis.com"
REPO_ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/../.." && pwd)"
OUT_DIR="${REPO_ROOT}/tests/Juker.TestKit/Recordings"

# nome|caminho|parâmetros separados por ";" (valores sem codificação, sem a chave).
# Parâmetros fixos para que as gravações sejam reproduzíveis.
RECORDINGS=(
  "search-karaoke|/youtube/v3/search|part=snippet;type=video;videoEmbeddable=true;maxResults=10;regionCode=BR;relevanceLanguage=pt;q=karaoke evidências"
  "videos-details|/youtube/v3/videos|part=snippet,contentDetails,status;id=ePjtnSPFWK8"
)

fail() { echo "refresh-youtube: $*" >&2; exit 1; }

[[ -n "${YOUTUBE_API_KEY:-}" ]] || fail "defina YOUTUBE_API_KEY com uma chave real da YouTube Data API v3."
command -v curl >/dev/null || fail "curl não encontrado."
command -v jq >/dev/null || fail "jq não encontrado."
mkdir -p "${OUT_DIR}"

record() {
  local name="$1" path="$2" params="$3"
  local target="${OUT_DIR}/youtube-${name}.json"
  local body status pair
  local -a curl_args=()

  IFS=';' read -r -a pairs <<< "${params}"
  for pair in "${pairs[@]}"; do
    curl_args+=(--data-urlencode "${pair}")
  done

  body="$(mktemp)"
  status="$(curl --silent --show-error --get --output "${body}" --write-out '%{http_code}' \
    "${curl_args[@]}" --data-urlencode "key=${YOUTUBE_API_KEY}" "${API_BASE}${path}")" || { rm -f "${body}"; fail "${name}: curl falhou."; }

  if [[ "${status}" != "200" ]]; then
    local message
    message="$(jq -r '.error.message? // empty' "${body}" 2>/dev/null || true)"
    rm -f "${body}"
    fail "${name}: HTTP ${status} em ${path}${message:+ (${message})}."
  fi

  if grep -qF -- "${YOUTUBE_API_KEY}" "${body}"; then
    rm -f "${body}"
    fail "${name}: a resposta contém a chave; nada foi gravado."
  fi

  jq --null-input --arg name "${name}" --arg path "${path}" --arg params "${params}" --arg source "${API_BASE}${path}" \
    --slurpfile response "${body}" '
    {
      Title: ("YouTube Data API v3 - " + $name),
      Description: ("Gravado de " + $source + " por tools/recordings/refresh-youtube.sh (sem a chave)."),
      Request: {
        Methods: ["GET"],
        Path: {Matchers: [{Name: "ExactMatcher", Pattern: $path}]},
        Params: ($params | split(";") | map(index("=") as $i | {
          Name: .[:$i],
          Matchers: [{Name: "ExactMatcher", Pattern: .[($i + 1):]}]
        }))
      },
      Response: {
        StatusCode: 200,
        Headers: {"Content-Type": "application/json; charset=UTF-8"},
        BodyAsJson: $response[0]
      }
    }' > "${target}"
  rm -f "${body}"

  echo "gravado: ${target#"${REPO_ROOT}/"}"
}

for entry in "${RECORDINGS[@]}"; do
  IFS='|' read -r name path params <<< "${entry}"
  record "${name}" "${path}" "${params}"
done
