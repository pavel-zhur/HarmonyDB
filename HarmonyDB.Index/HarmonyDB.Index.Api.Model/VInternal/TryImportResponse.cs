namespace HarmonyDB.Index.Api.Model.VInternal;

public class TryImportResponse
{
    public required ImportData? Data { get; init; }

    public class ImportData
    {
        public string? Title { get; init; }

        public string? Artist { get; init; }

        public string Source { get; init; }
    }
}