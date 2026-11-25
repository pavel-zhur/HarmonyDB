namespace HarmonyDB.Source.Api.Client.V2;

/// <summary>
/// A server of the source contract did not answer an operation whose answer has no place for "unavailable" (the icon,
/// the ping): it could not be reached, answered with an error, or did not answer in time.
/// </summary>
public class SourceUnavailableException : Exception
{
    public SourceUnavailableException(string source, bool isTimeout, Exception? innerException = null)
        : base($"The source {source} {(isTimeout ? "did not answer in time" : "is unavailable")}.", innerException)
    {
        Source = source;
        IsTimeout = isTimeout;
    }

    /// <summary>
    /// The name of the server, as the caller is configured with it.
    /// </summary>
    public new string Source { get; }

    public bool IsTimeout { get; }
}
