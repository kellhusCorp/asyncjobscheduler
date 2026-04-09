namespace AsyncJobScheduler.Infrastructure.UnitTests;

public static class Eventually
{
    public static async Task AssertAsync(Func<Task<bool>> condition, TimeSpan? timeout = null, TimeSpan? pollInterval = null, CancellationToken ct = default)
    {
        var actualTimeout = timeout ?? TimeSpan.FromSeconds(10);
        var actualPollInterval = pollInterval ?? TimeSpan.FromMilliseconds(100);
        
        var deadline = DateTime.UtcNow + actualTimeout;

        while (DateTime.UtcNow < deadline)
        {
            if (await condition())
            {
                return;
            }
            
            await Task.Delay(actualPollInterval, ct);
        }
        
        throw new TimeoutException();
    }
}