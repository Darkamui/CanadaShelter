using System.Reflection;
using Shelter.BuildingBlocks.Auditing;
using Shelter.BuildingBlocks.Communications;
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
builder.Services.AddShelterOpenApi();
builder.Services.AddShelterTenancy();
builder.Services.AddShelterAuthentication(builder.Configuration, builder.Environment);
builder.Services.AddShelterPersistence();
builder.Services.AddShelterHealthChecks();
builder.Services.AddShelterJobHosting(builder.Configuration);
builder.Services.AddShelterEmail(builder.Configuration);
builder.Services.AddModules(builder.Configuration);

var app = builder.Build();
app.WarnIfMfaBypassed();

// Fail at startup, not at the first personal-data write, when audit keys are not configured (ADR 0016).
// Skipped by the build-time OpenAPI export, which runs this file without secrets and serves no request.
if (Assembly.GetEntryAssembly()?.GetName().Name != "GetDocument.Insider")
{
    _ = app.Services.GetRequiredService<IKeyProvider>();
}

app.UseMiddleware<CorrelationIdMiddleware>();
app.UseExceptionHandler();
app.UseStatusCodePages();
app.UseAuthentication();
app.UseMiddleware<TenantResolutionMiddleware>();
app.UseAuthorization();

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
