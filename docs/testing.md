# Guia de testes do Juker

Como escrever e rodar testes no Juker usando o harness da SPEC-0003. As ferramentas estão no ADR-0011, a estratégia
de integração no ADR-0012 e as regras de arquitetura (ARQ-01..ARQ-08) na SPEC-0001, seção 3.

## Comandos

Rode na raiz do repositório. O runner é o Microsoft.Testing.Platform (`global.json`), então a sintaxe VSTest
(`dotnet test Juker.slnx --filter …`) não funciona.

| Gate | Comando |
|---|---|
| `test` (todos) | `dotnet test --solution Juker.slnx -c Release` |
| `test_integration` | `dotnet test --solution Juker.slnx -c Release --filter-trait "Category=Integration"` |
| `arch_test` (G3) | `dotnet test --project tests/Juker.ArchitectureTests -c Release` |
| Listar sem executar | acrescente `--list-tests` (e `--no-build` se já compilou) |
| Um teste | `dotnet test --project <projeto> --filter-method "*NomeDoMetodo*"` |

Os testes de integração precisam do Docker (a imagem `postgres:18` é baixada na primeira execução).

Um projeto de teste sem nenhum teste selecionado não é falha: o Microsoft.Testing.Platform termina com o código 8
nesse caso, e o `tests/Directory.Build.props` ignora esse código (`--ignore-exit-code 8`). É isso que permite
filtrar por categoria na solução inteira e manter projetos de UT ainda vazios. Falhas de teste continuam
terminando com código diferente de zero.

## Projetos

| Projeto | Nível | Conteúdo |
|---|---|---|
| `tests/Juker.Modules.<Módulo>.UnitTests` | Unit | handlers, domínio e validação do módulo, sem infraestrutura |
| `tests/Juker.BuildingBlocks.UnitTests` | Unit | `src/BuildingBlocks/*` |
| `tests/Juker.Web.UnitTests` | Unit | componentes Blazor com bUnit |
| `tests/Juker.IntegrationTests` | Integration | host real, Postgres real e HTTP externo gravado |
| `tests/Juker.ArchitectureTests` | Architecture/Unit | ARQ-01..ARQ-08, convenção de traits e política de tokens do design |
| `tests/Juker.TestKit` | — | fixtures compartilhadas de integração (biblioteca, sem testes) |

Os fixtures dos próprios testes do harness ficam fora da solução: `tests/Juker.ArchitectureTests/Fixtures/**` e
`tests/Juker.IntegrationTests/Fixtures/**`.

Ao criar um projeto de teste novo:

1. Adicione o projeto à `Juker.slnx`, na pasta `/tests/`.
2. Adicione um `ProjectReference` com `ReferenceOutputAssembly="false"` no `tests/Juker.ArchitectureTests/Juker.ArchitectureTests.csproj`.
   Sem isso, o `arch_test` não compila o projeto e a suíte de arquitetura falha dizendo qual referência falta.

## Convenções obrigatórias

Todo `[Fact]`/`[Theory]` tem, no método ou na classe:

```csharp
[Trait("Category", "Integration")]              // Unit | Integration | E2E | Smoke | Architecture
public sealed class QueueEndpointsTests(PostgresFixture db)
{
    [Fact]
    [Trait("Spec", "SPEC-0012:IT-03")]          // SPEC-NNNN:(CH|UT|IT|CT|E2E)-NN
    public async Task Pedido_entra_no_fim_da_fila() { … }
}
```

O `TestConventions` (UT-01) reflete todos os assemblies de teste da solução e falha listando os métodos fora da
convenção. Um teste pode ter mais de um `Spec`.

Também valem para testes:

- Nada de `Thread.Sleep`/`Task.Delay` (RS0030). Espere por uma condição, avance o `FakeTimeProvider` ou drene o
  outbox (`DrainOutboxAsync`, da SPEC-0004).
- Nada de retry de teste nem de dependência entre testes.
- Asserções com AwesomeAssertions. FluentAssertions, MediatR e MassTransit são proibidos (ARQ-06).
- Nada de EF Core InMemory/Sqlite nem mock de `HttpClient`/`HttpMessageHandler` (ARQ-08).

## Testes de integração com o `Juker.TestKit`

### `PostgresFixture`

É uma fixture de assembly (`[assembly: AssemblyFixture(typeof(PostgresFixture))]` em
`tests/Juker.IntegrationTests/Harness/AssemblyFixtures.cs`): **um container `postgres:18` por execução**, com senha
gerada a cada execução.

Na subida, a fixture cria o banco `juker_template` com:

1. as extensões `pg_trgm` e `unaccent`;
2. as migrations reais de todos os `DbContext` da solução (`Database.Migrate()`, nunca `EnsureCreated`).

Depois ela marca o modelo como template (sem conexões). Cada classe pede o próprio banco, clonado do modelo com
`CREATE DATABASE … TEMPLATE juker_template`. O clone leva menos de 1 s (na prática, dezenas de milissegundos):

```csharp
[Trait("Category", "Integration")]
public sealed class CatalogSearchTests(PostgresFixture db)
{
    [Fact]
    [Trait("Spec", "SPEC-0014:IT-01")]
    public async Task Busca_ignora_acentos()
    {
        var connectionString = await db.CreateDatabaseAsync();
        …
    }
}
```

As classes rodam em paralelo e os testes de uma mesma classe rodam em sequência. Cada teste cria os próprios
dados, com um bar (`VenueId`/slug) único. A base interna `TestDataBuilder<T>` do TestKit gera slugs únicos.

### Como a fixture encontra os `DbContext`

