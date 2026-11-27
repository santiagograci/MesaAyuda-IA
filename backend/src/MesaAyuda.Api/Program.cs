using System.Security.Claims;
using System.Text;
using System.Text.Json.Serialization;
using MesaAyuda.Application;
using MesaAyuda.Application.Interfaces;
using MesaAyuda.Infrastructure;
using MesaAyuda.Infrastructure.Persistence;
using MesaAyuda.Infrastructure.Security;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddApplication();
builder.Services.AddInfrastructure(builder.Configuration);

builder.Services.AddControllers()
    .AddJsonOptions(options =>
    {
        options.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter());
    });

var jwtSettings = JwtSettings.FromConfiguration(builder.Configuration);
builder.Services
    .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.MapInboundClaims = false;
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            ValidIssuer = jwtSettings.Issuer,
            ValidAudience = jwtSettings.Audience,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtSettings.Secret)),
            NameClaimType = ClaimTypes.Name,
            RoleClaimType = ClaimTypes.Role
        };
    });

builder.Services.AddAuthorization();

builder.Services.AddCors(options =>
{
    options.AddPolicy("FrontendPolicy", policy =>
    {
        policy
            .WithOrigins("http://localhost:4200", "http://127.0.0.1:4200")
            .AllowAnyHeader()
            .AllowAnyMethod();
    });
});

var connectionString = MesaAyuda.Infrastructure.DependencyInjection.ResolveConnectionString(builder.Configuration);
var ragHealthUrl = (builder.Configuration["RAG_SERVICE_URL"] ?? "http://rag-service:8000").TrimEnd('/') + "/health";

builder.Services.AddHealthChecks()
    .AddNpgSql(connectionString, name: "postgresql")
    .AddUrlGroup(new Uri(ragHealthUrl), name: "rag-service", tags: new[] { "ready" });

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(options =>
{
    options.SwaggerDoc("v1", new OpenApiInfo
    {
        Title = "MesaAyuda IA API",
        Version = "v1",
        Description = "API empresarial de Mesa de Ayuda con Clean Architecture, JWT y soporte RAG para Ofima ERP."
    });

    foreach (var xmlFile in Directory.GetFiles(AppContext.BaseDirectory, "*.xml"))
    {
        options.IncludeXmlComments(xmlFile, includeControllerXmlComments: true);
    }

    var bearerScheme = new OpenApiSecurityScheme
    {
        Name = "Authorization",
        Description = "Ingrese el token JWT usando el formato: Bearer {token}",
        In = ParameterLocation.Header,
        Type = SecuritySchemeType.Http,
        Scheme = "bearer",
        BearerFormat = "JWT",
        Reference = new OpenApiReference
        {
            Type = ReferenceType.SecurityScheme,
            Id = JwtBearerDefaults.AuthenticationScheme
        }
    };

    options.AddSecurityDefinition(JwtBearerDefaults.AuthenticationScheme, bearerScheme);
    options.AddSecurityRequirement(new OpenApiSecurityRequirement
    {
        { bearerScheme, Array.Empty<string>() }
    });
});

var app = builder.Build();

using (var scope = app.Services.CreateScope())
{
    var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    var passwordHasher = scope.ServiceProvider.GetRequiredService<IPasswordHasher>();

    if (dbContext.Database.IsRelational())
    {
        await dbContext.Database.MigrateAsync();
    }
    else
    {
        await dbContext.Database.EnsureCreatedAsync();
    }

    await DbSeeder.SeedAsync(dbContext, passwordHasher);
}

app.UseSwagger();
app.UseSwaggerUI(options =>
{
    options.SwaggerEndpoint("/swagger/v1/swagger.json", "MesaAyuda IA API v1");
});

app.UseCors("FrontendPolicy");
app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();
app.MapHealthChecks("/health");

app.Run();

/// <summary>
/// Punto de entrada parcial expuesto para pruebas de integración con <c>WebApplicationFactory</c>.
/// </summary>
public partial class Program
{
}
