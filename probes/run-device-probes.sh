#!/bin/sh
set -e
probe_root=$(CDPATH= cd -- "$(dirname -- "$0")" && pwd)
export MONO_PATH="$probe_root/lib/mono/4.0"
export MONO_CFG_DIR="$probe_root/etc"
"$probe_root/bin/native-sdk42"
"$probe_root/bin/jit-permissions"
"$probe_root/bin/mono-sgen" --version
"$probe_root/bin/mono-sgen" --runtime=v4.0.30128 "$probe_root/RuntimeProbe.exe"
