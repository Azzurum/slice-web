using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using SLICE_Website.Data;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddControllers();

// Swagger for testing APIs locally
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

// ==========================================================
// DEPENDENCY INJECTION REGISTRATIONS
// ==========================================================
// These registrations allow your Controllers to "ask" for 
// a Repository, and the system automatically provides a 
// fresh instance for every web request.

builder.Services.AddScoped<DatabaseService>();
builder.Services.AddScoped<InventoryRepository>();
builder.Services.AddScoped<DashboardRepository>();
builder.Services.AddScoped<WasteRepository>();
builder.Services.AddScoped<UserRepository>();
builder.Services.AddScoped<MenuRepository>();
builder.Services.AddScoped<FinanceRepository>();
builder.Services.AddScoped<DiscountRepository>();
builder.Services.AddScoped<AuditRepository>();
builder.Services.AddScoped<SuggestionRepository>();
builder.Services.AddScoped<LogisticsRepository>();
builder.Services.AddScoped<SalesRepository>();
builder.Services.AddScoped<SLICE_Website.Services.EmailService>();
builder.Services.AddScoped<SLICE_Website.Data.UserRepository>();
builder.Services.AddScoped<SLICE_Website.Data.AuditRepository>();
// ==========================================================

var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();

// This only applies to API endpoint security
app.UseAuthorization();

app.MapControllers();

app.Run();