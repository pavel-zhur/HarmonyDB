namespace OneShelf.Common.Database;

/// <summary>
/// The letter case of this text does not matter: it is compared, looked up and kept unique case-insensitively.
/// On SQL Server the database's case-insensitive collation does it; on PostgreSQL the column gets a case-insensitive collation.
/// </summary>
[AttributeUsage(AttributeTargets.Property)]
public class CaseInsensitiveAttribute : Attribute;
