using System.Security.Cryptography;
using HarmonyDB.Analyses.Api.Models;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace HarmonyDB.Analyses.Api.Services;

/// <summary>
/// The analyses' folder (<see cref="AnalysesOptions.FilesFolder"/>): a folder an analysis, named by its id, and in it a
/// file an upload, named by the file's name and the upload. A file once written is never changed: an upload of the
/// same name is a new file, and the old one is deleted when the new one has become the analysis's.
/// </summary>
public sealed class AnalysisFolder(IOptions<AnalysesOptions> options, ILogger<AnalysisFolder> logger)
{
    private const string PartSuffix = ".part";

    public sealed record Written(string StoredName, long Size, string Sha256);

    /// <summary>
    /// The full path of a stored file.
    /// </summary>
    public string PathOf(Guid analysisId, string storedName) => Path.Combine(FolderOf(analysisId), storedName);

    /// <summary>
    /// Writes the body as a new file of the analysis.
    /// </summary>
    /// <returns>The new file; null when the body is longer than <paramref name="maxBytes"/> (nothing is left behind).</returns>
    public async Task<Written?> WriteAsync(Guid analysisId, string name, Stream body, long maxBytes, CancellationToken cancellationToken)
    {
        var directory = Directory.CreateDirectory(FolderOf(analysisId)).FullName;
        var storedName = $"{name}.{Guid.NewGuid():N}";
        var part = Path.Combine(directory, storedName + PartSuffix);

        try
        {
            long size = 0;
            using var hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
            await using (var output = new FileStream(part, FileMode.CreateNew, FileAccess.Write, FileShare.None, 1 << 16, useAsync: true))
            {
                var buffer = new byte[1 << 16];
                int read;
                while ((read = await body.ReadAsync(buffer, cancellationToken)) > 0)
                {
                    size += read;
                    if (size > maxBytes) break;
                    hash.AppendData(buffer, 0, read);
                    await output.WriteAsync(buffer.AsMemory(0, read), cancellationToken);
                }

                await output.FlushAsync(cancellationToken);
            }

            if (size > maxBytes)
            {
                File.Delete(part);
                return null;
            }

            File.Move(part, Path.Combine(directory, storedName));
            return new(storedName, size, Convert.ToHexString(hash.GetHashAndReset()).ToLowerInvariant());
        }
        catch
        {
            File.Delete(part);
            throw;
        }
    }

    /// <summary>
    /// Deletes a stored file that is no longer the analysis's. A file that cannot be deleted is left and logged: it is
    /// named by nothing.
    /// </summary>
    public void Delete(Guid analysisId, string storedName)
    {
        try
        {
            File.Delete(PathOf(analysisId, storedName));
        }
        catch (IOException e)
        {
            logger.LogError(e, "The file {storedName} of the analysis {analysisId} could not be deleted.", storedName, analysisId);
        }
    }

    /// <summary>
    /// Deletes the folder of an analysis that is gone.
    /// </summary>
    public void DeleteAll(Guid analysisId)
    {
        var directory = FolderOf(analysisId);
        try
        {
            if (Directory.Exists(directory)) Directory.Delete(directory, recursive: true);
        }
        catch (IOException e)
        {
            logger.LogError(e, "The folder of the analysis {analysisId} could not be deleted.", analysisId);
        }
    }

    private string FolderOf(Guid analysisId) => Path.Combine(options.Value.FilesFolder!, analysisId.ToString("N"));
}
