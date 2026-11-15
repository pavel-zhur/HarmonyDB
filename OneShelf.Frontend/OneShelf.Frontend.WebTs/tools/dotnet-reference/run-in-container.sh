#!/bin/bash
# Inside mcr.microsoft.com/dotnet/sdk:9.0 (see reference.ps1): the repository is at /repo (read-only), the data at /data.
# Copies the libraries DotnetReference needs out of the repository, builds it there and runs it with the arguments.
set -e
dirs="HarmonyDB.Common HarmonyDB.Index/HarmonyDB.Index.Analysis HarmonyDB.Index/HarmonyDB.Index.Analysis.Em HarmonyDB.Source/HarmonyDB.Source.Api.Model OneShelf.Common/OneShelf.Common OneShelf.Common/OneShelf.Common.Songs OneShelf.Frontend/OneShelf.Frontend.Api.Model OneShelf.Frontend/OneShelf.Frontend.SpecificModel OneShelf.Collectives/OneShelf.Collectives.Api.Model OneShelf.Authorization/OneShelf.Authorization.Api.Model OneShelf.Frontend/OneShelf.Frontend.WebTs/tools/dotnet-reference/DotnetReference"
for d in $dirs; do mkdir -p "/src/$d"; cp -r "/repo/$d/." "/src/$d/"; done
cp "/repo/nuget readme.md" /src/
find /src -type d \( -name bin -o -name obj \) -prune -exec rm -rf {} +
cd /src/OneShelf.Frontend/OneShelf.Frontend.WebTs/tools/dotnet-reference/DotnetReference
dotnet build -c Release -nologo -v q -clp:ErrorsOnly -p:EnableSourceLink=false -p:EnableSourceControlManagerQueries=false
dotnet bin/Release/net9.0/DotnetReference.dll "$@"
