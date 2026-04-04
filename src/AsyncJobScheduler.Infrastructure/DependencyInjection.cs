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
    public static IServiceCollection AddInfrastructureServices(this IServiceCollection services, IConfiguration configuration)
    {
        return services;
    }

    public static IServiceCollection AddWorkers(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddHostedService<JobWorker>(sp =>
        {
            const int ticksInSec = 5;
            
            var scheduler = sp.GetRequiredService<IJobScheduler>();
            var logger = sp.GetRequiredService<ILogger<JobWorker>>();
            var options = sp.GetRequiredService<IOptions<JobWorkerOptions>>();

            return new JobWorker(scheduler, logger, options.Value, DoWork);

            static async Task DoWork(Job job, CancellationToken ct)
            {
                var steps = (int)job.Duration.TotalSeconds * ticksInSec;
                var delay = job.Duration / steps;
                for (var i = 1; i <= steps; i++)
                {
                    ct.ThrowIfCancellationRequested();

                    await Task.Delay(delay, ct);

                    job.ProgressPoints = Math.Round(i / (double)steps, 2);

                    if (job.ShouldFail && i >= steps / 2)
                    {
                        throw new Exception("Job failed");
                    }
                }
            }
        });
        
        return services;
    }
}