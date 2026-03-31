using System.Diagnostics.CodeAnalysis;
using AsyncJobScheduler.Application.Enums;
using AsyncJobScheduler.Domain.Entities;

namespace AsyncJobScheduler.Application.Interfaces;

/// <summary>
/// Defines a job scheduler.
/// </summary>
public interface IJobScheduler
{
    IReadOnlyCollection<Job> Jobs { get; }

    Job AddJob(TimeSpan duration, bool shouldFail, TimeSpan? timeout);

    bool TryGetJob(Guid id, [NotNullWhen(true)] out Job? job);

    CancelJobResult CancelJob(Guid id);

    Task<Job?> WaitForCompletionAsync(Guid id, CancellationToken ct);
    
    Task<Guid> DequeueAsync(CancellationToken ct);
    
    bool TryGetInfo(Guid jobId, out JobInfo? info);
    
    void Complete(Job job);
}