A fixture e o `MigrationsGuard` procuram subclasses concretas de `DbContext` nos assemblies `Juker.*` carregados
no processo e nos `Juker.*` que eles referenciam (a partir do `Juker.Host`, todos os módulos). Cada contexto é
instanciado, nesta ordem, por:

1. um `IDesignTimeDbContextFactory<TContext>` no mesmo assembly (**o jeito recomendado**: é o mesmo que o
   `dotnet ef migrations add` usa e carrega as opções do módulo);
2. um construtor que recebe `DbContextOptions<TContext>` (Npgsql);
3. um construtor sem parâmetros com `OnConfiguring`.

Cada módulo guarda o histórico das migrations no próprio schema
(`o.MigrationsHistoryTable("__EFMigrationsHistory", "<schema do módulo>")`), para que os módulos não disputem a
mesma tabela no `juker_template`.

### `MigrationsGuard`

`MigrationsGuard.AssertNoPendingModelChanges()` falha se algum `DbContext` tiver mudança no modelo sem migration
(`HasPendingModelChanges`). A mensagem cita cada contexto. O `SPEC-0003:IT-05` roda o guard para a solução. Se
ele falhar, crie a migration no projeto do módulo:

```bash
dotnet ef migrations add <Nome> --project src/Modules/<Módulo>/Juker.Modules.<Módulo>
```

### `JukerApiFactory`

É um `WebApplicationFactory<Program>` com o host real:

- o banco da factory é clonado do `juker_template` quando o host é construído (`ConnectionStrings:juker`);
- o ambiente é `Testing`;
- o `TimeProvider` é substituído pelo `FakeTimeProvider` exposto em `factory.Time` (começa em 2026-01-01 12:00 UTC);
- `Outbox:DispatcherEnabled=false`, e os eventos são entregues pelo `DrainOutboxAsync` (SPEC-0004);
- os esquemas de autenticação são os reais (SPEC-0009). Não existe handler de teste que libere tudo.

```csharp
await using var factory = new JukerApiFactory(db);
factory.UseTestIssuers(services => { /* emissores de teste da SPEC-0009 */ });   // antes de criar o cliente
using var client = factory.CreateClient();

factory.Time.Advance(TimeSpan.FromMinutes(5));
```

`UseTestIssuers` registra os emissores que produzem credenciais válidas para os esquemas reais. Chame antes do
primeiro `CreateClient()` ou `Services`. Chamar depois lança `InvalidOperationException`.

### `FakeHttpServer`

É um servidor WireMock.Net que responde com as gravações de `tests/Juker.TestKit/Recordings/*.json`
(mapeamentos estáticos do WireMock, copiados para a saída dos testes). Aponte o cliente HTTP tipado para `BaseUrl`:

```csharp
using var youtube = new FakeHttpServer();
// configure o HttpClient tipado do Catalog com BaseAddress = youtube.BaseUrl
```

Uma requisição sem gravação correspondente recebe 404. Nenhum teste chama a API real.

Para regravar as respostas da YouTube Data API (quando o contrato mudar), use uma chave real, fora do CI:

```bash
YOUTUBE_API_KEY=... tools/recordings/refresh-youtube.sh
```

O script grava um mapeamento por chamada em `tests/Juker.TestKit/Recordings/` e remove a chave das URLs e das
respostas. Revise o diff antes de commitar.

## Suíte de arquitetura

Em `tests/Juker.ArchitectureTests`, com ArchUnitNET sobre os assemblies compilados da `Juker.slnx`
(`ArchitectureSubject.Solution()`: `src/**` como produção e `tests/**` como teste). Os fixtures e o AppHost do
Aspire ficam de fora.

| Regra | O que falha |
|---|---|
| ARQ-01 `ModuleBoundaries` | um módulo usando um tipo de outro módulo fora do `.Contracts` dele |
| ARQ-02 `ContractsAreLeaves` | um `.Contracts` usando código Juker que não seja `.Contracts` ou `Juker.SharedKernel` |
| ARQ-03 `DbContextOwnership` | `DbContext` fora de módulo, mais de um por módulo, ou o `DbContext` de um módulo usado por outro módulo ou pelo Web |
| ARQ-04 `DomainPurity` | um namespace `…Domain…` usando EF Core, ASP.NET Core, Npgsql ou `…Infrastructure…` |
| ARQ-05 `WebBoundaries` | `Juker.Web*` usando internos de módulo, `Juker.Infrastructure` ou `Juker.Host` |
| ARQ-06 `ForbiddenDependencies` | qualquer uso (produção ou teste) de MediatR, MassTransit ou FluentAssertions, em qualquer versão |
| ARQ-07 `MediaProviderIsolation` | tipo com `YouTube` no nome, ou dependência de um, fora de `Juker.Modules.Catalog.Infrastructure` e `Juker.Web.Client.Tv` |
| ARQ-08 `TestingPolicy` | teste usando EF Core InMemory/Sqlite, bibliotecas de mock HTTP ou `HttpMessageHandler` (mock ou subclasse) |

Cada regra tem dois testes. Um prova que ela detecta a violação do fixture `Juker.ArchitectureTests.Violations`, e o
outro prova que a solução real passa. Uma violação traz a regra, o tipo que viola e o motivo, citando a dependência.

### Política de tokens do design (ADR-0014, regra 7)

`DesignTokenPolicy` varre `.razor`, `.razor.css` e `.css` em `src/` (ignorando `bin/` e `obj/`). A política falha,
com arquivo e linha, quando encontra:

- cor literal (`#fff`, `#ff007a`, `rgb(`, `hsl(`, `oklch(`…);
- valor arbitrário do Tailwind com cor (`bg-[#fff]`);
- `font-family`.

A exceção é o arquivo de tokens `src/Web/Juker.Web/Styles/theme.css`. Cores e fontes vêm dos tokens do design
system do Stitch.
