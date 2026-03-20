namespace AsyncJobScheduler.Domain.Enums;

/// <summary>
/// Describes the status of a job.
/// </summary>
public enum JobStatus
{
    Created,
    Queued,
    Running,
    Succeeded,
    Failed,
    Cancelled,
    TimedOut
}