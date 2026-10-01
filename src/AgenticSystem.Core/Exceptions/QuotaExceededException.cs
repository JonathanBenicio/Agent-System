namespace AgenticSystem.Core.Exceptions;

public sealed class QuotaExceededException : Exception
{
    public QuotaExceededException(string reason)
        : base(string.IsNullOrWhiteSpace(reason) ? "Quota Exceeded" : "Quota Exceeded: " + reason)
    {
    }

    public static QuotaExceededException? Find(Exception exception)
    {
        ArgumentNullException.ThrowIfNull(exception);
        var pending = new Stack<Exception>();
        pending.Push(exception);

        while (pending.TryPop(out var current))
        {
            if (current is QuotaExceededException quotaExceeded)
                return quotaExceeded;

            if (current is AggregateException aggregate)
            {
                foreach (var inner in aggregate.InnerExceptions)
                    pending.Push(inner);
            }
            else if (current.InnerException is not null)
            {
                pending.Push(current.InnerException);
            }
        }

        return null;
    }
}
