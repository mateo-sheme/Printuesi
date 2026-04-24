using Microsoft.EntityFrameworkCore;
using Printuesi.Server.Data;
using Printuesi.Server.Services;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.

builder.Services.AddControllers();
builder.Services.AddDbContext<ApplicationDbContext>(option => 
option.UseSqlServer(builder.Configuration.GetConnectionString("DefaultConnection")));
builder.Services.AddOpenApi();

builder.Services.AddScoped<IPrinterService, PrinterService>();

var app = builder.Build();

app.UseDefaultFiles();
app.MapStaticAssets();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.UseHttpsRedirection();

app.UseAuthorization();

app.MapControllers();

app.MapFallbackToFile("/index.html");

app.Run();
