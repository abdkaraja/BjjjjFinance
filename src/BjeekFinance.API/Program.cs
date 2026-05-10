using BjeekFinance.API.Extensions;
using BjeekFinance.API.Middleware;
using BjeekFinance.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

// ── Services ───────────────────────────────────────────────────────────────────
builder.Services.AddControllers();
builder.Services.AddInfrastructure(builder.Configuration);
builder.Services.AddApplicationServices();
builder.Services.AddJwtAuthentication(builder.Configuration);
builder.Services.AddSwagger();

builder.Services.AddHttpContextAccessor();
builder.Services.AddHealthChecks()
    .AddDbContextCheck<BjeekFinanceDbContext>("sqlserver");

// ── App ────────────────────────────────────────────────────────────────────────
var app = builder.Build();

// Global error handler — must be first middleware
app.UseMiddleware<GlobalExceptionMiddleware>();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI(c =>
    {
        c.SwaggerEndpoint("/swagger/v1/swagger.json", "Bjeek Finance API v1");
        c.RoutePrefix = string.Empty;
        c.DisplayRequestDuration();
        c.EnableTryItOutByDefault();
    });

    // Auto-apply migrations in development
    using var scope = app.Services.CreateScope();
    var db = scope.ServiceProvider.GetRequiredService<BjeekFinanceDbContext>();
    await db.Database.MigrateAsync();
}

app.UseHttpsRedirection();
app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();
app.MapHealthChecks("/health");

app.Run();
