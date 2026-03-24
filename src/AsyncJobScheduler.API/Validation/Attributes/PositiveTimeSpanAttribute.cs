using System.ComponentModel.DataAnnotations;

namespace AsyncJobScheduler.API.Validation.Attributes;

internal sealed class PositiveTimeSpanAttribute : ValidationAttribute
{
    public override bool IsValid(object? value)
    {
        if (value == null)
        {
            return true;
        }
        
        return value is TimeSpan ts && ts > TimeSpan.Zero;
    }

    public override string FormatErrorMessage(string name)
    {
        return $"{name} must be greater than 00:00:00";
    }
}