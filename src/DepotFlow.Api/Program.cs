using DepotFlow.Api.Auth;
using DepotFlow.Api.ErrorHandling;
using DepotFlow.Application;
using DepotFlow.Api.OpenApi;
using DepotFlow.Infrastructure;
using DepotFlow.Infrastructure.Persistence;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();
builder.Services.AddApplication();
builder.Services.AddInfrastructure(builder.Configuration);
builder.Services.AddJwtAuthentication();
builder.Services.AddHealthChecks();
builder.Services.AddProblemDetails();
builder.Services.AddExceptionHandler<GlobalExceptionHandler>();
builder.Services.AddOpenApi(options => options.AddDocumentTransformer<BearerSecurityTransformer>());

var app = builder.Build();

// Unexpected exceptions and bare 401/403/404 responses all come out as RFC 7807 problem details.
app.UseExceptionHandler();
app.UseStatusCodePages();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi().AllowAnonymous();
    app.UseSwaggerUI(options => options.SwaggerEndpoint("/openapi/v1.json", "DepotFlow v1"));
    await DatabaseInitializer.InitializeAsync(app.Services);
}

app.UseHttpsRedirection();

app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();
app.MapHealthChecks("/health").AllowAnonymous();

app.Run();
