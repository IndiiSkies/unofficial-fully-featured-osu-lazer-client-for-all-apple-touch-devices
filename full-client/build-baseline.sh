#!/bin/bash
set -euo pipefail
root="$(cd "$(dirname "$0")" && pwd)"
local_sdk="$root/../toolchain/dotnet-10"
if [ -x "$local_sdk/dotnet" ]; then
    export DOTNET_ROOT="$local_sdk"
    export PATH="$local_sdk:$PATH"
fi
export DOTNET_CLI_HOME="$root/../build/dotnet-home"
export NUGET_PACKAGES="$root/../build/nuget-packages"
export DOTNET_CLI_TELEMETRY_OPTOUT=1
export DOTNET_SKIP_FIRST_TIME_EXPERIENCE=1
cd "$root/osu"
# This is the original desktop client with the matching framework built from
# source. It is a reference build, not an ARMv6/iOS package.
exec dotnet build osu.Desktop/osu.Desktop.csproj -p:UseLocalFrameworkSource=true "$@"
