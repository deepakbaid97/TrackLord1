using System;
using System.IO;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using TrackLord.Application.Commands;
using TrackLord.Domain.Common;
using TrackLord.Domain.Repositories;
using TrackLord.Domain.Services;
using TrackLord.Infrastructure.Persistence;
using TrackLord.Infrastructure.Persistence.Repositories;
using TrackLord.Infrastructure.Services;

namespace TrackLord.StressTest;

public class Program
{
    public static async Task Main(string[] args)
    {
        Console.WriteLine("=======================================================");
        Console.WriteLine("        TrackLord Backend Stress Testing Tool          ");
        Console.WriteLine("=======================================================");

        var configuration = new ConfigurationBuilder()
            .SetBasePath(Directory.GetCurrentDirectory())
            .AddJsonFile("appsettings.json", optional: true)
            .AddEnvironmentVariables()
            .Build();

        var services = new ServiceCollection();

        var connectionString = configuration.GetConnectionString("DefaultConnection");
        Console.WriteLine($"[CONFIG] Connection String: {connectionString}");
        services.AddDbContext<TrackLordDbContext>(options =>
        {
            if (!string.IsNullOrWhiteSpace(connectionString) && !connectionString.Contains("InMemory", StringComparison.OrdinalIgnoreCase))
            {
                options.UseNpgsql(connectionString);
            }
            else
            {
                options.UseInMemoryDatabase("TrackLordStressTestDb");
            }
        });

        services.AddHttpContextAccessor();
        services.AddScoped<ITenantContext, TenantContext>();
        services.AddSingleton<ILedgerReplayEngine, LedgerReplayEngine>();
        services.AddSingleton<IUnitConverterService, UnitConverterService>();

        services.AddScoped<ITenantRepository, TenantRepository>();
        services.AddScoped<IProductionLotRepository, ProductionLotRepository>();
        services.AddScoped<IMaterialLotRepository, MaterialLotRepository>();
        services.AddScoped<IManufacturingMovementRepository, ManufacturingMovementRepository>();
        services.AddScoped<IManufacturingStepRepository, ManufacturingStepRepository>();
        services.AddScoped<IMaterialConsumptionRepository, MaterialConsumptionRepository>();
        services.AddScoped<IRejectionRecordRepository, RejectionRecordRepository>();
        services.AddScoped<IClientRepository, ClientRepository>();
        services.AddScoped<IUserRepository, UserRepository>();

        services.AddLogging();
        services.AddMediatR(cfg => cfg.RegisterServicesFromAssembly(typeof(CreateProductionLotCommand).Assembly));

        var serviceProvider = services.BuildServiceProvider();

        using (var scope = serviceProvider.CreateScope())
        {
            var dbContext = scope.ServiceProvider.GetRequiredService<TrackLordDbContext>();
            if (dbContext.Database.IsNpgsql())
            {
                dbContext.Database.Migrate();
            }
            else
            {
                dbContext.Database.EnsureCreated();
            }
        }

        while (true)
        {
            Console.WriteLine("\nSelect an action:");
            Console.WriteLine("  [1] Bulk Seed 2,000,000 Movements (40,000 Lots x 50 Movements)");
            Console.WriteLine("  [2] Bulk Seed 500,000 Movements (10,000 Lots x 50 Movements)");
            Console.WriteLine("  [3] Bulk Seed 50,000 Movements (1,000 Lots x 50 Movements)");
            Console.WriteLine("  [0] Clean / Reset Stress Test Data");
            Console.WriteLine("  [4] Run Dashboard Stress Test (20 Parallel Workers)");
            Console.WriteLine("  [5] Run Single Lot Journey Query Test against 2 Million Rows (20 Workers)");
            Console.WriteLine("  [6] Exit");
            Console.Write("\nChoice > ");

            var input = Console.ReadLine()?.Trim();
            if (input == "0")
            {
                using var scope = serviceProvider.CreateScope();
                var dbContext = scope.ServiceProvider.GetRequiredService<TrackLordDbContext>();
                await DbSeeder.CleanStressTestDataAsync(dbContext);
            }
            else if (input == "1")
            {
                using var scope = serviceProvider.CreateScope();
                var dbContext = scope.ServiceProvider.GetRequiredService<TrackLordDbContext>();
                await DbSeeder.SeedDatabaseAsync(dbContext, totalLots: 40000, movementsPerLot: 50);
            }
            else if (input == "2")
            {
                using var scope = serviceProvider.CreateScope();
                var dbContext = scope.ServiceProvider.GetRequiredService<TrackLordDbContext>();
                await DbSeeder.SeedDatabaseAsync(dbContext, totalLots: 10000, movementsPerLot: 50);
            }
            else if (input == "3")
            {
                using var scope = serviceProvider.CreateScope();
                var dbContext = scope.ServiceProvider.GetRequiredService<TrackLordDbContext>();
                await DbSeeder.SeedDatabaseAsync(dbContext, totalLots: 1000, movementsPerLot: 50);
            }
            else if (input == "4")
            {
                await LoadRunner.RunDashboardStressTestAsync(serviceProvider, concurrencyLevel: 20, durationSeconds: 15);
            }
            else if (input == "5")
            {
                await LoadRunner.RunSingleLotStressTestAsync(serviceProvider, concurrencyLevel: 20, durationSeconds: 15);
            }
            else if (input == "6" || input?.ToLower() == "exit")
            {
                Console.WriteLine("Exiting stress test tool.");
                break;
            }
            else
            {
                Console.WriteLine("Invalid option. Try again.");
            }
        }
    }
}
