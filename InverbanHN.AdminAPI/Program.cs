using System;
using System.Linq;
using System.Text;
using System.Threading.RateLimiting;
using InverbanHN.Shared.Authentication;
using InverbanHN.Shared.Data;
using InverbanHN.Shared.Middleware;
using InverbanHN.Shared.Services;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;

var builder = WebApplication.CreateBuilder(args);

// Authentication
builder.Services.AddAuthentication(options =>
{
    options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
    options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
})
.AddJwtBearer(options =>
{
    options.TokenValidationParameters = new TokenValidationParameters
    {
        ValidateIssuer = true,
        ValidateAudience = true,
        ValidateLifetime = true,
        ValidateIssuerSigningKey = true,
        ValidIssuer = builder.Configuration["Jwt:Issuer"] ?? "INVERBANHN_API",
        ValidAudience = builder.Configuration["Jwt:Audience"] ?? "INVERBANHN_CLIENTS",
        IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(builder.Configuration["Jwt:Key"] ?? "ARMANDO_BANEGAS_SUPER_SECRET_SECURITY_KEY_2026"))
    };
})
.AddScheme<Microsoft.AspNetCore.Authentication.AuthenticationSchemeOptions, ApiKeyAuthenticationHandler>("ApiKey", null);

builder.Services.AddAuthorization(options =>
{
    options.AddPolicy("SuperAdminOnly", policy => policy.RequireRole("SuperAdmin"));
});

// Data & Core Infrastructure Services (DI Audit)
builder.Services.AddSingleton<DapperContext>();
builder.Services.AddTransient<IEmailService, SendGridEmailService>();
builder.Services.AddScoped<IMediaService, AzureBlobStorageService>();

// CORS Policy (Azure Static Web Apps & App Service Support)
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
            "http://localhost:5180",
            "http://localhost:5173",
            "http://localhost:5174",
            "http://localhost:3000",
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

// Rate Limiting (Prevención de abusos y ataques de fuerza bruta)
builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
    options.AddPolicy("AuthRateLimit", httpContext =>
        RateLimitPartition.GetFixedWindowLimiter(
            partitionKey: httpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown_ip",
            factory: _ => new FixedWindowRateLimiterOptions
            {
                AutoReplenishment = true,
                PermitLimit = 10,
                Window = TimeSpan.FromMinutes(1),
                QueueLimit = 0
            }));
});

builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new OpenApiInfo { Title = "InverbanHN AdminAPI", Version = "v1" });
});

var app = builder.Build();

// Manejo Global de Excepciones y Logging
app.UseMiddleware<GlobalExceptionHandlerMiddleware>();

app.UseSwagger();
app.UseSwaggerUI(c => c.SwaggerEndpoint("/swagger/v1/swagger.json", "AdminAPI v1"));

// Ubica esto estrictamente antes de app.UseAuthorization();
app.UseCors("AllowSiteGround");
app.UseRateLimiter();
app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();

app.Run();
