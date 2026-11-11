using System.Text.Json.Serialization;
using Asp.Versioning;
using BodegaAbierta.Api.Http;
using BodegaAbierta.Api.Tenancy;
using BodegaAbierta.Application;
using BodegaAbierta.Application.Abstractions;
using BodegaAbierta.Infrastructure;
using BodegaAbierta.Infrastructure.Persistence;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;

var builder = WebApplication.CreateBuilder(args);

builder.Services
    .AddControllers(options =>
    {
        // FluentValidation is the single source of validation rules; don't let MVC add implicit
        // [Required] errors for non-nullable reference types with different keys and messages.
        options.SuppressImplicitRequiredAttributeForNonNullableReferenceTypes = true;
    })
    .AddJsonOptions(options => options.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter()));

builder.Services.AddProblemDetails();
builder.Services.AddExceptionHandler<ProblemDetailsExceptionHandler>();
builder.Services.AddSingleton(TimeProvider.System);

builder.Services
    .AddApiVersioning(options =>
    {
        options.DefaultApiVersion = new ApiVersion(1, 0);
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
builder.Services.AddApplication();
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
