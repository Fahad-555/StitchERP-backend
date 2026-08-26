
using StitchERP.Application;
using StitchERP.Infrastructure;
using StitchERP.Api.Middleware;
using StitchERP.Api;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();
builder.Services.AddAuthorization();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();
builder.Services.AddHealthChecks();
builder.Services.AddCors(options =>
{
    options.AddPolicy("Frontend", policy => policy
        .WithOrigins("http://localhost:4200", "http://127.0.0.1:4300", "http://localhost:4300")
        .AllowAnyHeader()
        .AllowAnyMethod());
});
builder.Services.AddApplication();
builder.Services.AddInfrastructure(builder.Configuration);

var app = builder.Build();

app.UseMiddleware<CorrelationIdMiddleware>();
app.UseMiddleware<ApiExceptionMiddleware>();
app.UseMiddleware<DevelopmentIdentityMiddleware>();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();
app.UseCors("Frontend");
app.UseAuthorization();
app.MapControllers();
app.MapHealthChecks("/health");
app.MapGet("/health/database", async (IServiceProvider services, CancellationToken cancellationToken) =>
{
    var readiness = await services.CheckDatabaseAsync(cancellationToken);
    return readiness.Status == "ready" ? Results.Ok(readiness) : Results.Json(readiness, statusCode: 503);
});

app.MapGet("/api/v1/info", () => new
{
    name = "StitchERP API",
    version = "v1",
    environment = app.Environment.EnvironmentName,
    status = "ready"
});

app.Run();
