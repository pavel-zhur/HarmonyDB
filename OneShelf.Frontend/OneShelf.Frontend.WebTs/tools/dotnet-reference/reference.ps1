# Builds DotnetReference against the real C# libraries and runs it, in Docker (mcr.microsoft.com/dotnet/sdk:9.0).
# The repository is mounted read-only and copied into the container (run-in-container.sh); nothing is built in it.
#
#   reference.ps1 -DataDir <dir> run <cases.jsonl> <outputs.jsonl>
#   reference.ps1 -DataDir <dir> gen-json <cases.jsonl> <seed> <countPerType>
#
# The .jsonl arguments are file names inside DataDir. The time zone is Europe/Berlin (the TypeScript side uses the
# same), so that local DateTime conversions are compared under a zone with daylight saving time.

param(
    [Parameter(Mandatory = $true)][string]$DataDir,
    [Parameter(Mandatory = $true, ValueFromRemainingArguments = $true)][string[]]$Arguments
)

$root = (Resolve-Path (Join-Path $PSScriptRoot '..\..\..\..')).Path
$data = (Resolve-Path $DataDir).Path
$programArguments = $Arguments | ForEach-Object { if ($_ -match '\.jsonl$') { "/data/$_" } else { $_ } }

docker run --rm `
    -e TZ=Europe/Berlin `
    -e DOTNET_CLI_TELEMETRY_OPTOUT=1 `
    -e DOTNET_NOLOGO=1 `
    -v harmonydb-nuget:/root/.nuget/packages `
    -v "${root}:/repo:ro" `
    -v "${data}:/data" `
    mcr.microsoft.com/dotnet/sdk:9.0 `
    bash /repo/OneShelf.Frontend/OneShelf.Frontend.WebTs/tools/dotnet-reference/run-in-container.sh @programArguments
if ($LASTEXITCODE -ne 0) { throw "DotnetReference failed with exit code $LASTEXITCODE." }
