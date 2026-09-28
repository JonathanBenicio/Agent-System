namespace AgenticSystem.Core.Exceptions;

/// <summary>
/// Thrown when a cron expression is syntactically invalid or cannot be parsed
/// by the Quartz scheduler engine. Callers must surface this to the user as a
/// 422 Unprocessable Entity response rather than silently swallowing it.
/// </summary>
public sealed class InvalidCronExpressionException : Exception
{
    public string CronExpression { get; }

    public InvalidCronExpressionException(string cronExpression, string message, Exception? innerException = null)
        : base(message, innerException)
    {
        CronExpression = cronExpression;
    }
}
