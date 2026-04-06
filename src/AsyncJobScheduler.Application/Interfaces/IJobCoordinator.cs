using AsyncJobScheduler.Domain.Entities;

namespace AsyncJobScheduler.Application.Interfaces;

public interface IJobCoordinator
{
    Task<Guid> DequeueAsync(CancellationToken ct);
    
    void Complete(Job job);
    
    bool TryGetInfo(Guid jobId, out JobInfo? info);
}