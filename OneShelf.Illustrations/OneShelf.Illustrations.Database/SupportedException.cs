namespace OneShelf.Illustrations.Database;

/// <summary>
/// A change refused for a reason the caller is told in the message.
/// </summary>
public class SupportedException(string message) : Exception(message);
