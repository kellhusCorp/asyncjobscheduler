using System.ComponentModel.DataAnnotations;
using AsyncJobScheduler.Domain.Entities;
using AsyncJobScheduler.Domain.Enums;

namespace AsyncJobScheduler.API.Dtos;

public sealed class CreateJobRequest
{
    [Required]
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

    public static Job ToModel(this CreateJobRequest request)
    {
        return new Job
        {
            Id = Guid.NewGuid(),
            CreatedAt = DateTime.UtcNow,
            Duration = request.Duration,
            ShouldFail = request.ShouldFail,
            Status = JobStatus.Created,
            Timeout = request.Timeout
        };
    }
}