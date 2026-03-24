using System.Collections;
using AsyncJobScheduler.API.Dtos;
using AsyncJobScheduler.API.Validators;
using AsyncJobScheduler.Application.Interfaces;
using AsyncJobScheduler.Infrastructure;
using FluentValidation;
using Microsoft.AspNetCore.Mvc;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddSingleton<IJobStore, InMemoryJobStore>();
builder.Services.AddScoped<IValidator<CreateJobRequest>, CreateJobRequestValidator>();
builder.Services.AddOpenApi();

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.MapPost("/api/jobs", async (
    [FromBody] CreateJobRequest request,
    [FromServices] IJobStore jobStore,
    [FromServices] IValidator<CreateJobRequest> validator) =>
{
    var validationResult = await validator.ValidateAsync(request);

    if (!validationResult.IsValid)
    {
        return Results.ValidationProblem(validationResult.ToDictionary());
    }

    var job = request.ToModel();

    jobStore.Add(job);

    return Results.Created($"/api/jobs/{job.Id}", job.ToResponse());
}).Produces<JobResponse>(StatusCodes.Status201Created);

app.MapGet("/api/jobs/{id:guid}", ([FromRoute] Guid id, [FromServices] IJobStore jobStore) =>
{
    if (!jobStore.TryGetJob(id, out var job))
    {
        return Results.NotFound();
    }

    return Results.Ok(job.ToResponse());
}).Produces<JobResponse>();

app.MapGet("/api/jobs", ([FromServices] IJobStore jobStore) => { return Results.Ok(jobStore.Jobs.Select(x => x.ToResponse())); })
    .Produces<IEnumerable<JobResponse>>();

app.Run();