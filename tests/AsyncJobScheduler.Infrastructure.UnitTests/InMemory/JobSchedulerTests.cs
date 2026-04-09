using System.Collections.Concurrent;
using AsyncJobScheduler.Domain.Enums;
using AsyncJobScheduler.Infrastructure.InMemory;
using AsyncJobScheduler.Infrastructure.Options;
using AsyncJobScheduler.Infrastructure.Workers;
using Microsoft.Extensions.Logging;

// ReSharper disable AccessToDisposedClosure

namespace AsyncJobScheduler.Infrastructure.UnitTests.InMemory;

internal struct Dummy;

public sealed class JobSchedulerTests
{
    [Fact]
    public async Task Test_Add_Concurrently_Jobs_Returns_Correct_Count()
    {
        var store = new JobStore();
        using var scheduler = new JobScheduler(store);
        const int count = 100;
        using var sync = new ManualResetEvent(false);
        var jobIds = new ConcurrentDictionary<Guid, Dummy>();
        
        var tasks = Enumerable.Range(0, count)
            .Select(_ => Task.Run(() =>
            {
                sync.WaitOne();
                
                for (var i = 0; i < count; i++)
                {
                    var job = scheduler.AddJob(TimeSpan.FromSeconds(10), false, null);
                    jobIds.TryAdd(job.Id, default);
                }

                return Task.CompletedTask;
            }))
            .ToArray();
        
        sync.Set();
        
        await Task.WhenAll(tasks);
        Assert.Equal(count * count, scheduler.Jobs.Count);

        foreach (var id in scheduler.Jobs.Select(x => x.Id))
        {
            jobIds.TryRemove(id, out _);
        }
        
        Assert.Empty(jobIds);
    }

    [Fact]
    public async Task Test_Add_Jobs_Should_Eventually_Succeed()
    {
        using var cts = new CancellationTokenSource();
        using var loggerFactory = LoggerFactory.Create(opt => opt.AddConsole());
        var timeout = TimeSpan.FromSeconds(10);
        var store = new JobStore();
        using var scheduler = new JobScheduler(store);
        using var worker = new JobWorker(scheduler, scheduler, loggerFactory.CreateLogger<JobWorker>(), new JobWorkerOptions()
        {
            MaxDegreeOfParallelism = 1
        }, doWork: DependencyInjection.DoWork);
        await worker.StartAsync(cts.Token);
        
        var job = scheduler.AddJob(timeout, false, null);
        var jobId = job.Id;
        
        await Eventually.AssertAsync(() =>
        {
            if (!scheduler.TryGetJob(jobId, out var job))
            {
                throw new Exception("Job not found");
            }
            
            return Task.FromResult(job.Status == JobStatus.Succeeded);
        }, timeout: timeout + TimeSpan.FromSeconds(1), ct: cts.Token);
    }
}