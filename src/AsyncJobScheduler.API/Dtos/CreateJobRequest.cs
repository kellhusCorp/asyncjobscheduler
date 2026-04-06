using AsyncJobScheduler.Domain.Entities;

namespace AsyncJobScheduler.API.Dtos;

public sealed class CreateJobRequest
{
    public TimeSpan Duration { get; init; }

    public bool ShouldFail { get; init; }
    
    public TimeSpan? Timeout { get; init; }
}

public sealed record JobResponse(
    Guid Id,
    string Status,
    double Progress,
    DateTimeOffset CreatedAt,
    DateTimeOffset? StartedAt,
    DateTimeOffset? FinishedAt,
    string? Error
);

public static class JobMapping
{
    public static JobResponse ToResponse(this Job job)
    {
        return new JobResponse(
            job.Id,
            job.Status.ToString(),
            job.ProgressPoints,
            job.CreatedAt,
            job.StartedAt,
            job.FinishedAt,
            job.ErrorMessage
        );
    }
}