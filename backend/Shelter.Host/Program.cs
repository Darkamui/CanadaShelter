using Shelter.BuildingBlocks.Logging;
using Shelter.BuildingBlocks.Persistence;
using Shelter.BuildingBlocks.Tenancy;
using Shelter.Host.Composition;
using Shelter.Host.Middleware;

var builder = WebApplication.CreateBuilder(args);

builder.Logging.ClearProviders();
builder.Logging.AddJsonConsole(options =>
{
    options.IncludeScopes = true;
    options.UseUtcTimestamp = true;
    options.TimestampFormat = "yyyy-MM-ddTHH:mm:ss.fffZ";
});
builder.Logging.AddShelterRedaction();

builder.Services.AddShelterProblemDetails();
builder.Services.AddOpenApi();
builder.Services.AddShelterTenancy(builder.Environment);
builder.Services.AddShelterPersistence();
builder.Services.AddShelterHealthChecks();
builder.Services.AddShelterJobHosting(builder.Configuration);
builder.Services.AddModules(builder.Configuration);

var app = builder.Build();

app.UseMiddleware<CorrelationIdMiddleware>();
app.UseExceptionHandler();
app.UseStatusCodePages();
app.UseMiddleware<TenantResolutionMiddleware>();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.MapShelterHealthChecks();
app.MapShelterJobDashboard();
app.MapModules();

app.Run();

/// <summary>Entry point; public so integration tests can use <c>WebApplicationFactory&lt;Program&gt;</c>.</summary>
public partial class Program;
