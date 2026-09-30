# Juker

Jukebox colaborativa para bares: os clientes entram pelo QR do bar, pedem músicas e votam na fila; a TV do bar toca a fila.
Monólito modular em .NET 10 com Aspire, Postgres e Blazor.

## Pré-requisitos
- .NET SDK 10 (versão fixada em `global.json`)
- Docker (ou Podman) em execução — o Aspire sobe o Postgres em container

## Rodar localmente
```bash
dotnet run --project src/Aspire/Juker.AppHost
```
O AppHost sobe o `postgres` (imagem `postgres:18`, banco `juker`) e o `juker-host`, e abre o painel do Aspire
(o endereço aparece no terminal). A senha do Postgres é um parâmetro secreto gerado pelo Aspire e guardado nos
user secrets do AppHost — nada é versionado.

Endpoints do host:
- `GET /health/live` — processo de pé
- `GET /health/ready` — pronto para atender (inclui o Postgres; 503 se ele não responde)
- `GET /_diag/modules` — módulos registrados (só em `Development` e `Testing`)

## Desenvolvimento
| Objetivo | Comando |
|---|---|
| Build | `dotnet build Juker.slnx -c Release -warnaserror` |
| Testes | `dotnet test --project tests/Juker.IntegrationTests -c Release` |
| Lint | `dotnet format Juker.slnx --verify-no-changes` |

Convenções de código, commits (Conventional Commits com `Refs: SPEC-NNNN`), CHANGELOG (Keep a Changelog) e
versões (SemVer) estão em [AGENTS.md](AGENTS.md). Specs em [docs/specs](docs/specs/INDEX.md), decisões em [docs/adr](docs/adr).
