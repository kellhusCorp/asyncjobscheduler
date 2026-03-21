using System.Collections.Concurrent;
using System.Diagnostics.CodeAnalysis;
using AsyncJobScheduler.Application.Interfaces;
using AsyncJobScheduler.Domain.Entities;

namespace AsyncJobScheduler.Infrastructure;

/// <summary>
/// Defines an in-memory job store.
/// </summary>
public sealed class InMemoryJobStore : IJobStore
{
    private readonly ConcurrentDictionary<Guid, Job> _jobs = new();

    public Job Add(Job job)
    {
        if (!_jobs.TryAdd(job.Id, job)) throw new InvalidOperationException("Job already exists");

        return job;
    }

    public bool TryGetJob(Guid id, [NotNullWhen(true)] out Job? job)
    {
        return _jobs.TryGetValue(id, out job);
    }

    public IReadOnlyCollection<Job> Jobs => Array.AsReadOnly(_jobs.ToArray().Select(x => x.Value).ToArray());

    public bool TryUpdate(Job job)
    {
        if (!_jobs.ContainsKey(job.Id)) throw new InvalidOperationException("Job does not exist");

        _jobs[job.Id] = job;

        return true;
    }
}