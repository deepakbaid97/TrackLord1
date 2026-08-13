using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using TrackLord.Domain.Aggregates.ClientAggregate;
using TrackLord.Domain.Aggregates.MaterialLotAggregate;
using TrackLord.Domain.Aggregates.MovementAggregate;
using TrackLord.Domain.Aggregates.ProductionLotAggregate;
using TrackLord.Domain.Aggregates.StepAggregate;
using TrackLord.Domain.Aggregates.TenantAggregate;
using TrackLord.Domain.Enums;
using TrackLord.Domain.ValueObjects;
using TrackLord.Infrastructure.Persistence;

namespace TrackLord.StressTest;

public static class DbSeeder
{
    public static async Task SeedDatabaseAsync(TrackLordDbContext dbContext, int totalLots = 10000, int movementsPerLot = 50)
    {
        int totalMovements = totalLots * movementsPerLot;
        Console.WriteLine($"\n=======================================================");
        Console.WriteLine($" [SEEDER] Preparing to seed {totalLots:N0} production lots and {totalMovements:N0} movements...");
        Console.WriteLine($"=======================================================\n");

        var stopwatch = Stopwatch.StartNew();

        // 1. Ensure Tenant
        var tenantId = Tenant.DefaultTenantId;
        if (!await dbContext.Tenants.AnyAsync(t => t.Id == tenantId))
        {
            var defaultTenant = new Tenant(tenantId, "Stress Test Tenant", "STRESS");
            dbContext.Tenants.Add(defaultTenant);
            await dbContext.SaveChangesAsync();
            Console.WriteLine($"[1/5] Tenant created.");
        }

        // 2. Ensure Manufacturing Steps
        var factoryId = Guid.NewGuid();
        var existingSteps = await dbContext.ManufacturingSteps.ToListAsync();
        ManufacturingStep stepCut, stepStitch, stepWash, stepPack;

        if (existingSteps.Count == 0)
        {
            stepCut = new ManufacturingStep(Guid.NewGuid(), "CUT", "Cutting", 1, factoryId, isTerminating: false);
            stepStitch = new ManufacturingStep(Guid.NewGuid(), "STITCH", "Stitching", 2, factoryId, isTerminating: false);
            stepWash = new ManufacturingStep(Guid.NewGuid(), "WASH", "Washing", 3, factoryId, isTerminating: false);
            stepPack = new ManufacturingStep(Guid.NewGuid(), "PACK", "Packing", 4, factoryId, isTerminating: true);

            dbContext.ManufacturingSteps.AddRange(stepCut, stepStitch, stepWash, stepPack);
            await dbContext.SaveChangesAsync();
            Console.WriteLine($"[2/5] 4 Manufacturing steps created.");
        }
        else
        {
            stepCut = existingSteps[0];
            stepStitch = existingSteps.Count > 1 ? existingSteps[1] : stepCut;
            stepWash = existingSteps.Count > 2 ? existingSteps[2] : stepStitch;
            stepPack = existingSteps.Count > 3 ? existingSteps[3] : stepWash;
        }

        // 3. Seed Clients
        var clients = new List<Client>();
        if (!await dbContext.Clients.AnyAsync())
        {
            for (int c = 1; c <= 100; c++)
            {
                clients.Add(new Client(Guid.NewGuid(), $"Stress Client {c}", $"+91 90000 {c:D5}", "Buyer"));
            }
            dbContext.Clients.AddRange(clients);
            await dbContext.SaveChangesAsync();
            Console.WriteLine($"[3/5] 100 Clients created.");
        }

        // 4. Seed Production Lots & Movements in Batch Chunks
        int batchSize = 1000;
        var now = DateTime.UtcNow;
        var itemId = Guid.NewGuid();
        int seededLotsCount = 0;
        int seededMovementsCount = 0;

        Console.WriteLine($"[4/5] Bulk seeding {totalLots:N0} lots & {totalMovements:N0} movements in batches of {batchSize} lots...");

        for (int i = 0; i < totalLots; i += batchSize)
        {
            int currentBatchLots = Math.Min(batchSize, totalLots - i);
            var lotBatch = new List<ProductionLot>();
            var movementBatch = new List<ManufacturingMovement>();

            for (int j = 0; j < currentBatchLots; j++)
            {
                int lotIndex = i + j + 1;
                var lotId = Guid.NewGuid();
                var lotNumberStr = $"LOT-STRESS-{lotIndex:D6}";

                var lot = ProductionLot.Create(
                    lotId,
                    ProductionLotNumber.Create(lotNumberStr),
                    itemId,
                    new Quantity(10000, UnitType.Pieces),
                    stepCut.Id,
                    "StressSeeder",
                    "Bulk stress test lot");

                lotBatch.Add(lot);

                // Initial Movement
                movementBatch.Add(ManufacturingMovement.Create(
                    Guid.NewGuid(),
                    lotId,
                    fromStepId: null,
                    toStepId: stepCut.Id,
                    quantity: 10000,
                    type: MovementType.InitialProduction,
                    movementDate: now.AddDays(-30),
                    clientId: clients.Count > 0 ? clients[j % clients.Count].Id : null,
                    clientName: clients.Count > 0 ? clients[j % clients.Count].Name : null,
                    createdBy: "Seeder",
                    remarks: "Initial production lot creation",
                    referenceNumber: $"INIT-{lotNumberStr}"));

                // Subsequent Step Transfers (to simulate movements)
                for (int m = 1; m < movementsPerLot; m++)
                {
                    movementBatch.Add(ManufacturingMovement.Create(
                        Guid.NewGuid(),
                        lotId,
                        fromStepId: stepCut.Id,
                        toStepId: stepStitch.Id,
                        quantity: 10,
                        type: MovementType.StepTransfer,
                        movementDate: now.AddDays(-30).AddMinutes(m * 10),
                        clientId: clients.Count > 0 ? clients[j % clients.Count].Id : null,
                        clientName: clients.Count > 0 ? clients[j % clients.Count].Name : null,
                        createdBy: "Seeder",
                        remarks: $"Movement {m} transfer from {stepCut.Name} to {stepStitch.Name}",
                        referenceNumber: $"MOV-{lotNumberStr}-{m:D3}"));
                }
            }

            dbContext.ProductionLots.AddRange(lotBatch);
            dbContext.ManufacturingMovements.AddRange(movementBatch);
            await dbContext.SaveChangesAsync();

            // Clear ChangeTracker to prevent memory buildup during massive bulk insert
            dbContext.ChangeTracker.Clear();

            seededLotsCount += currentBatchLots;
            seededMovementsCount += currentBatchLots * movementsPerLot;

            double progress = (double)seededLotsCount / totalLots * 100;
            Console.WriteLine($"      Seeded {seededLotsCount:N0}/{totalLots:N0} lots ({seededMovementsCount:N0} movements) [{progress:F1}%]");
        }

        stopwatch.Stop();
        Console.WriteLine($"\n[5/5] SUCCESS! Seeded {seededLotsCount:N0} lots & {seededMovementsCount:N0} movements in {stopwatch.Elapsed.TotalSeconds:F2} seconds.");
        Console.WriteLine($"=======================================================\n");
    }
}
