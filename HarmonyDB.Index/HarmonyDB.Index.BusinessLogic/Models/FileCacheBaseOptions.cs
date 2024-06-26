namespace HarmonyDB.Index.BusinessLogic.Models;

public class FileCacheBaseOptions
{
    public FileCacheSource? ReadSource { get; init; }

    public FileCacheSource? WriteSource { get; init; }

    public string? DiskPath { get; init; }
    
    public string? StorageConnectionString { get; init; }
}