namespace OneShelf.Common.Api.Client;

public class ApiTraceItem
{
    public required string Url { get; init; }

    public object? Request { get; init; }
    
    public required object? Response { get; init; }

    public required string Method { get; init; }

    public required TimeSpan TimeTaken { get; init; }
}