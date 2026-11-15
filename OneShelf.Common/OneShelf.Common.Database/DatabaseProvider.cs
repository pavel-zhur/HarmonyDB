namespace OneShelf.Common.Database;

/// <summary>
/// The database a service keeps its EF Core contexts in. Chosen by the "DatabaseProvider" setting; SQL Server when it is not set.
/// </summary>
public enum DatabaseProvider
{
    SqlServer,
    PostgreSql,
}
