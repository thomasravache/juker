---
id: ADR-0002
title: Backend em .NET 10 LTS com minimal APIs e handlers próprios (sem MediatR)
status: accepted
origin: decision
date: 2026-09-30
decision_makers: [thomas]
consulted: [claude]
informed: []
supersedes:
superseded_by:
enforced_by: tests/Juker.ArchitectureTests — regra ARQ-06 (sem referência a MediatR/MassTransit/FluentAssertions ≥ 8) e revisão G4
---

# ADR-0002 — Backend em .NET 10 LTS com minimal APIs e handlers próprios (sem MediatR)

## Contexto e Problema
O fundador domina .NET e quer usar Aspire e Blazor. É preciso definir runtime, estilo de API, validação e como cada slice despacha comandos e consultas. O padrão do Milan usa MediatR, que passou a ter licença comercial em julho de 2025.

## Direcionadores da Decisão
- Domínio do time: .NET/C#.
- Versão com suporte longo (LTS) para não migrar no meio do piloto.
- Evitar dependências com licença comercial ou recíproca (RPL) num produto que será vendido.
- Slices simples: endpoint → handler → resultado.

## Opções Consideradas
- .NET 10 LTS + minimal APIs + handlers próprios (interfaces `ICommandHandler<,>`/`IQueryHandler<,>` e decorators) + FluentValidation
- .NET 10 LTS + minimal APIs + biblioteca `Mediator` (source generator, MIT)
- .NET 10 LTS + MediatR 13 (licença comercial/RPL)

## Resultado da Decisão
**Opção escolhida:** "handlers próprios", porque são cerca de 50 linhas no `Juker.Infrastructure`, sem licença a gerenciar, sem reflexão em runtime e com decorators explícitos (validação, logging, transação). Estilo de API: REST com minimal APIs, erros como `ProblemDetails` (RFC 9457) com `code` estável por erro de negócio. Resultados de domínio via tipo `Result`/`Error` no SharedKernel (sem exceções para fluxo de negócio). Jobs recorrentes com `BackgroundService` + `PeriodicTimer` (sem Quartz no MVP).

### Consequências
- **Boa**, porque zero custo de licença e zero mágica: o endpoint chama o handler resolvido do DI.
- **Boa**, porque o .NET 10 tem suporte até 14/11/2028, cobrindo piloto e primeiros clientes.
- **Ruim**, porque os behaviors do pipeline (validação, transação) são nossos para manter; mitigado por testes unitários dos decorators.

### Confirmação (G3)
ARQ-06 no ArchUnitNET: nenhum assembly `Juker.*` referencia `MediatR`, `MassTransit` ou `FluentAssertions`. A versão do SDK fica fixada em `global.json`, verificado no G4.

## Prós e Contras das Opções
| Critério (peso) | Handlers próprios | Mediator (MIT) | MediatR 13 |
|---|---|---|---|
| Licença para produto comercial (5) | 5 | 5 | 2 |
| Simplicidade/menos dependências (4) | 5 | 4 | 3 |
| Familiaridade (padrão Milan) (3) | 4 | 4 | 5 |
| Performance/AOT (2) | 5 | 5 | 3 |
| **Total ponderado** | **69** | **65** | **47** |

## Mais Informações
- Política de suporte .NET — https://dotnet.microsoft.com/en-us/platform/support/policy/dotnet-core — .NET 10 LTS lançado em 11/11/2025, suporte até 14/11/2028; patch mais recente 10.0.12 (08/09/2026). Consultado em 2026-09-30.
- endoflife.date/dotnet — https://endoflife.date/dotnet (2026-09-30).
- Milan Jovanović, "MediatR and MassTransit Going Commercial" — https://milanjovanovic.tech/blog/mediatr-and-masstransit-going-commercial-what-this-means-for-you (2026-09-30).
- MediatR commercial alternatives (2026-09-09) — https://umamaheswaran.com/2026/09/09/mediatr-commercial-license-alternatives-migration/.
