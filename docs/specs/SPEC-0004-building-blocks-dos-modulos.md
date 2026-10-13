---
id: SPEC-0004
title: Building blocks dos módulos
tier: full
type: foundation
user_facing: false
status: implemented
created: 2026-09-30
parent: SPEC-0001
depends_on: [SPEC-0002]
consumes_contract: [SPEC-0003@2]
contract_version: 5
touches: [src/BuildingBlocks/**, src/Juker.Host/Program.cs, src/Juker.Host/Juker.Host.csproj, Juker.slnx, Directory.Packages.props, .config/dotnet-tools.json, tests/Juker.BuildingBlocks.UnitTests/**, tests/Juker.IntegrationTests/BuildingBlocks/**, tests/Juker.IntegrationTests/Juker.IntegrationTests.csproj, tests/Juker.ArchitectureTests/PublicApi/**, tests/Juker.ArchitectureTests/Juker.ArchitectureTests.csproj, tests/Juker.TestKit/Outbox/**, tests/Juker.TestKit/Juker.TestKit.csproj, tests/fixtures/TestModules.Outbox/**]
adrs: [ADR-0002, ADR-0004, ADR-0006, ADR-0008, ADR-0012]
external: []
size: M
approved_by: thomas
approved_at: 2026-09-30
---

# SPEC-0004 — Building blocks dos módulos

## 1. Visão Geral
Fornece aos módulos o "kit" comum: tipo `Result`/`Error`, handlers de comando e consulta com decorators (validação e logging; o handler de comando confirma a própria transação), mapeamento de erros para `ProblemDetails`, `DbContext` base com schema do módulo e filtro de tenant, outbox/inbox para eventos de integração e o dispatcher em segundo plano. Toda funcionalidade do MVP é escrita em cima disso.

## 2. Motivação & Escopo
**Motivação:** sem um kit comum, cada módulo resolveria de um jeito o tratamento de erro, a validação, o isolamento entre bares e a entrega de eventos, que são justamente as partes críticas para a justiça da fila e para o bloqueio de comanda.

**Objetivos (dentro do escopo):**
- `Juker.SharedKernel` (namespace `Juker.SharedKernel`): `Result`, `Result<T>`, `Error`, `ErrorType`, `VenueId`, `ITenantScoped`, `IIntegrationEvent` e `Entity<TId>`, que acumula os eventos de integração levantados pela entidade (`Raise`). Não existe `IDomainEvent` no MVP: todo evento levantado por uma entidade é um evento de integração publicado pelo outbox.
- `Juker.Infrastructure` (namespace `Juker.Infrastructure`): `ICommand<T>`/`IQuery<T>` e seus handlers; decorators de validação (FluentValidation) e logging (**internos**); o handler de comando confirma a transação com uma única chamada a `SaveChangesAsync` do `ModuleDbContext` do seu módulo, e o interceptor do outbox grava os eventos levantados nessa mesma transação (emenda v5); `ResultHttpExtensions` → `ProblemDetails` (RFC 9457) com `code` e, em validação, `errors` por campo.
- `ModuleDbContext`: schema do módulo, snake_case, tabela de histórico de migrations no schema do módulo, filtro global de tenant para `ITenantScoped` falhando fechado (`TenantNotResolvedException`) e as entidades de outbox/inbox do módulo.
- Montagem do kit: `AddJukerInfrastructure(IConfiguration)` (chamado pelo `Juker.Host`), `AddModuleDbContext<TContext>()` (registra o contexto do módulo com Npgsql e o inclui como fonte do outbox) e `AddModuleHandlers(Assembly)` (handlers, validators e consumidores de eventos).
- Outbox: um interceptor de `SaveChanges` grava, na mesma transação, os eventos levantados pelas entidades rastreadas em `<schema>.outbox_messages` (`type` = nome do tipo com o assembly; `payload` = JSON do System.Text.Json com as opções web). O `OutboxDispatcher` (`BackgroundService`, só registrado se `Outbox:DispatcherEnabled=true`) entrega aos consumidores com retry exponencial até `Outbox:MaxAttempts` (5); inbox por consumidor para idempotência.
- `IOutboxDrainer.DrainAsync()` entrega de forma síncrona tudo o que está pendente em todos os módulos; no TestKit, a extensão **interna** `DrainOutboxAsync()` para os IT (ADR-0012).
- `IIdGenerator` com Guid v7 a partir do `TimeProvider`, e `TimeProvider.System` como padrão (substituível nos testes).
- Um módulo de teste fora da convenção `Juker.*` (`tests/fixtures/TestModules.Outbox`, com migration real), para exercitar o kit sem ser descoberto pelo `DbContextDiscovery` do TestKit.
- Snapshot da API pública do `Juker.SharedKernel` e do `Juker.Infrastructure` (PublicApiGenerator 11.5.x): mudanças exigem emenda.

**Não-objetivos (fora do escopo):**
- Autenticação e resolução de tenant a partir de cookies ou tokens (SPEC-0009).
- Qualquer entidade de negócio.
- Broker externo (ADR-0006).

## 3. Dependências
- **Implementações necessárias:** SPEC-0002 — projetos `BuildingBlocks` e `Directory.Packages.props`.
- **Contratos consumidos:** SPEC-0003@2 — `PostgresFixture`, `JukerApiFactory` e convenções de tag para os testes desta spec (enquanto a SPEC-0003 não estiver implementada, os testes usam dublês conforme o contrato do TestKit).
- **Pré-requisitos externos:** FluentValidation (Apache-2.0), PublicApiGenerator 11.5.4 (MIT), `dotnet-ef` como ferramenta local em `.config/dotnet-tools.json` (gerar a migration do módulo de teste).

## 4. Decisão Arquitetural
**Contexto:** projeto novo; ADR-0002 (handlers próprios), ADR-0004 (schema por módulo), ADR-0006 (outbox/inbox), ADR-0008 (tenancy) e ADR-0012 (outbox determinístico nos IT).

**Decisão:** o kit vive em dois projetos de building blocks; os módulos herdam o `ModuleDbContext` e registram seus handlers e consumidores com `AddModuleHandlers(assembly)`.

**Justificativa:** concentra num lugar só as partes que, se implementadas de formas diferentes, quebrariam o isolamento entre bares ou a entrega de efeitos colaterais.

**Desvio do padrão existente:** Nenhum.

**Alternativas descartadas:** MediatR/MassTransit (licença, ADR-0002); `Hangfire` para o dispatcher (dependência extra sem necessidade no MVP).

**ADRs:** ADR-0002, ADR-0004, ADR-0006, ADR-0008, ADR-0012.

## 5. Requisitos Não-Funcionais
- **Desempenho e escala:** latência do dispatcher (gravação → entrega) p95 < 1 s com 100 mensagens pendentes — medida no IT-06.
- **Segurança:** consulta de entidade `ITenantScoped` sem tenant resolvido lança `TenantNotResolvedException` (fail closed) — UT-04.
- **Privacidade e dados pessoais:** payload do outbox não contém IP nem token; somente IDs e números de comanda — revisão G4.
- **Disponibilidade e resiliência:** consumidor que falha não bloqueia os demais; após 5 falhas, a mensagem fica `failed` com o erro registrado — IT-03.
- **Acessibilidade (UI):** N/A — sem telas.
- **Custo:** N/A.

## 6. Artefato A — Contrato
**Interface:** API pública de `Juker.SharedKernel` e `Juker.Infrastructure`, registro no host e esquema do outbox. Tudo o que não aparece aqui é `internal`.

```text
// ── Juker.SharedKernel (namespace Juker.SharedKernel) — sem dependências de outros projetos Juker ──
public enum ErrorType { Validation, NotFound, Conflict, Forbidden, BusinessRule, Unauthorized }
public sealed record Error(string Code, string Message, ErrorType Type)
  { public IReadOnlyDictionary<string, string[]>? Details { get; init; } }   // erros por campo (validação)
public class Result
  { protected Result(bool isSuccess, Error? error);
    public bool IsSuccess { get; } public bool IsFailure { get; } public Error? Error { get; }
    public static Result Success(); public static Result Failure(Error error); }
public sealed class Result<T> : Result
  { public T Value { get; }                          // InvalidOperationException se IsFailure
    public static Result<T> Success(T value); public static new Result<T> Failure(Error error); }
public readonly record struct VenueId(Guid Value);
public interface ITenantScoped { VenueId VenueId { get; } }
public interface IIntegrationEvent { Guid Id { get; } DateTimeOffset OccurredAt { get; } }
public abstract class Entity<TId> where TId : notnull
  { protected Entity(); protected Entity(TId id);
    public TId Id { get; protected init; }
    public IReadOnlyCollection<IIntegrationEvent> IntegrationEvents { get; }
    protected void Raise(IIntegrationEvent integrationEvent);
    public void ClearIntegrationEvents(); }

// ── Juker.Infrastructure (namespace Juker.Infrastructure) ──
public interface ICommand<TResult>; public interface IQuery<TResult>;
public interface ICommandHandler<in TCommand, TResult> where TCommand : ICommand<TResult>
  { Task<Result<TResult>> Handle(TCommand command, CancellationToken ct); }
public interface IQueryHandler<in TQuery, TResult> where TQuery : IQuery<TResult>
  { Task<Result<TResult>> Handle(TQuery query, CancellationToken ct); }
public interface ITenantContext { VenueId? VenueId { get; } }
public sealed class FixedTenantContext(VenueId? venueId) : ITenantContext      // jobs e testes
public sealed class TenantNotResolvedException : InvalidOperationException     // (), (string), (string, Exception)
public abstract class ModuleDbContext : DbContext
  { protected ModuleDbContext(DbContextOptions options, ITenantContext tenant, string schema);
    protected string Schema { get; }
    protected override void OnModelCreating(ModelBuilder modelBuilder);          // derivados chamam base
    protected override void ConfigureConventions(ModelConfigurationBuilder b); } // snake_case
public interface IIntegrationEventHandler<in TEvent> where TEvent : IIntegrationEvent
  { Task Handle(TEvent integrationEvent, CancellationToken ct); }
public interface IOutboxDrainer { Task<int> DrainAsync(CancellationToken ct); } // quantas entregou
public interface IIdGenerator { Guid NewId(); }
public sealed class OutboxOptions
  { public bool DispatcherEnabled { get; set; } = true; public int IntervalMs { get; set; } = 500;
    public int MaxAttempts { get; set; } = 5; }                                   // seção "Outbox"
public static class InfrastructureRegistration
  { IServiceCollection AddJukerInfrastructure(this IServiceCollection s, IConfiguration c);
      // TryAdd TimeProvider.System; IIdGenerator (Guid v7); OutboxOptions; IOutboxDrainer;
      // OutboxDispatcher como hosted service só se DispatcherEnabled; decorators
    IServiceCollection AddModuleDbContext<TContext>(this IServiceCollection s, string connectionName = "juker")
      where TContext : ModuleDbContext;   // Npgsql + interceptor do outbox + histórico de migrations no schema
    IServiceCollection AddModuleHandlers(this IServiceCollection s, Assembly moduleAssembly); }
      // command/query handlers (com decorators), validators e IIntegrationEventHandler<T>
public static class ResultHttpExtensions
  { IResult ToHttpResult(this Result r);                              // sucesso → 204
    IResult ToHttpResult<T>(this Result<T> r, Func<T, IResult> onSuccess); }

// ── Juker.Host ── Program.cs chama builder.Services.AddJukerInfrastructure(builder.Configuration)
// ── TestKit (interno, InternalsVisibleTo Juker.IntegrationTests) ──
internal static Task<int> DrainOutboxAsync(this JukerApiFactory f)   // resolve IOutboxDrainer, drena até vazio e retorna o total entregue

Mapeamento ErrorType → HTTP (ProblemDetails, application/problem+json, com "code"):
  Validation → 400 (+ "errors": Error.Details) · Unauthorized → 401 · Forbidden → 403
  NotFound → 404 · Conflict → 409 · BusinessRule → 422

Tabelas por módulo (<schema>):
  outbox_messages(id uuid pk, type text, payload jsonb, occurred_at timestamptz,
                  status text check in ('pending','processed','failed'), attempts int,
                  next_attempt_at timestamptz, processed_at timestamptz null, last_error text null)
    type = TypeName + ", " + AssemblyName do evento · payload = System.Text.Json (JsonSerializerDefaults.Web)
  inbox_messages(message_id uuid, consumer text, processed_at timestamptz, pk(message_id, consumer))
    consumer = nome completo do tipo do IIntegrationEventHandler
Retry: next_attempt_at = agora + 2^(attempts-1) s (1, 2, 4, 8 s); attempts = MaxAttempts → status 'failed'
```

**Arquivos/módulos afetados:** ver `touches` no frontmatter. O módulo de teste `tests/fixtures/TestModules.Outbox` (assembly `TestModules.Outbox`, schema `outbox_test`, com `IDesignTimeDbContextFactory` e migration real) fica fora da solução e é referenciado só pelo `Juker.IntegrationTests`. Por não começar com `Juker.`, não é descoberto pelo `DbContextDiscovery`/`MigrationsGuard` do TestKit.

### 6.1 Mapa de Comportamentos
| Cenário | Condição / Entrada | Resultado esperado | Testes |
|---|---|---|---|
| Resultado | `Result<int>.Success(1)` / `Result<int>.Failure(e)` | `IsSuccess`/`IsFailure` coerentes; `Value` de falha lança `InvalidOperationException` | UT-01 |
| Comando inválido | validator com erro no campo `name` | handler **não** executa; `ErrorType.Validation` com `Details["name"]` | UT-02 |
| Comando válido | validator sem erros | handler executa e confirma a transação com um único `SaveChangesAsync` | UT-02, IT-01 |
| Erro vira HTTP | `Result` com cada `ErrorType` | 400/401/403/404/409/422 com `code`; validação com `errors` | UT-03 |
| Sem tenant | consulta a `ITenantScoped` com `FixedTenantContext(null)` | `TenantNotResolvedException` antes de abrir conexão | UT-04 |
| Evento na mesma transação | entidade levanta evento; commit / rollback | linha + mensagem `pending` no outbox / nenhuma das duas | IT-01 |
| Exatamente uma vez por consumidor | consumidor A falha uma vez; mensagem reprocessada; host parado e religado no meio | A e B executam exatamente 1 vez cada (inbox) | IT-02 |
| Consumidor sempre falha | 5 tentativas com backoff (FakeTimeProvider) | status `failed` com `last_error`; outra mensagem entregue normalmente | IT-03 |
| Isolamento entre bares | linhas do bar A e B; contexto do bar A | só as do A | IT-04 |
| Entrega sob comando | `DispatcherEnabled=false`, 3 pendentes | nada entregue sozinho; `DrainOutboxAsync` entrega 3; depois retorna 0 | IT-05 |
| Latência do dispatcher | 100 pendentes, dispatcher ligado | p95 gravação → entrega < 1 s | IT-06 |
| Kit montado no host | `AddJukerInfrastructure` no host real | `IOutboxDrainer`, `IIdGenerator` e `TimeProvider` resolvem; dispatcher registrado só com a opção ligada | IT-05 |
| API pública estável | alteração em tipo público do kit | snapshot diverge e o teste falha | CT-01 |

## 7. Artefato B — Plano de Testes (TDD)

### 7.1 Testes de Caracterização
- N/A — projeto novo.

### 7.2 Testes Unitários
- **UT-01** — Dado `Result<int>.Failure(error)`, quando `Value` é acessado, então lança `InvalidOperationException`; `Result<int>.Success(1)` tem `IsSuccess` verdadeiro e `IsFailure` falso; `Result.Failure(e).Error` é `e`.
- **UT-02** — Dado um comando cujo validator falha no campo `Name`, quando despachado pelo pipeline de decorators montado por `AddModuleHandlers`, então o handler não é chamado e o resultado é `ErrorType.Validation` com `Details["name"]`; com validator sem erros, o handler é chamado uma vez.
- **UT-03** — Dado um `Result` com cada `ErrorType`, quando `ToHttpResult` é chamado, então o status e o `code` do `ProblemDetails` seguem a tabela do contrato, e a validação traz `errors`; `Result.Success().ToHttpResult()` é 204.
- **UT-04** — Dado um `ModuleDbContext` de teste configurado com Npgsql e uma connection string de placeholder, com `FixedTenantContext(null)`, quando uma entidade `ITenantScoped` é consultada, então lança `TenantNotResolvedException` antes de qualquer conexão ser aberta.

### 7.3 Testes de Integração
<!-- Todos usam o módulo de teste TestModules.Outbox (migration aplicada no banco da classe) e o JukerApiFactory/PostgresFixture. -->
- **IT-01** — Com Postgres real: um handler de comando grava uma entidade que levanta um evento; com commit, a linha e uma mensagem `pending` no outbox existem (com `type` e `payload` do contrato); com a transação revertida, nenhuma das duas existe.
- **IT-02** — Com dois consumidores do mesmo evento, o consumidor A falha na primeira entrega: após o reprocessamento, A executa 1 vez com sucesso e B continua com exatamente 1 execução (inbox); com o host parado e religado entre as entregas, nenhuma execução se repete.
- **IT-03** — Um consumidor que sempre lança exceção leva a mensagem a `failed` após 5 tentativas, respeitando o backoff (1, 2, 4, 8 s via `FakeTimeProvider`), com `last_error` preenchido; outra mensagem pendente é entregue normalmente.
- **IT-04** — Com linhas de dois `VenueId` numa tabela `ITenantScoped`, o contexto do bar A retorna somente as linhas do A.
- **IT-05** — Com o host real montado por `AddJukerInfrastructure` e `Outbox:DispatcherEnabled=false`: `IOutboxDrainer`, `IIdGenerator` e `TimeProvider` resolvem e o `OutboxDispatcher` não está entre os hosted services; 3 mensagens pendentes não são entregues após 2 s de relógio avançado; `DrainOutboxAsync()` entrega as 3 e retorna 3; a chamada seguinte retorna 0.
- **IT-06** — Com 100 mensagens pendentes e o dispatcher ligado (intervalo de 500 ms), o p95 entre a gravação e a entrega ao consumidor fica abaixo de 1 s (medido com relógio real, sem `sleep`: espera por condição).

### 7.4 Testes de Contrato
- **CT-01** — O snapshot da API pública (PublicApiGenerator, `IncludeAssemblyAttributes=false`) de `Juker.SharedKernel` e `Juker.Infrastructure` é igual ao aprovado em `tests/Juker.ArchitectureTests/PublicApi/*.approved.txt`, escrito a partir deste contrato; as specs do MVP consomem esse contrato.

### 7.5 Testes E2E
- N/A — building blocks internos (Result, handlers, DbContext, outbox/inbox); nenhuma tela ou API pública muda.

### 7.6 Outros
- N/A.

**Dublês e dados de teste:** módulo de teste `TestModules.Outbox` (entidade `ITenantScoped` que levanta eventos, dois consumidores configuráveis para falhar, migration real no schema `outbox_test`); `FixedTenantContext`; `FakeTimeProvider`.

**Ambiente de execução:** Testcontainers (Postgres 18) local e no CI.

## 8. Plano de Rollout
- **Estratégia:** deploy direto.
- **Dados/schema:** tabelas de outbox/inbox criadas pelas migrations de cada módulo que usar eventos, a partir da SPEC-0007.
- **Compatibilidade:** mudanças na API pública exigem emenda (CT-01).
- **Observabilidade:** métricas `juker.outbox.pending`, `juker.outbox.failed` e log de erro por mensagem que falha.
- **Rollback:** reverter o commit.
- **Etapas de migração/coexistência:** N/A.

## 9. Questões em Aberto
Nenhuma.

## 10. Aprovação (H1)
Registrada no frontmatter (`approved_by`, `approved_at`) somente depois que o humano responder "Aprovado". O arquiteto nunca aprova a própria spec.

## 11. Checklist de Implementação

**Fase 1: Regras e componentes (unitário)**
- [x] Red: escrever UT-01, UT-02, UT-03, UT-04 com a tag `SPEC-0004:<ID>` e confirmar que falham pelo motivo certo
- [x] Green: implementar o mínimo para passar, seguindo os ADRs de fundação e o padrão de slices
- [x] Refactor mantendo tudo verde
- [x] Validar: build + lint + suíte de UT (G2)

**Fase 2: Integração e contrato**
- [x] Red: escrever CT-01, IT-01, IT-02, IT-03, IT-04, IT-05, IT-06 com a tag `SPEC-0004:<ID>` (Postgres real, módulo de teste TestModules.Outbox, `DrainOutboxAsync` — ADR-0012) e confirmar que falham pelo motivo certo
- [x] Green: implementar persistência, outbox/inbox, dispatcher, registro no host e o módulo de teste até passar
- [x] Refactor mantendo tudo verde
- [x] Validar: build + suíte completa + arquitetura (G2/G3)

**Fase final: Integração, entrega e documentação**
- [x] Review independente (G4)
- [x] Integração + CI verde (G5) e aprovação (H2)
- [x] Deploy via pipeline com smoke/E2E no ambiente (G6) — N/A: CD adiado (SPEC-0006)
- [x] Relatório de Entrega, docs raiz e CHANGELOG (G7)

## 12. Registro de Gates
| Gate | Status | Evidência | Data |
|---|---|---|---|
| G0 Spec | PASS | `spec_graph.py validate SPEC-0004` → 0 erros após a emenda v4 (contrato completo); checklist de julgamento do G0 revisado pelo Architect | 2026-10-01 |
| G1 Red | PASS | `verify SPEC-0004 --base sdd/SPEC-0001` no worktree (HEAD 7f28318): red antes do green PASS, commits red só alteram testes PASS, rastreabilidade 11/11 PASS; `dotnet test --solution` → total 65, failed 24 (NotImplementedException do scaffold / host sem `AddJukerInfrastructure`), succeeded 41 (inclui a guarda CT-01, snapshot escrito a partir do contrato) | 2026-10-01 |
| G2 Green | PASS | Executado no worktree (HEAD 4d450a0): build `-warnaserror` 0/0; `dotnet test --solution Juker.slnx -c Release` → 65/65; `--filter-trait "Category=Integration"` → 24/24; `dotnet test --project tests/Juker.BuildingBlocks.UnitTests -c Release` → 16/16; `dotnet format Juker.slnx --verify-no-changes` e fixture exit 0. | 2026-10-06 |
| G3 Arquitetura | PASS | `dotnet test --project tests/Juker.ArchitectureTests -c Release` → 22/22 (CT-01 e ARQ-01..ARQ-08 passando, nenhuma violação); `verify SPEC-0004` no worktree com 102/102 arquivos em touches. | 2026-10-06 |
| G4 Review | PASS | Reviewer independente: APPROVED em 4d450a0 (verify PASS com 0 falhas; build Release -warnaserror 0/0; 65/65 testes na solução; integração 24/24; arquitetura 22/22; lint ok; snapshot de API pública CT-01 100% aderente; justificadas alterações em OutboxDrainExtensions e migrations do fixture de teste). | 2026-10-06 |
| G5 Integração & CI | PASS | Merge --no-ff em sdd/SPEC-0001 (475d61f). No resultado integrado: build -warnaserror 0/0; dotnet test --solution 65/65; integração 24/24; arch_test 22/22; lint ok; nenhum pacote vulnerável; validate --exclude-drafts PASS. CI: N/A até a SPEC-0005 | 2026-10-06 |
| H2 Integração aprovada | PASS | aprovado por thomas | 2026-10-13 |
| G6 Deploy | N/A | CD adiado por decisão do fundador; publicação na VPS fica para o fim (SPEC-0006, IMP-01); entrega = PR da sdd/SPEC-0001 na main com a suíte verde | 2026-10-13 |
| G7 Pronto & Docs | PASS | Relatório de Entrega e Definição de Pronto: ok — f04af98 | 2026-10-13 |

## 13. Registro de Impedimentos
| ID | Aberto em | Fase/Gate | Tipo | Descrição | Tentativas | Responsável | Resolução | Fechado em |
|---|---|---|---|---|---|---|---|---|
| IMP-01 | 2026-09-30 | G1 | spec | Contrato sem: mecanismo de levantar evento para o outbox (tipo/payload), montagem do kit (AddJukerInfrastructure/registro do ModuleDbContext como fonte do outbox, host fora do touches), forma exata da API pública para o snapshot CT-01 (exceção de tenant, ITenantContext explícito, Entity, IdGenerator, Result, membros protegidos do ModuleDbContext, visibilidade de decorators/opções); touches sem TestKit.csproj/ArchitectureTests.csproj; DbContext de teste conflita com a descoberta do TestKit; IT-02 mistura exatamente-uma-vez com latência | Test-writer leu spec (v2/v3), testing.md, ADRs, TestKit e specs consumidoras; nenhum teste escrito | Architect (emenda v4) + thomas (aprovação, sob autorização prévia) | Emenda v4 — contrato completo da API pública, montagem do kit, esquema do outbox, módulo de teste fora de Juker.*, IT-02/IT-06 separados (autorização prévia de thomas, confirmada por thomas no chat em 2026-10-13) | 2026-09-30 |
| IMP-02 | 2026-10-13 | G7 | spec | O contrato (seção 6, objetivos e Mapa: 'unidade de trabalho confirma') promete um decorator interno de unidade de trabalho no pipeline de comandos, mas ele não foi implementado: só existem os decorators de validação e logging, e os handlers chamam SaveChangesAsync por conta própria (tests/fixtures/TestModules.Outbox/CreateTestItemHandler.cs). Nenhum teste distingue os dois comportamentos, e o G4 não apontou. | Conferência do Relatório de Entrega contra src/BuildingBlocks (Pipeline/ só tem Validation* e Logging*) e o fixture de teste | thomas (decidir: emenda v5 removendo o decorator do contrato, ou spec nova que o implementa) + Architect | Emenda v5 — sem decorator de unidade de trabalho; o handler de comando confirma a própria transação (thomas, 2026-10-13) | 2026-10-13 |

## 14. Relatório de Entrega

### O que foi entregue
O kit comum dos módulos. `Juker.SharedKernel`: `Result`/`Result<T>`, `Error`/`ErrorType`, `VenueId`, `ITenantScoped`, `IIntegrationEvent` e `Entity<TId>` com `Raise`. Novo projeto `Juker.Infrastructure`: handlers de comando e consulta com decorators de validação (FluentValidation) e logging estruturado (`AddModuleHandlers`), com o handler de comando confirmando a própria transação (contrato v5); `ToHttpResult` mapeando cada `ErrorType` para `ProblemDetails`; `ModuleDbContext` com schema do módulo, filtro de tenant obrigatório e captura dos eventos levantados para o outbox na mesma transação; outbox/inbox (`outbox_messages`, `inbox_messages`) com entrega exatamente uma vez por consumidor, backoff 1/2/4/8 s e estado `failed`; `OutboxDispatcher` em segundo plano, ligado por `Outbox:DispatcherEnabled`; `AddJukerInfrastructure` e `AddModuleDbContext<T>` chamados pelo host; `DrainOutboxAsync` (interno) para os testes. O módulo de teste `tests/fixtures/TestModules.Outbox` (schema `outbox_test`, migration real) exercita tudo com Postgres real.

### Como foi feito
- Commits: scaffold 14ae37b (chore), red 7f28318, green 4d450a0; merge --no-ff em `sdd/SPEC-0001` 475d61f.
- Emendas: contrato v5 (sem decorator de unidade de trabalho; o handler confirma a própria transação — fechou o IMP-02, aprovada por thomas em 2026-10-13), v2 (`IIntegrationEvent` no SharedKernel, para o ARQ-02 estrito), v3 (`DrainOutboxAsync` interno, projeto `Juker.Infrastructure`), v4 (contrato completo da API pública, montagem do kit, esquema do outbox, IT-02/IT-06 separados — fechou o IMP-01) e escopo (`DrainOutboxAsync` retorna `Task<int>`). Todas confirmadas por thomas no chat em 2026-10-13.
- Decisões do Implementer aceitas no review (G4): ajuste em `OutboxDrainExtensions` do TestKit e migrations do fixture de teste geradas pelo `dotnet-ef`.
- Padrão seguido: ADR-0002 (handlers próprios, sem MediatR), ADR-0004 (schema por módulo), ADR-0006 (outbox/inbox in-process), ADR-0008 (tenant por `VenueId`), ADR-0012 (Postgres real e outbox determinístico nos testes).

### Prova de Correção
N/A — type: foundation.

### Verificação
| Teste | Comportamento | Resultado | Evidência |
|---|---|---|---|
| UT-01 | `Result`/`Result<T>` coerentes; `Value` de falha lança `InvalidOperationException` | PASS | `Outcomes/ResultTests.cs`; `dotnet test --solution Juker.slnx -c Release` no resultado integrado a21a3e0 (65/65, duas execuções em 2026-10-13) |
| UT-02 | Validator com erro barra o handler (`ErrorType.Validation` com `Details["name"]`); válido executa uma vez | PASS | `Pipeline/CommandPipelineTests.cs`; `dotnet test --solution Juker.slnx -c Release` no resultado integrado a21a3e0 (65/65, duas execuções em 2026-10-13) |
| UT-03 | Cada `ErrorType` vira o status e o `code` do contrato; validação traz `errors`; sucesso é 204 | PASS | `Http/ResultHttpExtensionsTests.cs`; `dotnet test --solution Juker.slnx -c Release` no resultado integrado a21a3e0 (65/65, duas execuções em 2026-10-13) |
| UT-04 | Sem tenant, consulta a `ITenantScoped` lança `TenantNotResolvedException` antes de abrir conexão | PASS | `Tenancy/TenantFilterTests.cs`; `dotnet test --solution Juker.slnx -c Release` no resultado integrado a21a3e0 (65/65, duas execuções em 2026-10-13) |
| IT-01 | Entidade + mensagem `pending` no outbox na mesma transação; rollback não grava nenhuma das duas | PASS | `BuildingBlocks/OutboxTransactionTests.cs`; `dotnet test --solution Juker.slnx -c Release` no resultado integrado a21a3e0 (65/65, duas execuções em 2026-10-13); integração 24/24 |
| IT-02 | Exatamente uma vez por consumidor (inbox), inclusive com host religado | PASS | `BuildingBlocks/OutboxExactlyOnceTests.cs`; `dotnet test --solution Juker.slnx -c Release` no resultado integrado a21a3e0 (65/65, duas execuções em 2026-10-13); integração 24/24 |
| IT-03 | 5 tentativas com backoff 1/2/4/8 s → `failed` com `last_error`; outra mensagem segue | PASS | `BuildingBlocks/OutboxRetryTests.cs`; `dotnet test --solution Juker.slnx -c Release` no resultado integrado a21a3e0 (65/65, duas execuções em 2026-10-13); integração 24/24 |
| IT-04 | Contexto do bar A só vê linhas do A | PASS | `BuildingBlocks/TenantIsolationTests.cs`; `dotnet test --solution Juker.slnx -c Release` no resultado integrado a21a3e0 (65/65, duas execuções em 2026-10-13); integração 24/24 |
| IT-05 | Kit montado no host; dispatcher desligado não entrega; `DrainOutboxAsync` entrega 3 e depois 0 | PASS | `BuildingBlocks/OutboxDrainTests.cs`; `dotnet test --solution Juker.slnx -c Release` no resultado integrado a21a3e0 (65/65, duas execuções em 2026-10-13); integração 24/24 |
| IT-06 | p95 gravação → entrega < 1 s com 100 mensagens e dispatcher ligado | PASS | `BuildingBlocks/OutboxLatencyTests.cs`; `dotnet test --solution Juker.slnx -c Release` no resultado integrado a21a3e0 (65/65, duas execuções em 2026-10-13); integração 24/24 |
| CT-01 | Snapshot da API pública de `Juker.SharedKernel` e `Juker.Infrastructure` igual ao aprovado | PASS | `PublicApi/PublicApiContractTests.cs`; `dotnet test --project tests/Juker.ArchitectureTests -c Release` em a21a3e0 (22/22) |

### Definição de Pronto
- [x] Todos os testes do plano passando e listados na Verificação
- [x] Todo comportamento do Mapa de Comportamentos coberto e verificado (contrato v5)
- [x] Suíte completa, arquitetura e CI verdes no resultado integrado (G5) — CI: N/A até a SPEC-0005
- [x] Review independente sem achados blocker/major (G4)
- [x] Padrão arquitetural existente mantido, ou desvio coberto por ADR aprovado
- [x] Requisitos não-funcionais medidos com evidência (ou N/A justificado) — p95 do dispatcher < 1 s verificado pelo IT-06
- [x] Disponível no ambiente-alvo via pipeline, com smoke/E2E passando no ambiente (G6) — N/A: CD adiado (SPEC-0006); entrega pelo PR da `sdd/SPEC-0001` na `main`
- [x] Observabilidade e rollback prontos conforme o Plano de Rollout — logs estruturados de falha por mensagem e do dispatcher entregues aqui; os gauges `juker.outbox.pending`/`failed` ficam com a SPEC-0008 (já no escopo dela); rollback = reverter o merge
- [x] Documentação raiz e CHANGELOG atualizados (G7)
- [x] Pendências registradas como novas specs (ou nenhuma)
- [x] Textos da interface em português do Brasil, sem conceitos fora do produto (contas, créditos, mesa, música ao vivo) — N/A: sem interface
- [x] Tokens visuais vindos do design system do Stitch (projeto 8904741919458823023), sem cores/fontes avulsas — N/A: sem interface

### Deploy
N/A — CD adiado (SPEC-0006). Integrado em `sdd/SPEC-0001`; entra na `main` pelo PR da onda.

### Pendências
- SPEC-0008 — gauges `juker.outbox.pending` e `juker.outbox.failed` (instrumentos já listados no `JukerMetrics` da spec).
- SPEC-0027 — `JukerAllowZeroTests` em `Juker.BuildingBlocks.UnitTests` (o filtro de integração esvazia o projeto).

## 15. Emendas
| Versão do contrato | Data | Mudança | Motivo | Specs impactadas | Aprovado por |
|---|---|---|---|---|---|
| 5 | 2026-10-13 | Sai o decorator de unidade de trabalho: o handler de comando confirma a transação com uma única chamada a `SaveChangesAsync` do `ModuleDbContext` do módulo; o interceptor do outbox continua gravando os eventos na mesma transação | IMP-02: o decorator nunca foi implementado; com um `DbContext` por módulo, um decorator genérico precisaria descobrir qual contexto salvar (a "mágica" que o ADR-0002 evita); handler que esquece de salvar é pego pelos IT de cada spec | Nenhuma muda: nenhuma spec consome o contrato com pin; dependentes (SPEC-0007+) só usam handlers que salvam explicitamente | thomas — no chat em 2026-10-13 ("A") |
| escopo | 2026-10-01 | `DrainOutboxAsync` (interno) retorna `Task<int>` com o total entregue | O IT-05 exige "retorna 3" e depois 0; é interno, o snapshot público não muda (apontado pelo Test-writer) | Nenhuma | thomas — autorização prévia no chat (2026-09-30), confirmada por thomas no chat em 2026-10-13 |
| 4 | 2026-10-01 | Contrato completo da API pública (SharedKernel e Infrastructure) com `Entity<TId>.Raise` e sem `IDomainEvent`; montagem do kit (`AddJukerInfrastructure`, `AddModuleDbContext<T>`, `AddModuleHandlers`) com o host no `touches`; esquema e formato do outbox (`type`, `payload`, `status`, backoff); módulo de teste `TestModules.Outbox` fora da convenção `Juker.*`; IT-02 dividido em exatamente-uma-vez (IT-02) e latência (IT-06); `touches` com TestKit, ArchitectureTests, IntegrationTests csproj, fixture e `dotnet-ef` | IMP-01 (SPEC_DEFECT do Test-writer: contrato vago demais para o snapshot CT-01 e para os IT) | Nenhuma implementada (os consumidores do kit são as specs do MVP) | thomas — autorização prévia no chat (2026-09-30), confirmada por thomas no chat em 2026-10-13 |
| 3 | 2026-10-01 | `DrainOutboxAsync` passa a `internal` (visível ao Juker.IntegrationTests); `touches` inclui `Juker.slnx` e `Directory.Packages.props` (novo projeto Juker.Infrastructure e pacotes) | Evitar mudar o snapshot público do TestKit aprovado na SPEC-0003 e permitir registrar o projeto novo | Nenhuma (nenhuma spec consome o contrato da SPEC-0004) | thomas — autorização prévia no chat (2026-09-30), confirmada por thomas no chat em 2026-10-13 |
| 2 | 2026-10-01 | `IIntegrationEvent` passa do `Juker.Infrastructure` para o `Juker.SharedKernel`; `IIntegrationEventHandler<T>` continua no Infrastructure | Review da SPEC-0003: ARQ-02 só permite que `.Contracts` referencie `SharedKernel` e outros `.Contracts`; eventos de integração vivem nos `.Contracts` e implementam `IIntegrationEvent` | Consumidores futuros do evento (SPECs 0011+) — nenhum implementado ainda | thomas — autorização prévia no chat (2026-09-30), confirmada por thomas no chat em 2026-10-13 |
