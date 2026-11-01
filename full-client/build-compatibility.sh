#!/bin/bash
set -euo pipefail
root="$(cd "$(dirname "$0")" && pwd)"
python3 "$root/test-scalar-math.py"
# Compile the complete source client with implemented compatibility paths.
# This remains a host verification build until the platform/runtime are ported.
exec bash "$root/build-baseline.sh" -p:LegacyScalarMath=true "$@"
