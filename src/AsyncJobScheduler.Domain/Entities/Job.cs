using AsyncJobScheduler.Domain.Enums;

namespace AsyncJobScheduler.Domain.Entities;

/// <summary>
/// Defines a job.
/// </summary>
public sealed class Job
{
    public Guid Id { get; set; }
    
    public JobStatus Status { get; set; }
    
    public DateTime CreatedAt { get; set; }
    
    public DateTime? StartedAt { get; set; }
    
    public DateTime? FinishedAt { get; set; }
    
    public double ProgressPoints { get; set; }
    
    public string? ErrorMessage { get; set; }

    #region It'll be defined by the user

    public TimeSpan Duration { get; init; }
    
    public bool ShouldFail { get; init; }
    
    public TimeSpan? Timeout { get; init; }

    #endregion
}