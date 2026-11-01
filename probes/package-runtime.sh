#!/bin/bash
set -eo pipefail
probe_dir="$(cd "$(dirname "$0")" && pwd)"
root_dir="$(cd "$probe_dir/.." && pwd)"
stage="$root_dir/build/device-runtime"
mkdir -p "$stage/bin" "$stage/lib/mono/4.0" "$stage/etc/mono"
cp "$root_dir/build/mono-sdk42/mono/mini/mono-sgen" "$stage/bin/mono-sgen"
cp "$root_dir/mono-legacy/mcs/class/lib/net_4_0/mscorlib.dll" "$stage/lib/mono/4.0/"
cp "$root_dir/build/mono-sdk42/data/config" "$stage/etc/mono/"
mcs -nostdlib -r:"$stage/lib/mono/4.0/mscorlib.dll" -out:"$stage/RuntimeProbe.exe" "$probe_dir/RuntimeProbe.cs"
for name in native-sdk42 jit-permissions; do
    cp "$root_dir/build/probes/$name" "$stage/bin/$name"
done
ldid2 -Hsha1 -Cadhoc -S "$stage/bin/mono-sgen"
python3 "$probe_dir/sign-legacy.py" "$stage/bin/mono-sgen"
cp "$probe_dir/run-device-probes.sh" "$stage/run.sh"
chmod +x "$stage/run.sh"
tar -czf "$root_dir/build/device-runtime-probes.tar.gz" -C "$root_dir/build" device-runtime
echo "Prepared $root_dir/build/device-runtime-probes.tar.gz (not run on device)"
