#!/bin/bash
set -euo pipefail
export NO_CODESIGN=1
probe_dir="$(cd "$(dirname "$0")" && pwd)"
out_dir="$probe_dir/../build/probes/minimal"
: "${LEGACY_LD:?Set LEGACY_LD to the ARMv6-capable cctools ld executable}"
mkdir -p "$out_dir"
for probe_source in minimal-system-stub.s start-armv6.s device-minimal.c; do
    clang -target armv6-apple-ios4.2.1 -ffreestanding -fno-stack-protector \
        -fno-builtin -c "$probe_dir/$probe_source" -o "$out_dir/$probe_source.o"
done
"$LEGACY_LD" -dylib -arch armv6 -iphoneos_version_min 4.2.1 \
    -install_name /usr/lib/system/libsystem_kernel.dylib -compatibility_version 1.0 \
    "$out_dir/minimal-system-stub.s.o" -o "$out_dir/libSystem.dylib"
# ld64 allows the bottom-level system library to bootstrap without libSystem.
# Give this link-only symbol stub its actual runtime lookup name afterwards.
/usr/bin/install_name_tool -id /usr/lib/libSystem.B.dylib "$out_dir/libSystem.dylib"
"$LEGACY_LD" -arch armv6 -iphoneos_version_min 4.2.1 -no_pie \
    -e _start "$out_dir/start-armv6.s.o" "$out_dir/device-minimal.c.o" \
    -L"$out_dir" -lSystem -o "$out_dir/device-minimal"
ldid2 -Hsha1 -Cadhoc -S "$out_dir/device-minimal"
python3 "$probe_dir/sign-legacy.py" "$out_dir/device-minimal"
file "$out_dir/device-minimal"
echo 'Built and ad-hoc signed. Device execution remains untested.'
echo 'Do NOT deploy libSystem.dylib: it is a link-time stub only.'
