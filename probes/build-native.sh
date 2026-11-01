#!/bin/bash
set -euo pipefail
probe_dir="$(cd "$(dirname "$0")" && pwd)"
: "${IOS_SDK:?Set IOS_SDK to an extracted legacy iPhoneOS SDK directory}"
# IOS_CC must be one executable path; use a wrapper if extra flags are required.
ios_cc="${IOS_CC:-clang}"
mkdir -p "$probe_dir/../build/probes"
"$ios_cc" -arch armv6 -miphoneos-version-min=4.2.1 \
    -isysroot "$IOS_SDK" "$probe_dir/native-runtime.c" \
    -o "$probe_dir/../build/probes/native-runtime"
file "$probe_dir/../build/probes/native-runtime"
# Signing and target execution are separate steps. No device is modified here.
