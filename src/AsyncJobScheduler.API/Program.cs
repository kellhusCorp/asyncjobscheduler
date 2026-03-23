using AsyncJobScheduler.API.Dtos;
using Microsoft.AspNetCore.Mvc;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddOpenApi();

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.MapPost("/api/jobs", ([FromBody]CreateJobRequest request) =>
{
    // todo implement

    var job = request.ToModel();
    
    return Results.Created($"/api/jobs/{job.Id}", job.ToResponse());
}); 

app.Run();