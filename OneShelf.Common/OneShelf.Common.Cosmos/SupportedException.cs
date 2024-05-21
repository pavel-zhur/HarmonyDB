namespace OneShelf.Common.Cosmos;

public class SupportedException : Exception
{
    public SupportedException(string message)
        : base(message)
    {
    }
}