using AsyncJobScheduler.API.Dtos;
using FluentValidation;

namespace AsyncJobScheduler.API.Validators;

internal sealed class CreateJobRequestValidator : AbstractValidator<CreateJobRequest>
{
    public CreateJobRequestValidator()
    {
        RuleFor(x => x.Duration)
            .GreaterThan(TimeSpan.Zero);
        RuleFor(x => x.Timeout)
            .GreaterThan(TimeSpan.Zero)
            .When(x => x.Timeout.HasValue);
    }
}