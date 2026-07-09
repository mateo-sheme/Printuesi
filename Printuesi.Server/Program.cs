using System.Text;
using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi;
using Printuesi.Server.Data;
using Printuesi.Server.Services;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.

builder.Services.AddControllers()
    .AddJsonOptions(options =>
    {
        options.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter());
    });
//builder.Services.AddDbContext<ApplicationDbContext>(option => 
//option.UseSqlServer(builder.Configuration.GetConnectionString("DefaultConnection")));

// Register Services
builder.Services.AddScoped<PrinterService_Interface, PrinterService>();
builder.Services.AddScoped<AuthService_Interface, AuthService>();
builder.Services.AddScoped<UsersService_Interface, UsersService>();
builder.Services.AddScoped<ClientService_Interface, ClientService>();
builder.Services.AddScoped<LabelTemplate_Interface, LabelTemplateService>();
builder.Services.AddScoped<PdfService_Interface, PdfService>();
builder.Services.AddScoped<OrderScanService_Interface, OrderScanService>();
builder.Services.AddScoped<PrintJobService_Interface, PrintJobService>();
builder.Services.AddHostedService<OutputCleanupService>();
builder.Services.AddScoped<ManualPrintingService_Interface, ManualPrintingService>();


builder.Services.AddDbContext<ApplicationDbContext>(option =>
    option.UseNpgsql(builder.Configuration.GetConnectionString("DefaultConnection"))
          .UseSnakeCaseNamingConvention()); // comes from EFCore.NamingConventions

var jwtSecret = builder.Configuration["Jwt:Secret"]!;
var outputPath = Path.Combine(builder.Environment.WebRootPath, "labels_output");

builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtSecret)),
            ValidateIssuer = false,
            ValidateAudience = false,
            ClockSkew = TimeSpan.Zero
        };
    });

    builder.Services.AddSwaggerGen(options =>
    {
        options.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
        {
            Name = "Authorization",
            Type = SecuritySchemeType.Http,
            Scheme = "bearer",
            BearerFormat = "JWT",
            In = ParameterLocation.Header,
            Description = "Enter your JWT token."
        });

        options.AddSecurityRequirement(document => new OpenApiSecurityRequirement
        {
            {
                new OpenApiSecuritySchemeReference("Bearer", document),
                new List<string>()
            }
        });
    });

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseDefaultFiles();

app.MapStaticAssets();

app.UseHttpsRedirection();

app.UseAuthentication();

app.UseAuthorization();

app.MapControllers();

app.MapFallbackToFile("/index.html");

app.Run();
