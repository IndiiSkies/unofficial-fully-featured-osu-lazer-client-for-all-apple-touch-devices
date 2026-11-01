#!/bin/bash
set -euo pipefail
probe_dir="$(cd "$(dirname "$0")" && pwd)"
mkdir -p "$probe_dir/../build/probes"
mcs -sdk:4 -out:"$probe_dir/../build/probes/RuntimeProbe.exe" "$probe_dir/RuntimeProbe.cs"
mono "$probe_dir/../build/probes/RuntimeProbe.exe"
echo 'Host test only: this does not establish iOS or ARMv6 compatibility.'
