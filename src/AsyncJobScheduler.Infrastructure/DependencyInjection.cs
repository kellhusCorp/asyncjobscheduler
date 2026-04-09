using AsyncJobScheduler.Application.Interfaces;
using AsyncJobScheduler.Domain.Entities;
using AsyncJobScheduler.Infrastructure.Options;
using AsyncJobScheduler.Infrastructure.Workers;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace AsyncJobScheduler.Infrastructure;

public static class DependencyInjection
{
    internal static async Task DoWork(Job job, IProgress<double> progress, CancellationToken ct)
    {
        const int ticksInSec = 5;
        
        var steps = Math.Max((int)Math.Ceiling(job.Duration.TotalSeconds * ticksInSec), 1);
        var delay = TimeSpan.FromMilliseconds(job.Duration.TotalMilliseconds / steps);
                
        for (var i = 1; i <= steps; i++)
        {
            ct.ThrowIfCancellationRequested();

            await Task.Delay(delay, ct);

            var points = Math.Round(i / (double)steps, 2);

            progress.Report(points);

            if (job.ShouldFail && i >= steps / 2)
            {
                throw new Exception("Job failed");
            }
        }
    }
    
    public static IServiceCollection AddInfrastructureServices(this IServiceCollection services, IConfiguration configuration)
    {
        return services;
    }

    public static IServiceCollection AddWorkers(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddHostedService<JobWorker>(sp =>
        {
            var scheduler = sp.GetRequiredService<IJobScheduler>();
            var coordinator = sp.GetRequiredService<IJobCoordinator>();
            var logger = sp.GetRequiredService<ILogger<JobWorker>>();
            var options = sp.GetRequiredService<IOptions<JobWorkerOptions>>();

            return new JobWorker(scheduler, coordinator, logger, options.Value, DoWork);
        });
        
        return services;
    }
}