#!/bin/bash
set -eo pipefail
toolchain_dir="$(cd "$(dirname "$0")" && pwd)"
root_dir="$(cd "$toolchain_dir/.." && pwd)"
if ! git -C "$root_dir/mono-legacy" apply --reverse --check "$toolchain_dir/mono-3.0.12-compat.patch" 2>/dev/null; then
    git -C "$root_dir/mono-legacy" apply --check "$toolchain_dir/mono-3.0.12-compat.patch"
    git -C "$root_dir/mono-legacy" apply "$toolchain_dir/mono-3.0.12-compat.patch"
fi
if ! git -C "$root_dir/mono-legacy" apply --reverse --check "$toolchain_dir/mono-directory-error.patch" 2>/dev/null; then
    git -C "$root_dir/mono-legacy" apply --check "$toolchain_dir/mono-directory-error.patch"
    git -C "$root_dir/mono-legacy" apply "$toolchain_dir/mono-directory-error.patch"
fi
if [ ! -f "$root_dir/mono-legacy/configure" ]; then
    (cd "$root_dir/mono-legacy" && NOCONFIGURE=1 ./autogen.sh)
fi
bash "$toolchain_dir/configure-mono.sh"
# --disable-boehm removes its runtime target; this skips the unused vendored GC.
make -C "$root_dir/build/mono-sdk42" -j6 libgc_dir=
make -C "$root_dir/mono-legacy/mcs/build" PROFILE=net_4_0 MONO_VERSION=3.0.12 common/Consts.cs
make -C "$root_dir/mono-legacy/mcs/class/corlib" PROFILE=net_4_0 BOOTSTRAP_MCS=mcs MCS=mcs RUNTIME=mono SN=sn
