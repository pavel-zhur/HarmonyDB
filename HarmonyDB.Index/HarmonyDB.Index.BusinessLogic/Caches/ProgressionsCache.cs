using System.Text.Json;
using HarmonyDB.Index.Analysis.Models.CompactV1;
using HarmonyDB.Index.Analysis.Models.Ids;
using HarmonyDB.Index.BusinessLogic.Caches.Bases;
using HarmonyDB.Index.BusinessLogic.Models;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace HarmonyDB.Index.BusinessLogic.Caches;

public class ProgressionsCache : FileCacheBase<IReadOnlyDictionary<string, byte[]>, CompactChordsProgressions>
{
    private readonly IndexIds _ids;

    public ProgressionsCache(ILogger<ProgressionsCache> logger, IOptions<FileCacheBaseOptions> options, IndexIds ids)
        : base(logger, options)
    {
        _ids = ids;
    }

    protected override string Key => "Progressions";

    public async Task Save(IReadOnlyDictionary<string, byte[]> model)
    {
        await StreamCompressSerialize(model);
    }

    /// <summary>
    /// Reads the file's JSON object (the external ids and their serialized progressions in base64) a part at a time,
    /// each progression straight into the arrays.
    /// </summary>
    protected override CompactChordsProgressions ReadPresentationModel(Stream stream)
    {
        var builder = new CompactChordsProgressions.Builder(_ids.Songs);

        var buffer = new byte[1 << 16];
        var length = 0;
        var isFinalBlock = false;
        var state = new JsonReaderState();
        var depth = 0;
        string? externalId = null;
        var completed = false;
        while (!completed)
        {
            if (!isFinalBlock)
            {
                if (length == buffer.Length)
                {
                    // a token longer than the buffer
                    Array.Resize(ref buffer, buffer.Length * 2);
                }

                var read = stream.Read(buffer, length, buffer.Length - length);
                isFinalBlock = read == 0;
                length += read;
            }

            var reader = new Utf8JsonReader(buffer.AsSpan(0, length), isFinalBlock, state);
            while (!completed && reader.Read())
            {
                switch (reader.TokenType)
                {
                    case JsonTokenType.StartObject when depth == 0:
                        depth++;
                        break;
                    case JsonTokenType.PropertyName when depth == 1 && externalId == null:
                        externalId = reader.GetString()!;
                        break;
                    case JsonTokenType.String when depth == 1 && externalId != null:
                        builder.Add(externalId, reader.GetBytesFromBase64());
                        externalId = null;
                        break;
                    case JsonTokenType.EndObject when depth == 1 && externalId == null:
                        completed = true;
                        break;
                    default:
                        throw new JsonException($"Unexpected {reader.TokenType} in the progressions cache.");
                }
            }

            if (!completed && isFinalBlock) throw new JsonException("The progressions cache ends too early.");

            state = reader.CurrentState;
            var consumed = (int)reader.BytesConsumed;
            buffer.AsSpan(consumed, length - consumed).CopyTo(buffer);
            length -= consumed;
        }

        return builder.Build();
    }
}
