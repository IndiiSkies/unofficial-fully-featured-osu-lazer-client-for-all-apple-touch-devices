#!/usr/bin/env python3
"""Test the actual legacy translator's conv.u case, including pinned objects."""
from pathlib import Path
import subprocess

root = Path(__file__).resolve().parents[2]
source = (root / 'mono-legacy/mono/mini/method-to-ir.c').read_text()
start = source.index('\tcase CEE_CONV_U:\n', source.index('type_from_op (MonoInst'))
end = source.index('\tcase CEE_CONV_I8:', start)
case = source[start:end]
test = r'''
#include <assert.h>
enum { STACK_I4, STACK_I8, STACK_PTR, STACK_MP, STACK_OBJ, STACK_R8 };
enum { CEE_CONV_U = 224, OP_MOVE = 300, OP_ICONV_TO_U, OP_LCONV_TO_U, OP_FCONV_TO_U };
typedef struct { int type, opcode; } Inst;
static void translate(Inst *ins, Inst *src1) {
    switch (ins->opcode) { CASE }
}
int main(void) {
    int inputs[] = { STACK_I4, STACK_I8, STACK_PTR, STACK_MP, STACK_OBJ, STACK_R8 };
    int pointer_op = SIZEOF_VOID_P == 4 ? OP_MOVE : OP_LCONV_TO_U;
    int expected[] = { OP_ICONV_TO_U, OP_LCONV_TO_U, pointer_op, pointer_op, pointer_op, OP_FCONV_TO_U };
    for (int i = 0; i < 6; i++) {
        Inst input = { inputs[i], 0 }, output = { -1, CEE_CONV_U };
        translate(&output, &input);
        assert(output.type == STACK_PTR);
        assert(output.opcode == expected[i]);
    }
}
'''.replace('CASE', case)
out = root / 'build/pointer-conversion-check'
out.mkdir(parents=True, exist_ok=True)
(out / 'test.c').write_text(test)
for width in (4, 8):
    binary = out / f'check-{width}'
    subprocess.run(['clang', '-O2', '-fsanitize=undefined', f'-DSIZEOF_VOID_P={width}', str(out / 'test.c'), '-o', str(binary)], check=True)
    subprocess.run([str(binary)], check=True)
print('PASS: conv.u translation for all six supported inputs at 32 and 64 bits')
