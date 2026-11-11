using Asp.Versioning;
using BodegaAbierta.Api.Tenancy;
using BodegaAbierta.Application.Abstractions;
using BodegaAbierta.Infrastructure;
using BodegaAbierta.Infrastructure.Persistence;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();
builder.Services.AddProblemDetails();

builder.Services
    .AddApiVersioning(options =>
    {
        options.DefaultApiVersion = new ApiVersion(1, 0);
        options.AssumeDefaultVersionWhenUnspecified = true;
        options.ReportApiVersions = true;
        options.ApiVersionReader = new UrlSegmentApiVersionReader();
    })
    .AddMvc()
    .AddApiExplorer(options =>
    {
        options.GroupNameFormat = "'v'VVV";
        options.SubstituteApiVersionInUrl = true;
    })
    .AddOpenApi(); // one OpenAPI document per API version (v1, v2, ...)

builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<ITenantContext, HttpTenantContext>();
builder.Services.AddInfrastructure();

builder.Services.AddHealthChecks()
    .AddDbContextCheck<BodegaAbiertaDbContext>("database");

var app = builder.Build();

app.UseExceptionHandler();
app.UseStatusCodePages();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi().WithDocumentPerVersion();
}

app.MapControllers();

// /health runs every check (including SQL Server); /health/live only says the process is up.
app.MapHealthChecks("/health");
app.MapHealthChecks("/health/live", new HealthCheckOptions { Predicate = _ => false });

await app.RunAsync();

/// <summary>Entry point; public partial so WebApplicationFactory&lt;Program&gt; can reference it.</summary>
public partial class Program;
