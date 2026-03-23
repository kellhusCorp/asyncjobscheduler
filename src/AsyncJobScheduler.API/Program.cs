using AsyncJobScheduler.API.Dtos;
using AsyncJobScheduler.Application.Interfaces;
using AsyncJobScheduler.Infrastructure;
using Microsoft.AspNetCore.Mvc;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddSingleton<IJobStore, InMemoryJobStore>();

builder.Services.AddOpenApi();

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.MapPost("/api/jobs", ([FromBody] CreateJobRequest request, [FromServices] IJobStore jobStore) =>
{
    var job = request.ToModel();

    jobStore.Add(job);

    return Results.Created($"/api/jobs/{job.Id}", job.ToResponse());
});

app.MapGet("/api/jobs/{id:guid}", ([FromRoute] Guid id, [FromServices] IJobStore jobStore) =>
{
    if (!jobStore.TryGetJob(id, out var job))
    {
        return Results.NotFound();
    }

    return Results.Ok(job.ToResponse());
});

app.MapGet("/api/jobs", ([FromServices] IJobStore jobStore) =>
{
    return Results.Ok(jobStore.Jobs.Select(x => x.ToResponse()));
});

app.Run();