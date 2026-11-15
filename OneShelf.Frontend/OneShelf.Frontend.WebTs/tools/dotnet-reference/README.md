# The ports against the real .NET

`DotnetReference` runs the original C# libraries (analysis, representations, full-text search, the API models' JSON,
and .NET's own behaviour the ports reproduce) on cases and writes what they return. `src/lib/dotnetReference/dispatch.ts`
runs the same cases through the TypeScript ports, projected the same way; `compare.ts` compares the two exactly.
The reference runs as the site does: .NET 9, invariant globalization, in Docker (`mcr.microsoft.com/dotnet/sdk:9.0`),
built from a copy of the repository inside the container (nothing is built in the repository). Both sides run in the
Europe/Berlin time zone.

## The committed check

`src/lib/dotnetReference/expected.jsonl` holds generated cases (seed 1, scale 0.5) with the .NET outputs;
`dotnetReference.test.ts` runs them in Vitest, without .NET. The few differences that cannot be removed are left out
there, with the reason (see PORTING.md, Departures).

To regenerate it (Docker needed):

    tools/dotnet-reference/run-all.ps1 -WorkDir <work directory> -Scale 0.5 -Expected src/lib/dotnetReference/expected.jsonl

A wider run, not committed: `-Seed 7 -Scale 3` (some 10 000 cases); `report.txt` in the work directory lists every
difference.

`src/lib/common/dotnet/invariantCasing.ts` is generated from .NET's casing (`casing.ts`, from the output of the
`chars` case).

## Real data

From a running original site, under a test account, into a directory outside the repository (the data is other
people's songs):

    node tools/dotnet-reference/real-data/dump.mjs <playwright-core directory> http://localhost:13003 <dir>/dump.json
    npx vite-node tools/dotnet-reference/real-data/cases.ts -- <dir>/dump.json <dir>/cases.jsonl
    tools/dotnet-reference/reference.ps1 -DataDir <dir> run cases.jsonl dotnet.jsonl
    npx vite-node tools/dotnet-reference/compare.ts -- <dir>/cases.jsonl --dotnet <dir>/dotnet.jsonl --report <dir>/report.txt
