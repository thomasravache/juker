using Juker.Host;
using Juker.Host.Diagnostics;
using Juker.Host.Testing;
using Juker.Infrastructure;

var builder = WebApplication.CreateBuilder(args);

builder.AddServiceDefaults();

// Postgres do AppHost (connection string "juker"); inclui o health check usado por /health/ready.
builder.AddNpgsqlDataSource("juker");

builder.Services.AddJukerInfrastructure(builder.Configuration);
builder.Services.AddJukerModules(builder.Configuration);

// Suporte a E2E (relógio controlável e /_test/*): só no ambiente E2E, num único ponto (SPEC-0025, ADR-0013).
var e2e = builder.Environment.IsE2E();
if (e2e)
{
    builder.AddE2ETestingSupport();
}

var app = builder.Build();

app.MapDefaultEndpoints();
app.MapDiagnosticsEndpoints(app.Environment);
app.MapJukerModules();

if (e2e)
{
    app.MapTestEndpoints();
}

app.Run();
