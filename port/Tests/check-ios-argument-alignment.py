#!/usr/bin/env python3
"""Check iOS argument alignment even on compilers preferring 8-byte int64."""
from pathlib import Path
import subprocess

root = Path(__file__).resolve().parents[2]
source = (root / 'mono-legacy/mono/mini/mini-arm.c').read_text()
start = source.index('void\nmono_arch_cpu_init (void)')
end = source.index('\n}', start) + 2
arg_start = source.index('static void inline\nadd_general (')
arg_end = source.index('\n}', arg_start) + 2
test = '''
#include <assert.h>
typedef long long gint64;
typedef unsigned int guint;
typedef int gboolean;
typedef struct { int size, offset, reg, storage; } ArgInfo;
enum { ARMREG_R3 = 3, ARMREG_SP = 13, RegTypeBase, RegTypeGeneral, RegTypeBaseGen, RegTypeIRegPair };
#define FALSE 0
static int i8_align, eabi_supported;
#define TRUE 1
#define __ARM_EABI__ 1
#define TARGET_IOS 1
HELPER
ARGS
int main(void) {
    assert(__alignof__(gint64) == 8);
    mono_arch_cpu_init();
    assert(eabi_supported && i8_align == 4);
    guint gr = 0, stack = 0;
    ArgInfo arg;
    add_general(&gr, &stack, &arg, FALSE); /* x */
    assert(arg.reg == 0 && arg.storage == RegTypeIRegPair);
    add_general(&gr, &stack, &arg, FALSE); /* y */
    assert(arg.reg == 2 && arg.storage == RegTypeIRegPair);
    add_general(&gr, &stack, &arg, TRUE); /* text */
    assert(arg.offset == 0 && arg.storage == RegTypeBase);
    add_general(&gr, &stack, &arg, FALSE); /* font size */
    assert(arg.offset == 4 && stack == 12);
    add_general(&gr, &stack, &arg, FALSE); /* red */
    assert(arg.offset == 12 && stack == 20);
}
'''.replace('HELPER', source[start:end]).replace('ARGS', source[arg_start:arg_end])
out = root / 'build/ios-alignment-check'
out.mkdir(parents=True, exist_ok=True)
(out / 'test.c').write_text(test)
subprocess.run(['clang', '-O2', str(out / 'test.c'), '-o', str(out / 'check')], check=True)
subprocess.run([str(out / 'check')], check=True)
print('PASS: iOS uses 4-byte argument alignment despite 8-byte preferred alignment')
