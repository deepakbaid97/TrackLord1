using System;
using System.Collections.Generic;
using System.Data;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using NpgsqlTypes;
using TrackLord.Domain.Aggregates.ClientAggregate;
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
    public static async Task CleanStressTestDataAsync(TrackLordDbContext dbContext)
    {
        Console.WriteLine("[CLEANUP] Removing previous stress test data...");
        var sw = Stopwatch.StartNew();

        if (dbContext.Database.IsNpgsql())
        {
            await dbContext.Database.ExecuteSqlRawAsync(@"
                DELETE FROM manufacturing_movements 
                WHERE production_lot_id IN (SELECT id FROM production_lots WHERE lot_number LIKE 'LOT-STRESS-%');
                DELETE FROM production_lots WHERE lot_number LIKE 'LOT-STRESS-%';
            ");
        }
        else
        {
            var stressLots = (await dbContext.ProductionLots
                .IgnoreQueryFilters()
                .ToListAsync())
                .Where(l => l.LotNumber.Value.StartsWith("LOT-STRESS-"))
                .Select(l => l.Id)
                .ToList();

            if (stressLots.Count > 0)
            {
                var stressMovements = await dbContext.ManufacturingMovements
                    .IgnoreQueryFilters()
                    .Where(m => stressLots.Contains(m.ProductionLotId))
                    .ToListAsync();
                dbContext.ManufacturingMovements.RemoveRange(stressMovements);

                var lotsToRemove = (await dbContext.ProductionLots
                    .IgnoreQueryFilters()
                    .ToListAsync())
                    .Where(l => stressLots.Contains(l.Id))
                    .ToList();
                dbContext.ProductionLots.RemoveRange(lotsToRemove);
                await dbContext.SaveChangesAsync();
            }
        }

        sw.Stop();
        Console.WriteLine($"[CLEANUP] Cleanup completed in {sw.Elapsed.TotalSeconds:F2} seconds.\n");
    }

    public static async Task SeedDatabaseAsync(TrackLordDbContext dbContext, int totalLots = 40000, int movementsPerLot = 50)
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

        // 3. Ensure Seed Clients
        var clients = await dbContext.Clients.ToListAsync();
        if (clients.Count == 0)
        {
            for (int c = 1; c <= 100; c++)
            {
                clients.Add(new Client(Guid.NewGuid(), $"Stress Client {c}", $"+91 90000 {c:D5}", "Buyer"));
            }
            dbContext.Clients.AddRange(clients);
            await dbContext.SaveChangesAsync();
            Console.WriteLine($"[3/5] 100 Clients created.");
        }

        // 4. Clean up any existing stress lots to avoid unique constraint collisions
        int existingStressLotsCount = 0;
        if (dbContext.Database.IsNpgsql())
        {
            existingStressLotsCount = await dbContext.Database.SqlQueryRaw<int>(
                "SELECT COUNT(*)::int AS \"Value\" FROM production_lots WHERE lot_number LIKE 'LOT-STRESS-%'"
            ).FirstOrDefaultAsync();
        }
        else
        {
            existingStressLotsCount = (await dbContext.ProductionLots
                .IgnoreQueryFilters()
                .ToListAsync())
                .Count(l => l.LotNumber.Value.StartsWith("LOT-STRESS-"));
        }

        if (existingStressLotsCount > 0)
        {
            Console.WriteLine($"[INFO] Detected {existingStressLotsCount:N0} existing stress lots. Resetting stress data first...");
            await CleanStressTestDataAsync(dbContext);
        }

        // 5. Bulk Seeding
        int seededLotsCount = 0;
        int seededMovementsCount = 0;
        var now = DateTime.UtcNow;
        var itemId = Guid.NewGuid();

        if (dbContext.Database.IsNpgsql())
        {
            Console.WriteLine($"[4/5] High-Speed PostgreSQL Binary Streaming {totalLots:N0} lots & {totalMovements:N0} movements...");

            var conn = (NpgsqlConnection)dbContext.Database.GetDbConnection();
            if (conn.State != ConnectionState.Open)
            {
                await conn.OpenAsync();
            }

            var lotIds = new List<Guid>(totalLots);

            // Stream Lots
            using (var writer = await conn.BeginBinaryImportAsync(
                "COPY production_lots (id, tenant_id, lot_number, item_id, initial_step_id, status, created_at_utc, created_by, remarks, client_id, client_name, initial_quantity, unit) FROM STDIN (FORMAT BINARY)"))
            {
                for (int i = 1; i <= totalLots; i++)
                {
                    var lotId = Guid.NewGuid();
                    lotIds.Add(lotId);
                    var lotNum = $"LOT-STRESS-{i:D6}";
                    var client = clients.Count > 0 ? clients[(i - 1) % clients.Count] : null;

                    await writer.StartRowAsync();
                    await writer.WriteAsync(lotId, NpgsqlDbType.Uuid);
                    await writer.WriteAsync(tenantId, NpgsqlDbType.Uuid);
                    await writer.WriteAsync(lotNum, NpgsqlDbType.Varchar);
                    await writer.WriteAsync(itemId, NpgsqlDbType.Uuid);
                    await writer.WriteAsync(stepCut.Id, NpgsqlDbType.Uuid);
                    await writer.WriteAsync((int)LotStatus.Active, NpgsqlDbType.Integer);
                    await writer.WriteAsync(now.AddDays(-30), NpgsqlDbType.TimestampTz);
                    await writer.WriteAsync("StressSeeder", NpgsqlDbType.Varchar);
                    await writer.WriteAsync("Bulk stress test lot", NpgsqlDbType.Varchar);
                    if (client != null)
                    {
                        await writer.WriteAsync(client.Id, NpgsqlDbType.Uuid);
                        await writer.WriteAsync(client.Name, NpgsqlDbType.Varchar);
                    }
                    else
                    {
                        await writer.WriteNullAsync();
                        await writer.WriteNullAsync();
                    }
                    await writer.WriteAsync(10000m, NpgsqlDbType.Numeric);
                    await writer.WriteAsync((int)UnitType.Pieces, NpgsqlDbType.Integer);
                }

                await writer.CompleteAsync();
            }
            seededLotsCount = totalLots;
            Console.WriteLine($"      ✓ Completed streaming {totalLots:N0} production lots.");

            // Stream Movements
            using (var writer = await conn.BeginBinaryImportAsync(
                "COPY manufacturing_movements (id, tenant_id, production_lot_id, from_step_id, to_step_id, quantity, type, movement_date, created_at_utc, updated_at_utc, client_id, client_name, created_by, remarks, reference_number, is_reversed, reversal_movement_id, reversal_reason) FROM STDIN (FORMAT BINARY)"))
            {
                for (int i = 0; i < totalLots; i++)
                {
                    var lotId = lotIds[i];
                    var lotNum = $"LOT-STRESS-{(i + 1):D6}";
                    var client = clients.Count > 0 ? clients[i % clients.Count] : null;

                    // Initial Movement
                    await writer.StartRowAsync();
                    await writer.WriteAsync(Guid.NewGuid(), NpgsqlDbType.Uuid);
                    await writer.WriteAsync(tenantId, NpgsqlDbType.Uuid);
                    await writer.WriteAsync(lotId, NpgsqlDbType.Uuid);
                    await writer.WriteNullAsync(); // from_step_id
                    await writer.WriteAsync(stepCut.Id, NpgsqlDbType.Uuid);
                    await writer.WriteAsync(10000m, NpgsqlDbType.Numeric);
                    await writer.WriteAsync((int)MovementType.InitialProduction, NpgsqlDbType.Integer);
                    await writer.WriteAsync(now.AddDays(-30), NpgsqlDbType.TimestampTz);
                    await writer.WriteAsync(now.AddDays(-30), NpgsqlDbType.TimestampTz);
                    await writer.WriteAsync(now.AddDays(-30), NpgsqlDbType.TimestampTz);
                    if (client != null)
                    {
                        await writer.WriteAsync(client.Id, NpgsqlDbType.Uuid);
                        await writer.WriteAsync(client.Name, NpgsqlDbType.Varchar);
                    }
                    else
                    {
                        await writer.WriteNullAsync();
                        await writer.WriteNullAsync();
                    }
                    await writer.WriteAsync("Seeder", NpgsqlDbType.Varchar);
                    await writer.WriteAsync("Initial production lot creation", NpgsqlDbType.Varchar);
                    await writer.WriteAsync($"INIT-{lotNum}", NpgsqlDbType.Varchar);
                    await writer.WriteAsync(false, NpgsqlDbType.Boolean);
                    await writer.WriteNullAsync();
                    await writer.WriteNullAsync();

                    seededMovementsCount++;

                    // Subsequent Transfers
                    for (int m = 1; m < movementsPerLot; m++)
                    {
                        await writer.StartRowAsync();
                        await writer.WriteAsync(Guid.NewGuid(), NpgsqlDbType.Uuid);
                        await writer.WriteAsync(tenantId, NpgsqlDbType.Uuid);
                        await writer.WriteAsync(lotId, NpgsqlDbType.Uuid);
                        await writer.WriteAsync(stepCut.Id, NpgsqlDbType.Uuid);
                        await writer.WriteAsync(stepStitch.Id, NpgsqlDbType.Uuid);
                        await writer.WriteAsync(10m, NpgsqlDbType.Numeric);
                        await writer.WriteAsync((int)MovementType.StepTransfer, NpgsqlDbType.Integer);
                        await writer.WriteAsync(now.AddDays(-30).AddMinutes(m * 10), NpgsqlDbType.TimestampTz);
                        await writer.WriteAsync(now.AddDays(-30).AddMinutes(m * 10), NpgsqlDbType.TimestampTz);
                        await writer.WriteAsync(now.AddDays(-30).AddMinutes(m * 10), NpgsqlDbType.TimestampTz);
                        if (client != null)
                        {
                            await writer.WriteAsync(client.Id, NpgsqlDbType.Uuid);
                            await writer.WriteAsync(client.Name, NpgsqlDbType.Varchar);
                        }
                        else
                        {
                            await writer.WriteNullAsync();
                            await writer.WriteNullAsync();
                        }
                        await writer.WriteAsync("Seeder", NpgsqlDbType.Varchar);
                        await writer.WriteAsync($"Movement {m} transfer from {stepCut.Name} to {stepStitch.Name}", NpgsqlDbType.Varchar);
                        await writer.WriteAsync($"MOV-{lotNum}-{m:D3}", NpgsqlDbType.Varchar);
                        await writer.WriteAsync(false, NpgsqlDbType.Boolean);
                        await writer.WriteNullAsync();
                        await writer.WriteNullAsync();

                        seededMovementsCount++;
                    }

                    if ((i + 1) % 5000 == 0 || (i + 1) == totalLots)
                    {
                        double pct = (double)(i + 1) / totalLots * 100;
                        Console.WriteLine($"      Streamed {(i + 1):N0}/{totalLots:N0} lots ({seededMovementsCount:N0} movements) [{pct:F1}%]");
                    }
                }

                await writer.CompleteAsync();
            }
        }
        else
        {
            int batchSize = 1000;
            Console.WriteLine($"[4/5] EF Core bulk seeding {totalLots:N0} lots & {totalMovements:N0} movements in batches of {batchSize} lots...");

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
                dbContext.ChangeTracker.Clear();

                seededLotsCount += currentBatchLots;
                seededMovementsCount += currentBatchLots * movementsPerLot;

                double progress = (double)seededLotsCount / totalLots * 100;
                Console.WriteLine($"      Seeded {seededLotsCount:N0}/{totalLots:N0} lots ({seededMovementsCount:N0} movements) [{progress:F1}%]");
            }
        }

        stopwatch.Stop();
        Console.WriteLine($"\n[5/5] SUCCESS! Seeded {seededLotsCount:N0} lots & {seededMovementsCount:N0} movements in {stopwatch.Elapsed.TotalSeconds:F2} seconds.");
        Console.WriteLine($"=======================================================\n");
    }
}

