using System.Diagnostics.CodeAnalysis;
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
    
    void CancelJob(Guid id);
}