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
            var stepCut = new TrackLord.Domain.Aggregates.StepAggregate.ManufacturingStep(Guid.NewGuid(), "CUT", "Cutting", 1, factoryId);
            var stepStitch = new TrackLord.Domain.Aggregates.StepAggregate.ManufacturingStep(Guid.NewGuid(), "STITCH", "Stitching", 2, factoryId);
            var stepWash = new TrackLord.Domain.Aggregates.StepAggregate.ManufacturingStep(Guid.NewGuid(), "WASH", "Washing", 3, factoryId);
            var stepFin = new TrackLord.Domain.Aggregates.StepAggregate.ManufacturingStep(Guid.NewGuid(), "FIN", "Finishing", 4, factoryId);
            var stepPack = new TrackLord.Domain.Aggregates.StepAggregate.ManufacturingStep(Guid.NewGuid(), "PACK", "Packing", 5, factoryId);

            dbContext.ManufacturingSteps.AddRange(stepCut, stepStitch, stepWash, stepFin, stepPack);
            dbContext.SaveChanges();

            // Seed sample material lot
            var matLotId = Guid.NewGuid();
            var matLot = TrackLord.Domain.Aggregates.MaterialLotAggregate.MaterialLot.Create(
                matLotId,
                TrackLord.Domain.ValueObjects.MaterialLotNumber.Create("MAT-101"),
                Guid.NewGuid(),
                new TrackLord.Domain.ValueObjects.Quantity(500, TrackLord.Domain.Enums.UnitType.Meters),
                "Denim Grey Cloth 300GSM");
            dbContext.MaterialLots.Add(matLot);

            // Seed sample production lot
            var lotId = Guid.NewGuid();
            var prodLot = TrackLord.Domain.Aggregates.ProductionLotAggregate.ProductionLot.Create(
                lotId,
                TrackLord.Domain.ValueObjects.ProductionLotNumber.Create("Lot-101"),
                Guid.NewGuid(),
                new TrackLord.Domain.ValueObjects.Quantity(95, TrackLord.Domain.Enums.UnitType.Pieces),
                stepCut.Id,
                "Operator1",
                "ABC Garments Summer Order");
            dbContext.ProductionLots.Add(prodLot);

            // Seed movements
            var now = DateTime.UtcNow;
            dbContext.ManufacturingMovements.AddRange(
                TrackLord.Domain.Aggregates.MovementAggregate.ManufacturingMovement.Create(
                    Guid.NewGuid(), lotId, null, stepCut.Id, 95, TrackLord.Domain.Enums.MovementType.InitialProduction, now.AddDays(-3), "ABC Garments", "System", "Initial Lot Creation"),
                TrackLord.Domain.Aggregates.MovementAggregate.ManufacturingMovement.Create(
                    Guid.NewGuid(), lotId, stepCut.Id, stepStitch.Id, 95, TrackLord.Domain.Enums.MovementType.StepTransfer, now.AddDays(-2), "ABC Garments", "Operator1", "Moved to Stitching"),
                TrackLord.Domain.Aggregates.MovementAggregate.ManufacturingMovement.Create(
                    Guid.NewGuid(), lotId, stepStitch.Id, stepWash.Id, 95, TrackLord.Domain.Enums.MovementType.StepTransfer, now.AddDays(-1), "ABC Garments", "Operator2", "Moved to Washing"),
                TrackLord.Domain.Aggregates.MovementAggregate.ManufacturingMovement.Create(
                    Guid.NewGuid(), lotId, stepWash.Id, stepFin.Id, 80, TrackLord.Domain.Enums.MovementType.StepTransfer, now.AddHours(-4), "ABC Garments", "Supervisor", "Partial move to Finishing")
            );

            // Seed material consumption
            dbContext.MaterialConsumptions.Add(
                TrackLord.Domain.Aggregates.ConsumptionAggregate.MaterialConsumption.Create(
                    Guid.NewGuid(), matLotId, lotId, stepCut.Id, 30, TrackLord.Domain.Enums.UnitType.Meters, now.AddDays(-3), "Operator1")
            );

            dbContext.SaveChanges();
            logger.LogInformation("Database initialized and default manufacturing steps, production lot, and material lots seeded.");
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
