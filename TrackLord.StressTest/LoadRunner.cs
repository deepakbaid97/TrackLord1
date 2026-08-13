using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using MediatR;
using Microsoft.Extensions.DependencyInjection;
using TrackLord.Application.Queries;
using TrackLord.Domain.Repositories;

namespace TrackLord.StressTest;

public static class LoadRunner
{
    public static async Task RunDashboardStressTestAsync(IServiceProvider serviceProvider, int concurrencyLevel = 20, int durationSeconds = 15)
    {
        Console.WriteLine($"\n=======================================================");
        Console.WriteLine($" [LOAD RUNNER] Starting Dashboard Stress Test...");
        Console.WriteLine($" Concurrency: {concurrencyLevel} parallel workers");
        Console.WriteLine($" Duration:    {durationSeconds} seconds");
        Console.WriteLine($" Target:      GetDashboardOverviewQuery");
        Console.WriteLine($"=======================================================\n");

        await ExecuteBenchmarkAsync(serviceProvider, concurrencyLevel, durationSeconds, scope =>
        {
            var mediator = scope.ServiceProvider.GetRequiredService<IMediator>();
            return mediator.Send(new GetDashboardOverviewQuery());
        });
    }

    public static async Task RunSingleLotStressTestAsync(IServiceProvider serviceProvider, int concurrencyLevel = 20, int durationSeconds = 15)
    {
        Guid targetLotId;
        using (var scope = serviceProvider.CreateScope())
        {
            var lotRepo = scope.ServiceProvider.GetRequiredService<IProductionLotRepository>();
            var lots = await lotRepo.GetAllAsync();
            if (lots.Count == 0)
            {
                Console.WriteLine("[Error] No production lots found in DB. Please seed database first!");
                return;
            }
            targetLotId = lots[0].Id;
        }

        Console.WriteLine($"\n=======================================================");
        Console.WriteLine($" [LOAD RUNNER] Starting Single Lot Journey Query Test...");
        Console.WriteLine($" Target Lot ID: {targetLotId}");
        Console.WriteLine($" Concurrency:   {concurrencyLevel} parallel workers");
        Console.WriteLine($" Duration:      {durationSeconds} seconds");
        Console.WriteLine($" Target:        GetLotJourneyQuery(ProductionLotId)");
        Console.WriteLine($"=======================================================\n");

        await ExecuteBenchmarkAsync(serviceProvider, concurrencyLevel, durationSeconds, scope =>
        {
            var mediator = scope.ServiceProvider.GetRequiredService<IMediator>();
            return mediator.Send(new GetLotJourneyQuery(targetLotId));
        });
    }

    private static async Task ExecuteBenchmarkAsync(
        IServiceProvider serviceProvider,
        int concurrencyLevel,
        int durationSeconds,
        Func<IServiceScope, Task> action)
    {
        GC.Collect();
        GC.WaitForPendingFinalizers();
        long initialMemoryBytes = GC.GetTotalMemory(true);

        var latenciesMs = new ConcurrentBag<double>();
        var cancellationTokenSource = new CancellationTokenSource(TimeSpan.FromSeconds(durationSeconds));
        var token = cancellationTokenSource.Token;

        var overallStopwatch = Stopwatch.StartNew();
        int errorCount = 0;

        var tasks = new List<Task>();

        for (int w = 0; w < concurrencyLevel; w++)
        {
            tasks.Add(Task.Run(async () =>
            {
                while (!token.IsCancellationRequested)
                {
                    var reqStopwatch = Stopwatch.StartNew();
                    try
                    {
                        using (var scope = serviceProvider.CreateScope())
                        {
                            await action(scope);
                        }

                        reqStopwatch.Stop();
                        latenciesMs.Add(reqStopwatch.Elapsed.TotalMilliseconds);
                    }
                    catch (OperationCanceledException)
                    {
                        break;
                    }
                    catch (Exception ex)
                    {
                        reqStopwatch.Stop();
                        Interlocked.Increment(ref errorCount);
                        Console.WriteLine($"[Error] Query failed: {ex.Message}");
                    }
                }
            }));
        }

        await Task.WhenAll(tasks);
        overallStopwatch.Stop();

        long peakMemoryBytes = GC.GetTotalMemory(false);

        var sortedLatencies = latenciesMs.OrderBy(x => x).ToList();
        int totalRequests = sortedLatencies.Count;
        double rps = totalRequests / overallStopwatch.Elapsed.TotalSeconds;

        double avgLatency = totalRequests > 0 ? sortedLatencies.Average() : 0;
        double minLatency = totalRequests > 0 ? sortedLatencies.First() : 0;
        double maxLatency = totalRequests > 0 ? sortedLatencies.Last() : 0;

        double p50 = GetPercentile(sortedLatencies, 0.50);
        double p95 = GetPercentile(sortedLatencies, 0.95);
        double p99 = GetPercentile(sortedLatencies, 0.99);

        double memoryUsedMb = (peakMemoryBytes - initialMemoryBytes) / (1024.0 * 1024.0);
        double totalAllocatedRamMb = peakMemoryBytes / (1024.0 * 1024.0);

        Console.WriteLine($"\n=======================================================");
        Console.WriteLine($"                 STRESS TEST RESULTS                   ");
        Console.WriteLine($"=======================================================");
        Console.WriteLine($" Total Requests Executed: {totalRequests:N0}");
        Console.WriteLine($" Failed Requests:         {errorCount:N0}");
        Console.WriteLine($" Total Elapsed Time:       {overallStopwatch.Elapsed.TotalSeconds:F2} seconds");
        Console.WriteLine($" Throughput (RPS):        {rps:F2} req/sec");
        Console.WriteLine($"-------------------------------------------------------");
        Console.WriteLine($" Min Latency:             {minLatency:F2} ms");
        Console.WriteLine($" Avg Latency:             {avgLatency:F2} ms");
        Console.WriteLine($" P50 Latency (Median):    {p50:F2} ms");
        Console.WriteLine($" P95 Latency:             {p95:F2} ms");
        Console.WriteLine($" P99 Latency:             {p99:F2} ms");
        Console.WriteLine($" Max Latency:             {maxLatency:F2} ms");
        Console.WriteLine($"-------------------------------------------------------");
        Console.WriteLine($" RAM Allocated Total:     {totalAllocatedRamMb:F2} MB");
        Console.WriteLine($" RAM Growth During Test:  {memoryUsedMb:F2} MB");
        Console.WriteLine($"=======================================================\n");
    }

    private static double GetPercentile(List<double> sortedValues, double percentile)
    {
        if (sortedValues.Count == 0) return 0;
        int index = (int)Math.Ceiling(percentile * sortedValues.Count) - 1;
        index = Math.Clamp(index, 0, sortedValues.Count - 1);
        return sortedValues[index];
    }
}
