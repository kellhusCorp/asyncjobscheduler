namespace AsyncJobScheduler.Application.Enums;

/// <summary>
/// Defines the result of a cancel job request.
/// </summary>
public enum CancelJobResult
{
    NotFound,
    CancelRequested,
    AlreadyCompleted
}