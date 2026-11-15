# The whole comparison on generated inputs: generates the cases (TypeScript, seeded) and the JSON model cases (.NET, from
# the C# contracts), runs the reference program in Docker, compares with the TypeScript ports.
#   tools/dotnet-reference/run-all.ps1 -WorkDir <work directory> [-Seed 1] [-Scale 1] [-Expected <file>]
# -Expected writes the cases with the .NET outputs (the committed set is src/lib/dotnetReference/expected.jsonl).

param(
    [Parameter(Mandatory = $true)][string]$WorkDir,
    [int]$Seed = 1,
    [double]$Scale = 1,
    [string]$Expected
)

$project = (Resolve-Path (Join-Path $PSScriptRoot '..\..')).Path
New-Item -ItemType Directory -Force $WorkDir | Out-Null
$work = (Resolve-Path $WorkDir).Path
Push-Location $project
try {
    npx vite-node tools/dotnet-reference/cases.ts -- "$work/cases.jsonl" $Seed $Scale
    if ($LASTEXITCODE -ne 0) { throw 'Generating the cases failed.' }
    & "$PSScriptRoot\reference.ps1" -DataDir $work gen-json json-cases.jsonl $Seed ([Math]::Max(1, [int](8 * $Scale)))
    $all = [IO.File]::ReadAllText("$work\cases.jsonl") + [IO.File]::ReadAllText("$work\json-cases.jsonl")
    [IO.File]::WriteAllText("$work\all.jsonl", $all, (New-Object Text.UTF8Encoding $false))
    & "$PSScriptRoot\reference.ps1" -DataDir $work run all.jsonl dotnet.jsonl
    $compareArguments = @("$work/all.jsonl", '--dotnet', "$work/dotnet.jsonl", '--report', "$work/report.txt")
    if ($Expected) { $compareArguments += @('--expected', $Expected) }
    npx vite-node tools/dotnet-reference/compare.ts -- @compareArguments
}
finally {
    Pop-Location
}
