#!/bin/bash
set -eo pipefail
toolchain_dir="$(cd "$(dirname "$0")" && pwd)"
root_dir="$(cd "$toolchain_dir/.." && pwd)"
sdk_dir="${IOS_SDK:-$toolchain_dir/iPhoneOS4.2.sdk}"
cc_dir="$(mktemp -d /private/tmp/osu-mono-cc.XXXXXX)"
printf '#!/bin/bash\nexport IOS_SDK=%q IOS_TARGET=armv6-apple-ios4.2.1 IOS_CRT=crt1.3.1.o\nexec %q "$@"\n' \
    "$sdk_dir" "$toolchain_dir/armv6-clang" > "$cc_dir/cc"
chmod +x "$cc_dir/cc"
# Keep the wrapper: generated Makefiles use its absolute path.
mkdir -p "$root_dir/build/mono-sdk42"
cd "$root_dir/build/mono-sdk42"
# sun_path[104] is declared in this SDK's sys/un.h. Darwin dlsym takes
# unprefixed names; that cache assumption still needs a target smoke test.
mono_cv_uscore=no mono_cv_sizeof_sunpath=104 \
    ../../mono-legacy/configure \
    --build="$(uname -m)-apple-darwin" --host=arm-apple-darwin \
    --disable-mcs-build --with-tls=pthread --with-glib=embedded \
    --disable-shared --with-sigaltstack=no --disable-boehm --with-gc=included --with-sgen=yes \
    --enable-minimal=profiler \
    CC="$cc_dir/cc"
