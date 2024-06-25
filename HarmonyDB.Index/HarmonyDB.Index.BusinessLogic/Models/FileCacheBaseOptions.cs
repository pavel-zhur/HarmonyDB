namespace HarmonyDB.Index.BusinessLogic.Models;

public class FileCacheBaseOptions
{
    public required FileCacheSource ReadSource { get; init; }

    public required FileCacheSource WriteSource { get; init; }

    public string? DiskPath { get; init; }
    
    public string? StorageConnectionString { get; init; }
}