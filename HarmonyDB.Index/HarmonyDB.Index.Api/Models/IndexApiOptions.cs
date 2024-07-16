namespace HarmonyDB.Index.Api.Models;

public class IndexApiOptions
{
    private readonly bool? _redirectCachesToIndex;
    private readonly bool? _limitConcurrency;

    public bool RedirectCachesToIndex
    {
        get => _redirectCachesToIndex ?? false;
        init => _redirectCachesToIndex = value;
    }

    // if RedirectCachesToIndex, defaults to true
    public bool LimitConcurrency
    {
        get => _limitConcurrency ?? _redirectCachesToIndex ?? false;
        init => _limitConcurrency = value;
    }
}