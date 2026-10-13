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

O Microsoft.Testing.Platform termina com o código 8 quando nenhum teste roda num projeto. **Toda execução aceita
zero testes** (SPEC-0027): o `tests/Directory.Build.targets` acrescenta `--ignore-exit-code 8` a
`TestingPlatformCommandLineArguments` em todos os projetos de `tests/`. Assim, os filtros por categoria
(`test_integration`, `--filter-trait "Category=Unit"`) podem esvaziar os projetos que só têm testes de outra
categoria. Falhas de teste continuam terminando com código diferente de zero.

A opção `--ignore-exit-code` aparece uma única vez: repetida, a plataforma rejeita a execução com o código 5. Ela
aceita uma lista separada por ponto e vírgula (`--ignore-exit-code 2;8`), e o `tests/Directory.Build.targets`
garante o 8 sem repetir a opção:

- se o projeto já ignora o código 8 em `TestingPlatformCommandLineArguments`, nada muda;
- se o projeto já ignora outros códigos, o 8 entra na mesma lista (`--ignore-exit-code 2` vira `2;8`);
- senão, a opção é acrescentada com o 8.

Não passe `--ignore-exit-code` na linha de comando do `dotnet test`: somada à do projeto, ela fica repetida.

A proteção contra um projeto de teste que perdeu seus testes é estática. O teste de convenção
`TestProjectsConventionsTests` (SPEC-0027:UT-03, no `arch_test`) conta os `[Fact]`/`[Theory]` de cada projeto
`tests/**` da `Juker.slnx` e falha listando:

- o projeto **sem** `JukerAllowZeroTests` que não tem nenhum teste;
- o projeto com `<JukerAllowZeroTests>true</JukerAllowZeroTests>` que tem testes.

Ou seja, `JukerAllowZeroTests` só serve para os projetos de UT ainda vazios. A spec que acrescentar os primeiros
testes num deles remove a propriedade no mesmo commit. A propriedade não muda mais os argumentos do
`dotnet test`; o `tests/Directory.Build.props` só define o padrão (`false`). Para conferir os argumentos, rode
`dotnet msbuild <projeto> -getProperty:TestingPlatformCommandLineArguments`. O `tests/Directory.Build.targets`
também importa um `Directory.Build.targets` da raiz, se um dia existir.

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
3. Não use `JukerAllowZeroTests`, a menos que o projeto nasça sem testes. Nesse caso, remova a propriedade junto
   com os primeiros testes (o `arch_test` falha enquanto as duas coisas não baterem).

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
`CREATE DATABASE … TEMPLATE juker_template`. O clone leva menos de 1 s (na prática, dezenas de milissegundos).

A subida tem que ficar abaixo de 10 s com a imagem em cache (SPEC-0003, seção 5). A fixture registra o tempo numa
mensagem de diagnóstico do xUnit (`PostgresFixture: container postgres:18 pronto em … ms; juker_template … pronto
em … ms`). O terminal do `dotnet test` não mostra essa mensagem. Para ver o valor, gere o log de diagnóstico da
plataforma e procure por `PostgresFixture`:

```bash
dotnet test --project tests/Juker.IntegrationTests -c Release --diagnostic --diagnostic-verbosity Information \
  --diagnostic-output-directory artifacts/diag
grep -h PostgresFixture artifacts/diag/*.diag
```

