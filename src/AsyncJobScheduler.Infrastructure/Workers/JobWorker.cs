using AsyncJobScheduler.Application.Interfaces;
using AsyncJobScheduler.Domain.Entities;
using AsyncJobScheduler.Domain.Enums;
using AsyncJobScheduler.Infrastructure.Options;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace AsyncJobScheduler.Infrastructure.Workers;

public sealed class JobWorker : BackgroundService
{
    private readonly IJobScheduler _jobScheduler;
    private readonly Lock _lock = new();
    private readonly ILogger<JobWorker> _logger;
    private readonly Func<Job, CancellationToken, Task> _doWork;
    private readonly SemaphoreSlim _semaphore;
    private readonly List<Task> _tasks = new();

    public JobWorker(IJobScheduler jobScheduler, ILogger<JobWorker> logger, JobWorkerOptions options, Func<Job, CancellationToken, Task> doWork)
    {
        _jobScheduler = jobScheduler;
        _logger = logger;
        _doWork = doWork;
        _semaphore = new SemaphoreSlim(options.MaxDegreeOfParallelism);
    }

    protected override async Task ExecuteAsync(CancellationToken ct)
    {
        _logger.LogTrace("Job worker has started");

        try
        {
            while (!ct.IsCancellationRequested)
            {
                var jobId = await _jobScheduler.DequeueAsync(ct);

                await _semaphore.WaitAsync(ct);

                var task = RunAsync(jobId, ct);

                lock (_lock)
                {
                    _tasks.Add(task);
                }

                _ = task.ContinueWith(t =>
                {
                    lock (_lock)
                    {
                        _tasks.Remove(t);
                    }
                }, TaskScheduler.Default);
            }
        }
        catch (OperationCanceledException)
        {
            _logger.LogTrace("Job worker is stopping...");
        }

        Task[] snapshot;

        lock (_lock)
        {
            snapshot = _tasks.ToArray();
        }

        await Task.WhenAll(snapshot);

        _logger.LogTrace("Job worker has stopped");
    }

    private async Task RunAsync(Guid jobId, CancellationToken ct)
    {
        try
        {
            await ProcessJobAsync(jobId, ct);
        }
        finally
        {
            _semaphore.Release();
        }
    }

    private async Task ProcessJobAsync(Guid jobId, CancellationToken ct)
    {
        if (!_jobScheduler.TryGetJob(jobId, out var job))
        {
            return;
        }

        if (!_jobScheduler.TryGetInfo(jobId, out var jobInfo) || jobInfo == null)
        {
            return;
        }

        using var timeoutCts = job.Timeout.HasValue ? new CancellationTokenSource(job.Timeout.Value) : null;

        using var linkedCts = CreateLinkedCts(timeoutCts?.Token, jobInfo.CancellationSource.Token, ct);

        var linkedToken = linkedCts.Token;

        try
        {
            job.Status = JobStatus.Running;
            job.StartedAt = DateTime.UtcNow;
            _logger.LogTrace("Job {jobId} has started", jobId);

            await _doWork(job, linkedToken);

            job.Status = JobStatus.Succeeded;
            job.ProgressPoints = 1d;
            job.FinishedAt = DateTime.UtcNow;

            _logger.LogTrace("Job {jobId} has succeeded", jobId);
        }
        catch (OperationCanceledException) when (jobInfo.CancelRequested)
        {
            job.Status = JobStatus.Cancelled;
            job.ErrorMessage = "Canceled by the user";
            job.FinishedAt = DateTime.UtcNow;

            _logger.LogTrace("Job {jobId} has canceled by the user", jobId);
        }
        catch (OperationCanceledException) when (timeoutCts?.IsCancellationRequested == true)
        {
            job.Status = JobStatus.TimedOut;
            job.ErrorMessage = "Timed out";
            job.FinishedAt = DateTime.UtcNow;
            _logger.LogTrace("Job {jobId} has timed out", jobId);
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            job.Status = JobStatus.Cancelled;
            job.ErrorMessage = "Canceled because of host is stopping";
            job.FinishedAt = DateTime.UtcNow;
        }
        catch (Exception e)
        {
            job.Status = JobStatus.Failed;
            job.ErrorMessage = e.Message;
            job.FinishedAt = DateTime.UtcNow;

            _logger.LogError(e, "Job {jobId} has failed", jobId);
        }
        finally
        {
            _jobScheduler.Complete(job);
        }
    }

    private static CancellationTokenSource CreateLinkedCts(
        CancellationToken? timeoutToken,
        CancellationToken jobToken,
        CancellationToken runtimeToken)
    {
        if (!timeoutToken.HasValue)
        {
            return CancellationTokenSource.CreateLinkedTokenSource(jobToken, runtimeToken);
        }

        return CancellationTokenSource.CreateLinkedTokenSource(jobToken, timeoutToken.Value, runtimeToken);
    }

    public override void Dispose()
    {
        _semaphore.Dispose();
        base.Dispose();
    }
}