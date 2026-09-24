using Infrastructure.Data.Contexts;
using Infrastructure.Hubs;
using Infrastructure.Services.Carriers;
using Infrastructure.Repositories;
using Application.Interfaces;
using Application.Services;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.Identity.Web;
using FluentValidation;
using FluentValidation.AspNetCore;
using Microsoft.OpenApi.Models;
using System.Reflection;
using System.Text;
using Microsoft.IdentityModel.Tokens;
using Microsoft.AspNetCore.Authentication;
using Infrastructure.Data;

var builder = WebApplication.CreateBuilder(args);

// 🔐 Authentication
// 🔐 Configuración de Autenticación Multiesquema
builder.Services.AddAuthentication(options =>
{
    // Por defecto intentamos JWT (Para Armando Banegas / Custom JWT)
    options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
    options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
})
.AddJwtBearer(JwtBearerDefaults.AuthenticationScheme, options =>
{
    // Configuración para validar tokens generados por el AuthController
    options.TokenValidationParameters = new TokenValidationParameters
    {
        ValidateIssuer = true,
        ValidateAudience = true,
        ValidateLifetime = true,
        ValidateIssuerSigningKey = true,
        ValidIssuer = builder.Configuration["Jwt:Issuer"],
        ValidAudience = builder.Configuration["Jwt:Audience"],
        IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(builder.Configuration["Jwt:Key"] ?? "ARMANDO_BANEGAS_SUPER_SECRET_SECURITY_KEY_2026"))
    };
})
.AddMicrosoftIdentityWebApi(builder.Configuration.GetSection("AzureAd"), "AzureAd")
    .EnableTokenAcquisitionToCallDownstreamApi()
    .AddDownstreamApi("DownstreamApi", builder.Configuration.GetSection("DownstreamApi"))
    .AddInMemoryTokenCaches();

// ✅ Agregar esquema de API Key
builder.Services.AddAuthentication()
    .AddScheme<AuthenticationSchemeOptions, INVERBANHN.Authentication.ApiKeyAuthenticationHandler>("ApiKey", null);

// ✅ Política de Autorización Combinada (Cualquiera de los 3 esquemas es válido)
builder.Services.AddAuthorization(options =>
{
    var defaultAuthorizationPolicyBuilder = new Microsoft.AspNetCore.Authorization.AuthorizationPolicyBuilder(
        JwtBearerDefaults.AuthenticationScheme,
        "AzureAd",
        "ApiKey");
    defaultAuthorizationPolicyBuilder = defaultAuthorizationPolicyBuilder.RequireAuthenticatedUser();
    options.DefaultPolicy = defaultAuthorizationPolicyBuilder.Build();
    
    options.AddPolicy("SuperAdminOnly", policy => policy.RequireRole("SuperAdmin"));
});

// ✅ EF CORE – OBLIGATORIO PARA aspnet-codegenerator
builder.Services.AddDbContext<MarketplaceDbContext>(options =>
    options.UseSqlServer(
        builder.Configuration.GetConnectionString("DefaultConnection")
    ));

// ✅ DAPPER
builder.Services.AddSingleton<DapperContext>();
builder.Services.AddScoped<ISqlStoredProcedureRepository, SqlStoredProcedureRepository>();
builder.Services.AddScoped<Infrastructure.Data.DapperTransactionHelper>();

// ✅ Cache en Memoria (Rate Limiting para Gift Cards)
builder.Services.AddMemoryCache();

// ✅ FluentValidation — Registrar todos los validadores del ensamblado
builder.Services.AddFluentValidationAutoValidation();
builder.Services.AddValidatorsFromAssemblyContaining<Program>();

// ✅ Controllers
builder.Services.AddControllers().AddJsonOptions(options =>
{
    options.JsonSerializerOptions.ReferenceHandler = System.Text.Json.Serialization.ReferenceHandler.IgnoreCycles;
});

// ✅ Swagger / Swashbuckle — Documentación de API
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(options =>
{
    options.SwaggerDoc("v1", new OpenApiInfo
    {
        Title = "INVERBANHN — Marketplace Logístico API",
        Version = "v1",
        Description = "API del Marketplace Logístico de Honduras. Todos los campos monetarios están en **Lempiras (LPS)** con precisión de 2 decimales. Los campos RTN son strings de 14 dígitos según formato SAR.",
        Contact = new OpenApiContact
        {
            Name = "Inversiones Banegas",
            Email = "dev@inverbanhn.com"
        }
    });

    // Incluir los comentarios XML para generar la documentación automáticamente
    var xmlFilename = $"{Assembly.GetExecutingAssembly().GetName().Name}.xml";
    var xmlPath = Path.Combine(AppContext.BaseDirectory, xmlFilename);
    if (File.Exists(xmlPath))
    {
        options.IncludeXmlComments(xmlPath);
    }

    // Definir esquema de seguridad para ApiKey
    options.AddSecurityDefinition("ApiKey", new OpenApiSecurityScheme
    {
        Description = "API Key de autenticación. Enviar en el header: X-Api-Key: {valor}",
        Name = "X-Api-Key",
        In = ParameterLocation.Header,
        Type = SecuritySchemeType.ApiKey,
        Scheme = "ApiKey"
    });

    // Definir esquema de seguridad para JWT (Bearer)
    options.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
    {
        Description = "JWT Authorization header using the Bearer scheme. \r\n\r\n Enter 'Bearer' [space] and then your token in the text input below.\r\n\r\nExample: \"Bearer eyJhbGciOi...\"",
        Name = "Authorization",
        In = ParameterLocation.Header,
        Type = SecuritySchemeType.ApiKey,
        Scheme = "Bearer"
    });

    options.AddSecurityRequirement(new OpenApiSecurityRequirement
    {
        {
            new OpenApiSecurityScheme
            {
                Reference = new OpenApiReference
                {
                    Type = ReferenceType.SecurityScheme,
                    Id = "ApiKey"
                }
            },
            Array.Empty<string>()
        },
        {
            new OpenApiSecurityScheme
            {
                Reference = new OpenApiReference
                {
                    Type = ReferenceType.SecurityScheme,
                    Id = "Bearer"
                }
            },
            Array.Empty<string>()
        }
    });
});

