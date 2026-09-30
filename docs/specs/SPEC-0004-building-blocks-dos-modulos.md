---
id: SPEC-0004
title: Building blocks dos módulos
tier: full
type: foundation
user_facing: false
status: approved
created: 2026-09-30
parent: SPEC-0001
depends_on: [SPEC-0002]
consumes_contract: [SPEC-0003@2]
contract_version: 1
touches: [src/BuildingBlocks/**, tests/Juker.BuildingBlocks.UnitTests/**, tests/Juker.IntegrationTests/BuildingBlocks/**, tests/Juker.ArchitectureTests/PublicApi/**, tests/Juker.TestKit/Outbox/**]
adrs: [ADR-0002, ADR-0004, ADR-0006, ADR-0008, ADR-0012]
external: []
size: M
approved_by: thomas
approved_at: 2026-09-30
---

# SPEC-0004 — Building blocks dos módulos

## 1. Visão Geral
Fornece aos módulos o "kit" comum: tipo `Result`/`Error`, handlers de comando e consulta com decorators (validação, logging, unidade de trabalho), mapeamento de erros para `ProblemDetails`, `DbContext` base com schema do módulo e filtro de tenant, outbox/inbox para eventos de integração e o dispatcher em segundo plano. Toda funcionalidade do MVP é escrita em cima disso.

## 2. Motivação & Escopo
**Motivação:** sem um kit comum, cada módulo resolveria de um jeito o tratamento de erro, a validação, o isolamento entre bares e a entrega de eventos, que são justamente as partes críticas para a justiça da fila e para o bloqueio de comanda.

**Objetivos (dentro do escopo):**
- `Juker.SharedKernel`: `Result`, `Result<T>`, `Error(Code, Message, Type)` com `ErrorType` = Validation | NotFound | Conflict | Forbidden | BusinessRule | Unauthorized; `Entity<TId>`, `IDomainEvent`, `VenueId`.
- `Juker.Infrastructure`: `ICommandHandler<TCommand, TResult>`, `IQueryHandler<TQuery, TResult>`; decorators `ValidationDecorator` (FluentValidation), `LoggingDecorator` e `UnitOfWorkDecorator` (somente comandos); registro por scan do assembly do módulo.
- `ResultExtensions.ToHttpResult()` → `ProblemDetails` (RFC 9457) com `code` e, em validação, `errors` por campo.
- `ModuleDbContext` base: schema do módulo, convenção de nomes snake_case, filtro global `VenueId == ITenantContext.VenueId` para entidades `ITenantScoped`, falhando fechado sem tenant.
- `ITenantContext` (implementação resolvida por SPEC-0009 a partir do principal; nesta spec, uma implementação explícita para jobs e testes).
- Outbox por módulo (`<schema>.outbox_messages`), gravado por interceptor do EF na mesma transação; `OutboxDispatcher` (`BackgroundService`, intervalo de 500 ms configurável) com retry exponencial até 5 tentativas; inbox por consumidor (`<schema>.inbox_messages`) para idempotência; `IIntegrationEventHandler<TEvent>`.
- `IdGenerator` (Guid v7) e uso de `TimeProvider` em todo o kit.
- Dispatcher desligável por configuração (`Outbox:DispatcherEnabled`, padrão verdadeiro) e `IOutboxDrainer.DrainAsync()`, que entrega de forma síncrona tudo o que está pendente em todos os módulos; no `Juker.TestKit`, a extensão `JukerApiFactory.DrainOutboxAsync()` usada pelos IT (ADR-0012).
- Snapshot da API pública do `SharedKernel` e do `Infrastructure` (PublicApiGenerator) para que mudanças exijam emenda.

**Não-objetivos (fora do escopo):**
- Autenticação e resolução de tenant a partir de cookies ou tokens (SPEC-0009).
- Qualquer entidade de negócio.
- Broker externo (ADR-0006).

## 3. Dependências
- **Implementações necessárias:** SPEC-0002 — projetos `BuildingBlocks` e `Directory.Packages.props`.
- **Contratos consumidos:** SPEC-0003@2 — `PostgresFixture`, `JukerApiFactory` e convenções de tag para os testes desta spec (enquanto a SPEC-0003 não estiver implementada, os testes usam dublês conforme o contrato do TestKit).
- **Pré-requisitos externos:** FluentValidation (Apache-2.0), PublicApiGenerator (MIT).

## 4. Decisão Arquitetural
**Contexto:** projeto novo; ADR-0002 (handlers próprios), ADR-0004 (schema por módulo), ADR-0006 (outbox/inbox), ADR-0008 (tenancy) e ADR-0012 (outbox determinístico nos IT).

**Decisão:** o kit vive em dois projetos de building blocks; os módulos herdam o `ModuleDbContext` e registram seus handlers e consumidores com `AddModuleHandlers(assembly)`.

**Justificativa:** concentra num lugar só as partes que, se implementadas de formas diferentes, quebrariam o isolamento entre bares ou a entrega de efeitos colaterais.

**Desvio do padrão existente:** Nenhum.

**Alternativas descartadas:** MediatR/MassTransit (licença, ADR-0002); `Hangfire` para o dispatcher (dependência extra sem necessidade no MVP).

**ADRs:** ADR-0002, ADR-0004, ADR-0006, ADR-0008, ADR-0012.

## 5. Requisitos Não-Funcionais
- **Desempenho e escala:** latência do dispatcher (gravação → entrega) p95 < 1 s com 100 mensagens pendentes — medida no IT-02.
- **Segurança:** consulta de entidade `ITenantScoped` sem tenant resolvido lança `TenantNotResolvedException` (fail closed) — UT-04.
- **Privacidade e dados pessoais:** payload do outbox não contém IP nem token; somente IDs e números de comanda — revisão G4.
- **Disponibilidade e resiliência:** consumidor que falha não bloqueia os demais; após 5 falhas, a mensagem fica `failed` com o erro registrado — IT-03.
- **Acessibilidade (UI):** N/A — sem telas.
- **Custo:** N/A.

## 6. Artefato A — Contrato
**Interface:** API pública de `Juker.SharedKernel` e `Juker.Infrastructure`.

```text
// SharedKernel
public enum ErrorType { Validation, NotFound, Conflict, Forbidden, BusinessRule, Unauthorized }
public sealed record Error(string Code, string Message, ErrorType Type);
public class Result { bool IsSuccess; Error? Error; static Result Success(); static Result Failure(Error e); }
public class Result<T> : Result { T Value; }
public interface ITenantScoped { VenueId VenueId { get; } }
public readonly record struct VenueId(Guid Value);

// Infrastructure
public interface ICommand<TResult>; public interface IQuery<TResult>;
public interface ICommandHandler<TCommand, TResult> where TCommand : ICommand<TResult>
    { Task<Result<TResult>> Handle(TCommand command, CancellationToken ct); }
public interface IQueryHandler<TQuery, TResult>  (idem)
public interface ITenantContext { VenueId? VenueId { get; } }
public abstract class ModuleDbContext(DbContextOptions o, ITenantContext t, string schema) : DbContext
public interface IIntegrationEvent { Guid Id { get; } DateTimeOffset OccurredAt { get; } }
public interface IIntegrationEventHandler<TEvent> where TEvent : IIntegrationEvent
    { Task Handle(TEvent e, CancellationToken ct); }
public static class ModuleRegistration { IServiceCollection AddModuleHandlers(this IServiceCollection s, Assembly a); }
public interface IOutboxDrainer { Task<int> DrainAsync(CancellationToken ct); }   // retorna quantas mensagens entregou
Configuração: Outbox:DispatcherEnabled (bool, padrão true) · Outbox:IntervalMs (padrão 500)
TestKit: public static Task DrainOutboxAsync(this JukerApiFactory f)   // resolve IOutboxDrainer e drena até vazio
public static class ResultHttpExtensions { IResult ToHttpResult<T>(this Result<T> r, Func<T, IResult> onSuccess); }

Mapeamento ErrorType → HTTP (ProblemDetails, application/problem+json, com "code"):
  Validation → 400 (+ "errors": { campo: [mensagens] })
  Unauthorized → 401 · Forbidden → 403 · NotFound → 404 · Conflict → 409 · BusinessRule → 422

Tabelas por módulo (<schema>):
  outbox_messages(id uuid pk, type text, payload jsonb, occurred_at timestamptz,
                  processed_at timestamptz null, attempts int, last_error text null, status text)
  inbox_messages(message_id uuid, consumer text, processed_at timestamptz, pk(message_id, consumer))
```

**Arquivos/módulos afetados:** ver `touches` no frontmatter.

### 6.1 Mapa de Comportamentos
| Cenário | Condição / Entrada | Resultado esperado | Testes |
|---|---|---|---|
| Comando válido | validator sem erros | handler executa; `UnitOfWork` confirma a transação | UT-02, IT-01 |
| Comando inválido | validator com erros | handler **não** executa; `Result` com `ErrorType.Validation` e erros por campo | UT-02 |
| Erro de negócio vira HTTP | `Result` com cada `ErrorType` | status 400/401/403/404/409/422 com `ProblemDetails.code` | UT-03 |
| Sem tenant | consulta a entidade `ITenantScoped` sem `VenueId` | `TenantNotResolvedException` | UT-04 |
| Isolamento entre bares | linhas do bar A e do bar B; contexto do bar A | só as linhas do A retornam | IT-04 |
| Evento na mesma transação | transação com evento é revertida | nenhuma linha no outbox | IT-01 |
| Entrega única por consumidor | mensagem reprocessada após falha do dispatcher | cada consumidor executa exatamente 1 vez (inbox) | IT-02 |
| Consumidor com falha | consumidor lança exceção sempre | retry até 5 tentativas; status `failed`; outros consumidores e mensagens seguem | IT-03 |
| API pública estável | alteração em tipo público do kit | snapshot diverge e o teste falha | CT-01 |
| Entrega sob comando | dispatcher desligado, 3 mensagens pendentes | nada é entregue sozinho; `DrainOutboxAsync` entrega as 3 na hora e retorna 3; chamada seguinte retorna 0 | IT-05 |
| Resultado | `Result.Success`/`Failure` | `IsSuccess` coerente; acessar `Value` de falha lança `InvalidOperationException` | UT-01 |

## 7. Artefato B — Plano de Testes (TDD)

### 7.1 Testes de Caracterização
- N/A — projeto novo.

### 7.2 Testes Unitários
- **UT-01** — Dado `Result<int>.Failure(error)`, quando `Value` é acessado, então lança `InvalidOperationException`; e `Result<int>.Success(1).IsSuccess` é verdadeiro.
- **UT-02** — Dado um comando cujo validator falha no campo `Name`, quando despachado pelo pipeline de decorators, então o handler não é chamado e o resultado é `ErrorType.Validation` com `errors["name"]`.
- **UT-03** — Dado um `Result` com cada `ErrorType`, quando `ToHttpResult` é chamado, então o status e o `code` do `ProblemDetails` seguem a tabela do contrato.
- **UT-04** — Dado um `ModuleDbContext` com `ITenantContext.VenueId == null`, quando uma entidade `ITenantScoped` é consultada, então lança `TenantNotResolvedException`.

### 7.3 Testes de Integração
- **IT-01** — Com Postgres real, um handler de comando grava uma entidade e levanta um evento; com commit, a linha e a mensagem do outbox existem; com a transação revertida, nenhuma das duas existe.
- **IT-02** — Com 100 mensagens pendentes e dois consumidores, o dispatcher é interrompido no meio e reiniciado; cada consumidor processa cada mensagem exatamente uma vez, e o p95 gravação → entrega fica abaixo de 1 s.
- **IT-03** — Um consumidor que sempre lança exceção leva a mensagem a `failed` após 5 tentativas com backoff (tempo via `FakeTimeProvider`), enquanto outra mensagem é entregue normalmente.
- **IT-04** — Com linhas de dois `VenueId` numa tabela `ITenantScoped`, o contexto do bar A retorna somente as linhas do A.
- **IT-05** — Com `Outbox:DispatcherEnabled=false`, 3 mensagens pendentes não são entregues após 2 s de relógio avançado; `DrainOutboxAsync()` entrega as 3 aos consumidores e retorna 3; uma segunda chamada retorna 0.

### 7.4 Testes de Contrato
- **CT-01** — O snapshot da API pública (PublicApiGenerator) de `Juker.SharedKernel` e `Juker.Infrastructure` é igual ao aprovado em `tests/Juker.ArchitectureTests/PublicApi/*.approved.txt`; as specs do MVP consomem esse contrato.

### 7.5 Testes E2E
- N/A — user_facing: false.

### 7.6 Outros
- N/A.

**Dublês e dados de teste:** `TestModuleDbContext` com entidade `ITenantScoped` de teste; consumidores de teste configuráveis para falhar.

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
- [ ] Red: escrever UT-01, UT-02, UT-03, UT-04 com a tag `SPEC-0004:<ID>` e confirmar que falham pelo motivo certo
- [ ] Green: implementar o mínimo para passar, seguindo os ADRs de fundação e o padrão de slices
- [ ] Refactor mantendo tudo verde
- [ ] Validar: build + lint + suíte de UT (G2)

**Fase 2: Integração e contrato**
- [ ] Red: escrever IT-01, IT-02, IT-03, IT-04, IT-05, CT-01 com a tag `SPEC-0004:<ID>` (Postgres real, esquemas de auth reais, `DrainOutboxAsync`, WireMock — ADR-0012) e confirmar que falham pelo motivo certo
- [ ] Green: implementar endpoints, persistência, migrations e integrações até passar
- [ ] Refactor mantendo tudo verde
- [ ] Validar: build + suíte completa + arquitetura + cobertura do diff ≥ 80% (G2/G3)

**Fase final: Integração, entrega e documentação**
- [ ] Review independente (G4)
- [ ] Integração + CI verde (G5) e aprovação (H2)
- [ ] Deploy via pipeline com smoke/E2E no ambiente (G6)
- [ ] Relatório de Entrega, docs raiz e CHANGELOG (G7)

## 12. Registro de Gates
| Gate | Status | Evidência | Data |
|---|---|---|---|
| G0 Spec | PASS | `spec_graph.py validate SPEC-0004` → G0 [SPEC-0004]: PASS — 0 erro(s), 0 aviso(s); checklist de julgamento do G0 revisado pelo Architect (revisão ADR-0012/0013) | 2026-09-30 |
| G1 Red | PENDING | | |
| G2 Green | PENDING | | |
| G3 Arquitetura | PENDING | | |
| G4 Review | PENDING | | |
| G5 Integração & CI | PENDING | | |
| H2 Integração aprovada | PENDING | | |
| G6 Deploy | PENDING | | |
| G7 Pronto & Docs | PENDING | | |

## 13. Registro de Impedimentos
| ID | Aberto em | Fase/Gate | Tipo | Descrição | Tentativas | Responsável | Resolução | Fechado em |
|---|---|---|---|---|---|---|---|---|

## 14. Relatório de Entrega

### O que foi entregue

### Como foi feito

### Prova de Correção

### Verificação
| Teste | Comportamento | Resultado | Evidência |
|---|---|---|---|

### Definição de Pronto
- [ ] Todos os testes do plano passando e listados na Verificação
- [ ] Todo comportamento do Mapa de Comportamentos coberto e verificado
- [ ] Suíte completa, arquitetura e CI verdes no resultado integrado (G5)
- [ ] Review independente sem achados blocker/major (G4)
- [ ] Padrão arquitetural existente mantido, ou desvio coberto por ADR aprovado
- [ ] Requisitos não-funcionais medidos com evidência (ou N/A justificado)
- [ ] Disponível no ambiente-alvo via pipeline, com smoke/E2E passando no ambiente (G6)
- [ ] Observabilidade e rollback prontos conforme o Plano de Rollout
- [ ] Documentação raiz e CHANGELOG atualizados (G7)
- [ ] Pendências registradas como novas specs (ou nenhuma)
- [ ] Textos da interface em português do Brasil, sem conceitos fora do produto (contas, créditos, mesa, música ao vivo)
- [ ] Tokens visuais vindos do design system do Stitch (projeto 8904741919458823023), sem cores/fontes avulsas

### Deploy

### Pendências

## 15. Emendas
| Versão do contrato | Data | Mudança | Motivo | Specs impactadas | Aprovado por |
|---|---|---|---|---|---|