Uso numa classe de teste:

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
   `dotnet ef migrations add` usa e carrega as opções do módulo). A fábrica precisa configurar o Npgsql com uma
   **connection string** (`UseNpgsql("Host=…")`), nunca com um `NpgsqlDataSource`. A fixture troca a conexão por
   `Database.SetConnectionString(...)` para migrar o `juker_template`, e isso não funciona quando o contexto foi
   montado sobre um data source;
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
| ARQ-02 `ContractsAreLeaves` | um `.Contracts` usando código Juker que não seja `Juker.SharedKernel` ou outro `.Contracts` (nem `Juker.Infrastructure`, nem internos de módulo). O `IIntegrationEvent` fica no `Juker.SharedKernel` (SPEC-0004, emendada) |
| ARQ-03 `DbContextOwnership` | `DbContext` fora de módulo, mais de um por módulo, ou o `DbContext` de um módulo usado por outro módulo ou pelo Web |
| ARQ-04 `DomainPurity` | um namespace `…Domain…` usando EF Core, ASP.NET Core, Npgsql ou `…Infrastructure…` |
| ARQ-05 `WebBoundaries` | `Juker.Web*` usando código Juker que não seja `.Contracts` ou `Juker.SharedKernel` (internos de módulo, `Juker.Infrastructure`, `Juker.Host`) |
| ARQ-06 `ForbiddenDependencies` | qualquer uso (produção ou teste) de MediatR, MassTransit ou FluentAssertions, em qualquer versão |
| ARQ-07 `MediaProviderIsolation` | tipo com `YouTube` no nome, ou dependência de um, fora de `Juker.Modules.Catalog.Infrastructure` e `Juker.Web.Client.Tv` |
| ARQ-08 `TestingPolicy` | teste usando EF Core InMemory/Sqlite ou bibliotecas de mock HTTP, uma subclasse de `HttpMessageHandler` escrita à mão, ou `HttpMessageHandler` como argumento genérico de biblioteca de mock (`Substitute.For<HttpMessageHandler>()`, `Mock<HttpMessageHandler>`). Usar o handler do TestServer (`server.CreateHandler()`) é permitido |

Cada regra tem dois testes. Um prova que ela detecta a violação do fixture `Juker.ArchitectureTests.Violations`, e o
outro prova que a solução real passa. Uma violação traz a regra, o tipo que viola e o motivo, citando a dependência.

### Política de tokens do design (ADR-0014, regra 7)

`DesignTokenPolicy` varre `.razor`, `.razor.css` e `.css` em `src/` (ignorando `bin/` e `obj/`). A política falha,
com arquivo e linha, quando encontra:

- cor literal (`#fff`, `#ff007a`, `rgb(`, `hsl(`, `oklch(`…), inclusive em atributos entre aspas (`fill="#ff007a"`,
  `stroke='#fff'`) e em parâmetros de componente (`Color="#ff007a"`). O valor de `href`, `xlink:href`, `src`,
  `action` e `formaction` (como `href="#add"`) é âncora, não cor;
- valor arbitrário do Tailwind com cor (`bg-[#fff]`);
- `font-family`.

A exceção é o arquivo de tokens `src/Web/Juker.Web/Styles/theme.css`. Cores e fontes vêm dos tokens do design
system do Stitch.

#### Navegação por fragmento em `.razor`

A política lê o arquivo como texto e não sabe se um trecho é C# ou marcação. Fora dos atributos de âncora, um
`#` depois de aspas, espaço, `(`, `=`, `:`, `[` ou `,` é tratado como cor quando o nome que vem a seguir (até o
primeiro caractere que não seja letra, dígito ou `_`) tem 3, 4, 6 ou 8 caracteres, todos hexadecimais, sem importar
maiúsculas (`#add`, `#ABC`, `#cafe-menu`). Por exemplo, `Navigation.NavigateTo("#add")` num bloco `@code` e `id="#abc"` falham. Para
navegar por fragmento sem acionar a política:

- prefira um link: `<a href="#add">` é âncora e passa;
- num bloco `@code`, monte o fragmento a partir de uma constante com o id, sem `#` seguido de letras no literal:
  `Navigation.NavigateTo($"#{AddSectionId}")`, com `private const string AddSectionId = "add";`;
- ou mova a lógica para o code-behind (`.razor.cs`), que a política não varre;
- ou escolha ids cujo começo não seja hexadecimal (`#adicionar`, `#secao-add`), lembrando que `#cafe-menu` ainda
  casa porque `cafe` é hexadecimal e o hífen fecha a palavra.

O `id` de um elemento não leva `#` (`id="add"`): o `#` só entra na referência ao fragmento.
