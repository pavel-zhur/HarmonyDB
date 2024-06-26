namespace OneShelf.Common.Api;

public class ConcurrencyLimiterOptions
{
    public int MaxConcurrency { get; init; } = 5;
}