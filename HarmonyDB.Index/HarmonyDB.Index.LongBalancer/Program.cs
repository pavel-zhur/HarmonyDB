using HarmonyDB.Common.Representations.OneShelf;
using HarmonyDB.Index.Analysis.Models;
using HarmonyDB.Index.Analysis.Services;
using HarmonyDB.Index.Analysis.Tools;
using HarmonyDB.Index.BusinessLogic;
using HarmonyDB.Index.BusinessLogic.Caches;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using OneShelf.Common;

var builder = Host.CreateApplicationBuilder(args);

builder.Configuration.AddJsonFile("appsettings.Secrets.json", true);

var services = builder.Services;

services
    .AddIndexBusinessLogic(builder.Configuration);

var host = builder.Build();

var inputParser = host.Services.GetRequiredService<InputParser>();
var trieCache = host.Services.GetRequiredService<TrieCache>();
var trieOperations = host.Services.GetRequiredService<TrieOperations>();

//await trieCache.Rebuild(10000);
//await trieCache.Rebuild();
//return;

var trie = await trieCache.Get();

var chords = new List<(byte root, ChordType chordType)>();

while (true)
{
    try
    {
        if (!chords.Any())
        {
            Console.Write("Enter chord: ");
            var chord = Console.ReadLine()!;
            chords.Add(inputParser.ParseChord(chord).HarmonyData.SelectSingle(x => (x.Root, x.ChordType)));
        }
        else
        {
            Console.Write("Enter .. or next chord: ");
            var chord = Console.ReadLine()!;
            if (chord == "..")
            {
                chords.RemoveAt(chords.Count - 1);
            }
            else
            {
                chords.Add(inputParser.ParseChord(chord).HarmonyData.SelectSingle(x => (x.Root, x.ChordType)));
            }
        }
    }
    catch
    {
        Console.WriteLine("Invalid input. Try again.");
        continue;
    }

    Console.WriteLine($"Current sequence: {string.Join(" ", chords.Select(x => x.ToChord()))}");

    if (!chords.Any())
        continue;

    var found = trieOperations.Search(
        trie, 
        chords
            .WithPrevious()
            .Select(c => c.previous == null ? ((byte)0, c.current.chordType) : (Note.Normalize(c.current.root - c.previous.Value.root), c.current.chordType))
            .ToArray());
    
    if (found == null)
    {
        Console.WriteLine("No progressions found.");
        continue;
    }

    var root = chords[^1].root;
    foreach (var ((rootDelta, toType), value) in found.OrderByDescending(x => x.Value))
    {
        Console.WriteLine($" -> {(Note.Normalize(rootDelta + root), toType).ToChord()}: {value}");
    }

    Console.WriteLine();
}