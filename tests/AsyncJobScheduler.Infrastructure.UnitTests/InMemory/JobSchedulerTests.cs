using System.Collections.Concurrent;
using AsyncJobScheduler.Infrastructure.InMemory;
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
}