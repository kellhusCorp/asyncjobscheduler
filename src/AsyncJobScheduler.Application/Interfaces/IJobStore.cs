using System.Diagnostics.CodeAnalysis;
using AsyncJobScheduler.Domain.Entities;

namespace AsyncJobScheduler.Application.Interfaces;

/// <summary>
/// Defines a job store.
/// </summary>
public interface IJobStore
{
    Job Add(Job job);
    
    bool TryGetJob(Guid id, [NotNullWhen(true)] out Job? job);
    
    IReadOnlyCollection<Job> Jobs { get; }
    
    bool TryUpdate(Job job);
}