using Microsoft.AspNetCore.Builder;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using TrackLord.Application.Commands;
using TrackLord.Domain.Common;
using TrackLord.Domain.Repositories;
using TrackLord.Domain.Services;
using TrackLord.Infrastructure.Persistence;
using TrackLord.Infrastructure.Persistence.Repositories;
using TrackLord.Infrastructure.Services;

using System.Text;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();

// Configure JWT Authentication
var secretKey = builder.Configuration["JwtSettings:SecretKey"] ?? "TrackLordSuperSecretAuthenticationKey2026!#ForJwtTokens";
var issuer = builder.Configuration["JwtSettings:Issuer"] ?? "TrackLordApi";
var audience = builder.Configuration["JwtSettings:Audience"] ?? "TrackLordWeb";

builder.Services.AddAuthentication(options =>
{
    options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
    options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
})
.AddJwtBearer(options =>
{
    options.RequireHttpsMetadata = false;
    options.SaveToken = true;
    options.TokenValidationParameters = new TokenValidationParameters
    {
        ValidateIssuerSigningKey = true,
        IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(secretKey)),
        ValidateIssuer = true,
        ValidIssuer = issuer,
        ValidateAudience = true,
        ValidAudience = audience,
        ValidateLifetime = true,
        ClockSkew = TimeSpan.FromMinutes(2)
    };
});

builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new Microsoft.OpenApi.Models.OpenApiInfo { Title = "TrackLord API", Version = "v1" });
    c.AddSecurityDefinition("Bearer", new Microsoft.OpenApi.Models.OpenApiSecurityScheme
    {
        Description = "JWT Authorization header using the Bearer scheme. Example: \"Bearer {token}\"",
        Name = "Authorization",
        In = Microsoft.OpenApi.Models.ParameterLocation.Header,
        Type = Microsoft.OpenApi.Models.SecuritySchemeType.ApiKey,
        Scheme = "Bearer"
    });
    c.AddSecurityRequirement(new Microsoft.OpenApi.Models.OpenApiSecurityRequirement
    {
        {
            new Microsoft.OpenApi.Models.OpenApiSecurityScheme
            {
                Reference = new Microsoft.OpenApi.Models.OpenApiReference
                {
                    Type = Microsoft.OpenApi.Models.ReferenceType.SecurityScheme,
                    Id = "Bearer"
                }
            },
            Array.Empty<string>()
        }
    });
});

// Auth Services
var serviceAccountPath = builder.Configuration["Firebase:ServiceAccountPath"] ?? "tracklord-service.json";
if (!System.IO.File.Exists(serviceAccountPath))
{
    // Try fallback file names
    var alternatePaths = new[] { "firebase-service-account.json", "tracklord-service.json" };
    serviceAccountPath = alternatePaths.FirstOrDefault(p => System.IO.File.Exists(p)) ?? serviceAccountPath;
}

if (System.IO.File.Exists(serviceAccountPath))
{
    try
    {
        FirebaseAdmin.FirebaseApp.Create(new FirebaseAdmin.AppOptions
        {
            Credential = Google.Apis.Auth.OAuth2.GoogleCredential.FromFile(serviceAccountPath)
        });
        Console.WriteLine($"[Firebase] Initialized Firebase Admin SDK from: {serviceAccountPath}");
    }
    catch (Exception ex)
    {
        Console.WriteLine($"[Firebase] Warning initializing Firebase Admin SDK: {ex.Message}");
    }
}

builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<ITenantContext, TenantContext>();

builder.Services.AddSingleton<IJwtTokenService, JwtTokenService>();
builder.Services.AddSingleton<IFirebaseTokenVerifier, FirebaseTokenVerifier>();

// MediatR
builder.Services.AddMediatR(cfg => cfg.RegisterServicesFromAssembly(typeof(CreateProductionLotCommand).Assembly));

// Configure EF Core (PostgreSQL default, with graceful fallback to InMemory if DB server is offline)
var connectionString = builder.Configuration.GetConnectionString("DefaultConnection");

builder.Services.AddDbContext<TrackLordDbContext>(options =>
{
    if (!string.IsNullOrWhiteSpace(connectionString) && !connectionString.Contains("InMemory", StringComparison.OrdinalIgnoreCase))
    {
        options.UseNpgsql(connectionString);
    }
    else
    {
        options.UseInMemoryDatabase("TrackLordErpDb");
    }
});

// Domain Services & Repositories
builder.Services.AddSingleton<ILedgerReplayEngine, LedgerReplayEngine>();
builder.Services.AddSingleton<IUnitConverterService, UnitConverterService>();

builder.Services.AddScoped<ITenantRepository, TenantRepository>();
builder.Services.AddScoped<IProductionLotRepository, ProductionLotRepository>();
builder.Services.AddScoped<IMaterialLotRepository, MaterialLotRepository>();
builder.Services.AddScoped<IManufacturingMovementRepository, ManufacturingMovementRepository>();
builder.Services.AddScoped<IManufacturingStepRepository, ManufacturingStepRepository>();
builder.Services.AddScoped<IMaterialConsumptionRepository, MaterialConsumptionRepository>();
builder.Services.AddScoped<IRejectionRecordRepository, RejectionRecordRepository>();
builder.Services.AddScoped<IClientRepository, ClientRepository>();
builder.Services.AddScoped<IUserRepository, UserRepository>();

// CORS for Frontend SvelteKit App
builder.Services.AddCors(options =>
{
    options.AddDefaultPolicy(policy =>
    {
        policy.AllowAnyOrigin()
              .AllowAnyHeader()
              .AllowAnyMethod();
    });
});

var app = builder.Build();

app.UseCors();

// Ensure database creation
using (var scope = app.Services.CreateScope())
{
    var logger = scope.ServiceProvider.GetRequiredService<ILogger<Program>>();
    var dbContext = scope.ServiceProvider.GetRequiredService<TrackLordDbContext>();

    try
    {
        dbContext.Database.Migrate();

        if (!dbContext.Tenants.Any())
        {
            var defaultTenant = new TrackLord.Domain.Aggregates.TenantAggregate.Tenant(
                TrackLord.Domain.Aggregates.TenantAggregate.Tenant.DefaultTenantId,
                "Default Tenant",
                "DEFAULT");
            dbContext.Tenants.Add(defaultTenant);
            dbContext.SaveChanges();
        }

        logger.LogInformation("Database initialized.");
    }
    catch (Exception ex)
    {
        logger.LogWarning(ex, "Could not initialize PostgreSQL database. Ensure PostgreSQL server is running at connection string.");
    }
}

// Configure HTTP request pipeline
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseDefaultFiles();
app.UseStaticFiles();

app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();
app.MapFallbackToFile("index.html");

app.Run();
