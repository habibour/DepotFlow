using DepotFlow.Api.Auth;
using DepotFlow.Api.ErrorHandling;
using DepotFlow.Api.Health;
using DepotFlow.Api.Logging;
using System.Text.Json.Serialization;
using DepotFlow.Application;
using DepotFlow.Api.OpenApi;
using DepotFlow.Infrastructure;
using DepotFlow.Infrastructure.Persistence;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers()
    .AddJsonOptions(o => o.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter()));   // "InYard", not 1
builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<ICurrentUser, CurrentUser>();
builder.Services.AddApplication();
builder.Services.AddInfrastructure(builder.Configuration);
builder.Services.AddJwtAuthentication();
builder.Services.AddHealthChecks().AddCheck<DatabaseHealthCheck>("database");
builder.Services.AddProblemDetails();
builder.Services.AddExceptionHandler<GlobalExceptionHandler>();
builder.Services.AddOpenApi(options => options.AddDocumentTransformer<BearerSecurityTransformer>());

var app = builder.Build();

// Behaviour that a container needs in any environment is switched by configuration, not by "Development":
//   Database:MigrateOnStartup  apply migrations and seed users, shipping lines and the yard when the API starts
//   Swagger:Enabled            serve the OpenAPI document and Swagger UI
// Development turns both on.
var migrateOnStartup = app.Environment.IsDevelopment() || app.Configuration.GetValue<bool>("Database:MigrateOnStartup");
var swaggerEnabled = app.Environment.IsDevelopment() || app.Configuration.GetValue<bool>("Swagger:Enabled");

// Outermost, so the logged status and duration are those of the finished response, including handled errors.
app.UseMiddleware<RequestLoggingMiddleware>();

// Unexpected exceptions and bare 401/403/404 responses all come out as RFC 7807 problem details.
app.UseExceptionHandler();
app.UseStatusCodePages();

if (swaggerEnabled)
{
    app.MapOpenApi().AllowAnonymous();
    app.UseSwaggerUI(options => options.SwaggerEndpoint("/openapi/v1.json", "DepotFlow v1"));
}

if (migrateOnStartup)
{
    await DatabaseInitializer.InitializeAsync(app.Services);
}

// In a container the API speaks plain HTTP behind the web proxy (TLS is terminated in front of it), so redirecting
// to HTTPS only makes sense when running directly on a developer machine.
if (app.Environment.IsDevelopment())
{
    app.UseHttpsRedirection();
}

app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();
app.MapHealthChecks("/health").AllowAnonymous();

app.Run();

public partial class Program;   // lets the integration tests start the app with WebApplicationFactory<Program>
