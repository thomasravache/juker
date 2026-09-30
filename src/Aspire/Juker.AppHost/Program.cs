var builder = DistributedApplication.CreateBuilder(args);

// Senha do Postgres local: parâmetro secreto gerado pelo Aspire e guardado nos user secrets do AppHost.
// Nunca versionada (SPEC-0002, seção 5).
var postgresPassword = builder.AddParameter(
    "postgres-password",
    new GenerateParameterDefault { MinLength = 24, Special = false },
    secret: true,
    persist: true);

var postgres = builder.AddPostgres("postgres", password: postgresPassword)
    .WithImageTag("18");

var database = postgres.AddDatabase("juker");

builder.AddProject<Projects.Juker_Host>("juker-host")
    .WithReference(database)
    .WaitFor(database);

builder.Build().Run();
