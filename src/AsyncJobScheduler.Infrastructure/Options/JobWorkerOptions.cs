namespace AsyncJobScheduler.Infrastructure.Options;

public sealed class JobWorkerOptions
{
    public int MaxDegreeOfParallelism { get; init; } = 2;
}