using Microsoft.EntityFrameworkCore;
using POManagement.API.Data;
using POManagement.API.Services;
using Scalar.AspNetCore;

var builder = WebApplication.CreateBuilder(args);

// ── Services ──────────────────────────────────────────────────────────────────

builder.Services.AddControllers()
    .AddJsonOptions(options =>
    {
        options.JsonSerializerOptions.Converters.Add(
            new System.Text.Json.Serialization.JsonStringEnumConverter());
        // Fix circular reference: LineItem.PurchaseOrder <-> PurchaseOrder.LineItems
        options.JsonSerializerOptions.ReferenceHandler =
            System.Text.Json.Serialization.ReferenceHandler.IgnoreCycles;
    });

// EF Core — SQL Server LocalDB
builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseSqlServer(builder.Configuration.GetConnectionString("DefaultConnection")));

// Business Services
builder.Services.AddScoped<ApprovalRoutingService>();
builder.Services.AddScoped<WorkflowService>();

// CORS — allow React dev server on any common local port
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowLocalFrontend", policy =>
    {
        policy.WithOrigins(
                "http://localhost:5173",
                "http://localhost:5174",
                "http://localhost:5175",
                "http://localhost:5176",
                "http://localhost:3000",
                "http://localhost:3001",
                "https://localhost:5173",
                "https://localhost:5174")
            .AllowAnyHeader()
            .AllowAnyMethod();
    });
});

// Built-in OpenAPI (ASP.NET Core 10)
builder.Services.AddOpenApi();

// ── Middleware Pipeline ───────────────────────────────────────────────────────

var app = builder.Build();

// Auto-apply migrations on startup
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    db.Database.Migrate();
}

// Map OpenAPI endpoint and Scalar UI
app.MapOpenApi();
app.MapScalarApiReference(options =>
{
    options.Title = "PO Management API";
    options.Theme = ScalarTheme.DeepSpace;
});

app.UseCors("AllowLocalFrontend");
app.UseAuthorization();
app.MapControllers();

// Health check endpoint
app.MapGet("/health", () => Results.Ok(new { status = "healthy", timestamp = DateTime.UtcNow }));

app.Run();
