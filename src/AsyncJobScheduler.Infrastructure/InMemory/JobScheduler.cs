using System.Collections.Concurrent;
using System.Diagnostics.CodeAnalysis;
using AsyncJobScheduler.Application;
using AsyncJobScheduler.Application.Enums;
using AsyncJobScheduler.Application.Interfaces;
using AsyncJobScheduler.Domain.Entities;
using AsyncJobScheduler.Domain.Enums;

namespace AsyncJobScheduler.Infrastructure.InMemory;

/// <summary>
/// Defines an in-memory job scheduler.
/// </summary>
public sealed class JobScheduler : IJobScheduler, IDisposable
{
    private readonly ConcurrentDictionary<Guid, JobInfo> _jobs = new();

    private readonly ConcurrentQueue<Guid> _queue = new();

    private readonly SemaphoreSlim _semaphore = new(0);

    private readonly IJobStore _store;

    public JobScheduler(IJobStore store)
    {
        _store = store;
    }

    public void Dispose()
    {
        _semaphore.Dispose();

        foreach (var jobInfo in _jobs)
        {
            jobInfo.Value.Dispose();
        }

        _jobs.Clear();
    }

    public IReadOnlyCollection<Job> Jobs => _store.Jobs;

    public Job AddJob(TimeSpan duration, bool shouldFail, TimeSpan? timeout)
    {
        var job = new Job
        {
            Id = Guid.NewGuid(),
            Status = JobStatus.Created,
            CreatedAt = DateTime.UtcNow,
            Duration = duration,
            ShouldFail = shouldFail,
            Timeout = timeout
        };

        _store.Add(job);
        _jobs[job.Id] = new JobInfo();
        Enqueue(job.Id);
        return job;
    }

    public bool TryGetJob(Guid id, [NotNullWhen(true)] out Job? job)
    {
        return _store.TryGetJob(id, out job);
    }

    public CancelJobResult CancelJob(Guid id)
    {
        if (!_store.TryGetJob(id, out var job))
        {
            return CancelJobResult.NotFound;
        }

        if (job.IsTerminal)
        {
            return CancelJobResult.AlreadyCompleted;
        }

        if (!_jobs.TryGetValue(id, out var jobInfo))
        {
            return CancelJobResult.AlreadyCompleted;
        }

        jobInfo.CancellationSource.Cancel();
        return CancelJobResult.CancelRequested;
    }

    public async Task<Job?> WaitForCompletionAsync(Guid id, CancellationToken ct)
    {
        while (true)
        {
            ct.ThrowIfCancellationRequested();

            if (!_store.TryGetJob(id, out var job))
            {
                return job;
            }

            if (job.IsTerminal)
            {
                return job;
            }

            if (_jobs.TryGetValue(id, out var jobInfo))
            {
                return await jobInfo.CompletionSource.Task.WaitAsync(ct);
            }

            await Task.Delay(50, ct);
        }
    }

    private void Enqueue(Guid id)
    {
        _queue.Enqueue(id);
        _semaphore.Release();

        if (_store.TryGetJob(id, out var job))
        {
            job.Status = JobStatus.Queued;
            _store.TryUpdate(job);
        }
    }

    public async Task<Guid> DequeueAsync(CancellationToken ct)
    {
        while (true)
        {
            await _semaphore.WaitAsync(ct);
            if (_queue.TryDequeue(out var id))
            {
                return id;
            }
        }
    }

    public bool TryGetInfo(Guid jobId, out JobInfo? info)
    {
        var found = _jobs.TryGetValue(jobId, out var jobInfo);
        info = jobInfo;
        return found;
    }

    public void Complete(Job job)
    {
        _store.TryUpdate(job);

        if (_jobs.TryRemove(job.Id, out var jobInfo))
        {
            jobInfo.CompletionSource.TrySetResult(job);
            jobInfo.Dispose();
        }
    }
}