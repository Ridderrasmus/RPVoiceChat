#!/usr/bin/env sh
set -eu
cd -- "$(dirname -- "$0")"
dotnet run --project ./CakeBuild/CakeBuild.csproj -- "$@"