// ✅ CORS Policy configurations (Azure Static Web Apps & App Service Support)
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowSiteGround", policy =>
    {
        var configuredOrigins = builder.Configuration["Cors:AllowedOrigins"]?
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries) ?? Array.Empty<string>();

        var defaultOrigins = new[]
        {
            "https://www.inverbanhn.com",
            "https://inverbanhn.com",
            "https://vendor.inverbanhn.com",
            "https://admin.inverbanhn.com",
            "http://localhost:3000",
            "http://localhost:5173",
            "http://localhost:4200",
            "http://localhost:8080",
            "http://localhost:3001"
        };

        var allowedOrigins = configuredOrigins.Concat(defaultOrigins).Distinct().ToArray();

        policy.SetIsOriginAllowed(origin =>
        {
            if (string.IsNullOrWhiteSpace(origin)) return false;
            try
            {
                var uri = new Uri(origin);
                var host = uri.Host;
                return host.EndsWith(".azurestaticapps.net", StringComparison.OrdinalIgnoreCase) ||
                       host.EndsWith(".azurewebsites.net", StringComparison.OrdinalIgnoreCase) ||
                       host.Equals("localhost", StringComparison.OrdinalIgnoreCase) ||
                       allowedOrigins.Contains(origin, StringComparer.OrdinalIgnoreCase);
            }
            catch
            {
                return false;
            }
        })
        .AllowAnyMethod()
        .AllowAnyHeader()
        .AllowCredentials(); 
    });

    options.AddPolicy("AllowWebApp", policy =>
    {
        policy.SetIsOriginAllowed(origin => true)
              .AllowAnyMethod()
              .AllowAnyHeader()
              .AllowCredentials(); 
    });
});

// ✅ SignalR
builder.Services.AddSignalR();

// ✅ Registrar Servicios de Aplicación
builder.Services.AddScoped<INotificationService, NotificationService>();
builder.Services.AddScoped<IWalletManagementService, WalletManagementService>();
builder.Services.AddScoped<ITaxService, TaxService>();
builder.Services.AddScoped<IReportsService, ReportsService>();
builder.Services.AddScoped<IDispatchService, DispatchService>();
builder.Services.AddScoped<IAnalyticsService, AnalyticsService>();
builder.Services.AddScoped<IProductService, ProductService>();
builder.Services.AddScoped<IProductMediaService, ProductMediaService>();
builder.Services.AddScoped<IImageUploadService, ImageUploadService>();
builder.Services.AddScoped<ICatalogProductMediaService, CatalogProductMediaService>();
builder.Services.AddScoped<IEmailService, EmailService>();

// ✅ Registrar Integraciones de Paqueteras
builder.Services.AddScoped<ICarrierIntegration, CaexCarrierIntegration>();
builder.Services.AddScoped<ICarrierIntegration, CargoExpresoIntegration>();
builder.Services.AddScoped<ICarrierIntegrationFactory, CarrierIntegrationFactory>();

var app = builder.Build();

// 🚀 Sembrar Base de Datos (Armando Banegas)
using (var scope = app.Services.CreateScope())
{
    var services = scope.ServiceProvider;
    try
    {
        var context = services.GetRequiredService<MarketplaceDbContext>();
        context.Database.EnsureCreated();
        Infrastructure.Data.DbInitializer.Initialize(context);
    }
    catch (Exception ex)
    {
        var logger = services.GetRequiredService<ILogger<Program>>();
        logger.LogError(ex, "Error al inicializar datos.");
    }
}

// ✅ Swagger UI
app.UseSwagger();
app.UseSwaggerUI(options =>
{
    options.SwaggerEndpoint("/swagger/v1/swagger.json", "INVERBANHN API v1");
    options.RoutePrefix = "swagger";
});

if (!app.Environment.IsDevelopment())
{
    app.UseHttpsRedirection();
}

// Ubica esto estrictamente antes de app.UseAuthorization();
app.UseCors("AllowSiteGround");

app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();
app.MapHub<NotificationHub>("/hubs/notifications");

app.Run();

public partial class Program { }