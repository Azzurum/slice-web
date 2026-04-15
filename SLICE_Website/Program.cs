using SLICE_Website.Data;
using SLICE_Website.Models;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddControllers();

// Learn more about configuring OpenAPI at https://aka.ms/aspnet/openapi
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

// ==========================================================
// DEPENDENCY INJECTION REGISTRATIONS
// ==========================================================
builder.Services.AddScoped<DatabaseService>();
builder.Services.AddScoped<InventoryRepository>();

// Note: As you migrate more of your system, you will add the rest here:
// builder.Services.AddScoped<SalesRepository>();
// builder.Services.AddScoped<LogisticsRepository>();
// ==========================================================

var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();
app.UseAuthorization();
app.MapControllers();
app.Run();