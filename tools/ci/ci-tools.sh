#!/usr/bin/env bash
# Ferramentas de verificação do CI fixadas por versão e SHA-256 (SPEC-0005:UT-01).
#
#   tools/ci/ci-tools.sh actionlint [argumentos]   roda o actionlint (com o shellcheck fixado abaixo)
#   tools/ci/ci-tools.sh shellcheck [argumentos]   roda o shellcheck
#   tools/ci/ci-tools.sh path <actionlint|shellcheck>  imprime o caminho do binário (baixa se preciso)
#
# Os binários oficiais das releases do GitHub são baixados uma vez para o cache, sem sudo:
#   ${JUKER_TOOLS_CACHE:-${XDG_CACHE_HOME:-$HOME/.cache}/juker/tools}/<ferramenta>/<versão>/
# Cada arquivo baixado é conferido pelo SHA-256 fixado aqui; um hash diferente aborta sem instalar nada.
# O actionlint recebe sempre o shellcheck desta versão e nunca o pyflakes, para que o resultado local e o do runner
# sejam os mesmos (o runner do GitHub traz outro shellcheck no PATH).
set -euo pipefail

readonly ACTIONLINT_VERSION="1.7.12"
readonly SHELLCHECK_VERSION="0.11.0"

cache_root="${JUKER_TOOLS_CACHE:-${XDG_CACHE_HOME:-$HOME/.cache}/juker/tools}"

die() {
  echo "ci-tools: $*" >&2
  exit 2
}

platform() {
  local os arch
  os="$(uname -s)"
  arch="$(uname -m)"
  case "$os" in
    Linux) os="linux" ;;
    Darwin) os="darwin" ;;
    *) die "sistema não suportado: $os" ;;
  esac
  case "$arch" in
    x86_64 | amd64) arch="amd64" ;;
    aarch64 | arm64) arch="arm64" ;;
    *) die "arquitetura não suportada: $arch" ;;
  esac
  echo "${os}_${arch}"
}

# SHA-256 publicados em actionlint_<versão>_checksums.txt da release.
actionlint_sha256() {
  case "$1" in
    linux_amd64) echo "8aca8db96f1b94770f1b0d72b6dddcb1ebb8123cb3712530b08cc387b349a3d8" ;;
    linux_arm64) echo "325e971b6ba9bfa504672e29be93c24981eeb1c07576d730e9f7c8805afff0c6" ;;
    darwin_amd64) echo "5b44c3bc2255115c9b69e30efc0fecdf498fdb63c5d58e17084fd5f16324c644" ;;
    darwin_arm64) echo "aba9ced2dee8d27fecca3dc7feb1a7f9a52caefa1eb46f3271ea66b6e0e6953f" ;;
    *) die "actionlint sem hash para $1" ;;
  esac
}

# SHA-256 dos arquivos da release v0.11.0 do shellcheck (a release não publica checksums; calculados no download
# feito para a SPEC-0005 e conferidos a cada instalação).
shellcheck_sha256() {
  case "$1" in
    linux_amd64) echo "b7af85e41cc99489dcc21d66c6d5f3685138f06d34651e6d34b42ec6d54fe6f6" ;;
    linux_arm64) echo "68a8133197a50beb8803f8d42f9908d1af1c5540d4bb05fdfca8c1fa47decefc" ;;
    darwin_amd64) echo "c2c15e08df0e8fbc374c335b230a7ee958c313fa5714817a59aa59f1aa594f51" ;;
    darwin_arm64) echo "339b930feb1ea764467013cc1f72d09cd6b869ebf1013296ba9055ab2ffbd26f" ;;
    *) die "shellcheck sem hash para $1" ;;
  esac
}

sha256_of() {
  if command -v sha256sum > /dev/null 2>&1; then
    sha256sum "$1" | cut -d' ' -f1
  else
    shasum -a 256 "$1" | cut -d' ' -f1
  fi
}

# install <nome> <versão> <url> <sha256> <caminho do binário dentro do .tar.gz>
install_tool() {
  local name="$1" version="$2" url="$3" expected="$4" member="$5"
  local dir="$cache_root/$name/$version"
  local binary="$dir/$name"
  if [[ -x "$binary" ]]; then
    echo "$binary"
    return
  fi

  command -v curl > /dev/null 2>&1 || die "curl não encontrado (necessário para baixar $name $version)"
  mkdir -p "$dir"
  local staging
  staging="$(mktemp -d "$dir/.download.XXXXXX")"
  # shellcheck disable=SC2064 # o caminho é fixado agora, de propósito
  trap "rm -rf '$staging'" RETURN

  curl --fail --silent --show-error --location --retry 2 --output "$staging/archive.tar.gz" "$url" \
    || die "falha ao baixar $url"
  local actual
  actual="$(sha256_of "$staging/archive.tar.gz")"
  [[ "$actual" == "$expected" ]] || die "SHA-256 inesperado para $url: $actual (esperado $expected)"

  mkdir -p "$staging/extracted"
  tar -xzf "$staging/archive.tar.gz" -C "$staging/extracted" "$member"
  chmod +x "$staging/extracted/$member"
  mv "$staging/extracted/$member" "$binary"
  echo "$binary"
}

actionlint_path() {
  local p
  p="$(platform)"
  install_tool actionlint "$ACTIONLINT_VERSION" \
    "https://github.com/rhysd/actionlint/releases/download/v${ACTIONLINT_VERSION}/actionlint_${ACTIONLINT_VERSION}_${p}.tar.gz" \
    "$(actionlint_sha256 "$p")" "actionlint"
}

shellcheck_path() {
  local p asset
  p="$(platform)"
  case "$p" in
    linux_amd64) asset="linux.x86_64" ;;
    linux_arm64) asset="linux.aarch64" ;;
    darwin_amd64) asset="darwin.x86_64" ;;
    darwin_arm64) asset="darwin.aarch64" ;;
  esac
  install_tool shellcheck "$SHELLCHECK_VERSION" \
    "https://github.com/koalaman/shellcheck/releases/download/v${SHELLCHECK_VERSION}/shellcheck-v${SHELLCHECK_VERSION}.${asset}.tar.gz" \
    "$(shellcheck_sha256 "$p")" "shellcheck-v${SHELLCHECK_VERSION}/shellcheck"
}

command="${1:-}"
[[ -n "$command" ]] || die "uso: $0 <actionlint|shellcheck|path> [argumentos]"
shift

case "$command" in
  actionlint)
    shellcheck_bin="$(shellcheck_path)"
    actionlint_bin="$(actionlint_path)"
    exec "$actionlint_bin" -shellcheck "$shellcheck_bin" -pyflakes "" "$@"
    ;;
  shellcheck)
    shellcheck_bin="$(shellcheck_path)"
    exec "$shellcheck_bin" "$@"
    ;;
  path)
    case "${1:-}" in
      actionlint) actionlint_path ;;
      shellcheck) shellcheck_path ;;
      *) die "uso: $0 path <actionlint|shellcheck>" ;;
    esac
    ;;
  *)
    die "comando desconhecido: $command (use actionlint, shellcheck ou path)"
    ;;
esac
