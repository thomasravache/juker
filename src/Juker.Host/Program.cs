using Juker.Host;
using Juker.Host.Diagnostics;
using Juker.Infrastructure;

var builder = WebApplication.CreateBuilder(args);

builder.AddServiceDefaults();

// Postgres do AppHost (connection string "juker"); inclui o health check usado por /health/ready.
builder.AddNpgsqlDataSource("juker");

builder.Services.AddJukerInfrastructure(builder.Configuration);
builder.Services.AddJukerModules(builder.Configuration);

var app = builder.Build();

app.MapDefaultEndpoints();
app.MapDiagnosticsEndpoints(app.Environment);
app.MapJukerModules();

app.Run();
