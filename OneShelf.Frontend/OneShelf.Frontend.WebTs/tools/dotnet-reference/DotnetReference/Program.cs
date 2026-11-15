// Runs the real C# libraries on the cases and writes what they return, in a neutral JSON form that
// src/lib/dotnetReference/dispatch.ts produces from the TypeScript ports for the same cases.
//
//   DotnetReference run <cases.jsonl> <outputs.jsonl>   one output line {"id","out"} per case line {"id","suite","input"}
//   DotnetReference gen-json <cases.jsonl> <seed> <count>   writes "json" cases generated from the C# models' contracts
//
// The suites run in the order of the cases, in one process, with one instance of each service (as the site's
// singletons), because some of the original's caches are static and their state carries over between calls.

using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using DotnetReference;

var mode = args[0];
switch (mode)
{
    case "run":
    {
        using var output = new StreamWriter(args[2], false, new UTF8Encoding(false)) { NewLine = "\n" };
        foreach (var line in File.ReadLines(args[1]))
        {
            if (string.IsNullOrWhiteSpace(line)) continue;
            using var document = JsonDocument.Parse(line);
            var root = document.RootElement;
            var id = root.GetProperty("id").GetString()!;
            var suite = root.GetProperty("suite").GetString()!;
            var input = root.GetProperty("input");
            var result = J.Try(() => Suites.Run(suite, input));
            output.WriteLine(new JsonObject { ["id"] = id, ["out"] = result }.ToJsonString());
        }

        break;
    }
    case "gen-json":
    {
        using var output = new StreamWriter(args[1], false, new UTF8Encoding(false)) { NewLine = "\n" };
        foreach (var node in JsonGen.Generate(int.Parse(args[2]), int.Parse(args[3])))
        {
            output.WriteLine(node.ToJsonString());
        }

        break;
    }
    default:
        throw new ArgumentException($"Unknown mode {mode}.");
}
