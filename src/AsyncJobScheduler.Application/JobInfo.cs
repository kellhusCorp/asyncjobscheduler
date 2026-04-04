using AsyncJobScheduler.Domain.Entities;

namespace AsyncJobScheduler.Application;

public sealed class JobInfo : IDisposable
{
    public JobInfo()
    {
        CancellationSource = new CancellationTokenSource();
        CompletionSource = new TaskCompletionSource<Job>(TaskCreationOptions.RunContinuationsAsynchronously);
    }

    public TaskCompletionSource<Job> CompletionSource { get; }

    public CancellationTokenSource CancellationSource { get; }

    public bool CancelRequested => CancellationSource.IsCancellationRequested;

    public void Dispose()
    {
        CancellationSource.Dispose();
    }
}