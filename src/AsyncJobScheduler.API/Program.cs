using AsyncJobScheduler.API.Dtos;
using AsyncJobScheduler.API.Validators;
using AsyncJobScheduler.Application.Enums;
using AsyncJobScheduler.Application.Interfaces;
using AsyncJobScheduler.Infrastructure.InMemory;
using FluentValidation;
using Microsoft.AspNetCore.Mvc;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddSingleton<IJobStore, JobStore>();
builder.Services.AddSingleton<IJobScheduler, JobScheduler>();
builder.Services.AddScoped<IValidator<CreateJobRequest>, CreateJobRequestValidator>();
builder.Services.AddOpenApi();

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
    app.MapOpenApi("/openapi/{documentName}.yaml");
}

app.MapPost("/api/jobs", async (
        [FromBody] CreateJobRequest request,
        [FromServices] IJobScheduler jobScheduler,
        [FromServices] IValidator<CreateJobRequest> validator) =>
    {
        var validationResult = await validator.ValidateAsync(request);

        if (!validationResult.IsValid)
        {
            return Results.ValidationProblem(validationResult.ToDictionary());
        }

        var job = jobScheduler.AddJob(request.Duration, request.ShouldFail, request.Timeout);

        return Results.Created($"/api/jobs/{job.Id}", job.ToResponse());
    }).Produces<JobResponse>(StatusCodes.Status201Created)
    .Produces(StatusCodes.Status400BadRequest);

app.MapGet("/api/jobs/{id:guid}", ([FromRoute] Guid id, [FromServices] IJobScheduler jobScheduler) =>
    {
        if (!jobScheduler.TryGetJob(id, out var job))
        {
            return Results.NotFound();
        }

        return Results.Ok(job.ToResponse());
    }).Produces<JobResponse>()
    .Produces(StatusCodes.Status404NotFound);

app.MapGet("/api/jobs", ([FromServices] IJobScheduler jobScheduler) => { return Results.Ok(jobScheduler.Jobs.Select(x => x.ToResponse())); })
    .Produces<IEnumerable<JobResponse>>();

app.MapDelete("/api/jobs/{id:guid}", ([FromRoute] Guid id, [FromServices] IJobScheduler jobScheduler) =>
    {
        var result = jobScheduler.CancelJob(id);

        if (result == CancelJobResult.NotFound)
        {
            return Results.NotFound();
        }

        if (result == CancelJobResult.AlreadyCompleted)
        {
            return Results.Conflict();
        }

        return Results.Accepted();
    })
    .Produces(StatusCodes.Status202Accepted)
    .Produces(StatusCodes.Status404NotFound)
    .Produces(StatusCodes.Status409Conflict);

app.Run();