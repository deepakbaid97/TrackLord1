using Microsoft.AspNetCore.Builder;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using TrackLord.Application.Commands;
using TrackLord.Domain.Repositories;
using TrackLord.Domain.Services;
using TrackLord.Infrastructure.Persistence;
using TrackLord.Infrastructure.Persistence.Repositories;
using TrackLord.Infrastructure.Services;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

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

builder.Services.AddScoped<IProductionLotRepository, ProductionLotRepository>();
builder.Services.AddScoped<IMaterialLotRepository, MaterialLotRepository>();
builder.Services.AddScoped<IManufacturingMovementRepository, ManufacturingMovementRepository>();
builder.Services.AddScoped<IManufacturingStepRepository, ManufacturingStepRepository>();
builder.Services.AddScoped<IMaterialConsumptionRepository, MaterialConsumptionRepository>();
builder.Services.AddScoped<IRejectionRecordRepository, RejectionRecordRepository>();

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

// Seed initial manufacturing steps & ensure database creation
using (var scope = app.Services.CreateScope())
{
    var logger = scope.ServiceProvider.GetRequiredService<ILogger<Program>>();
    var dbContext = scope.ServiceProvider.GetRequiredService<TrackLordDbContext>();

    try
    {
        dbContext.Database.EnsureCreated();

        if (!dbContext.ManufacturingSteps.Any())
        {
            var factoryId = Guid.NewGuid();
            dbContext.ManufacturingSteps.AddRange(
                new TrackLord.Domain.Aggregates.StepAggregate.ManufacturingStep(Guid.NewGuid(), "CUT", "Cutting", 1, factoryId),
                new TrackLord.Domain.Aggregates.StepAggregate.ManufacturingStep(Guid.NewGuid(), "STITCH", "Stitching", 2, factoryId),
                new TrackLord.Domain.Aggregates.StepAggregate.ManufacturingStep(Guid.NewGuid(), "WASH", "Washing", 3, factoryId),
                new TrackLord.Domain.Aggregates.StepAggregate.ManufacturingStep(Guid.NewGuid(), "FIN", "Finishing", 4, factoryId),
                new TrackLord.Domain.Aggregates.StepAggregate.ManufacturingStep(Guid.NewGuid(), "PACK", "Packing", 5, factoryId)
            );
            dbContext.SaveChanges();
            logger.LogInformation("Database initialized and default manufacturing steps seeded.");
        }
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

app.UseAuthorization();
app.MapControllers();

app.Run();
