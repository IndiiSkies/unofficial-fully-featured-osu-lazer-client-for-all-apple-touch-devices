#!/usr/bin/env python3
"""Exercise the actual Mono helper against ARM's 8-bit rotated-immediate format."""
from pathlib import Path
import subprocess

root = Path(__file__).resolve().parents[2]
source = (root / "mono-legacy/mono/mini/mini-arm.c").read_text()
start = source.index("int\nmono_arm_is_rotated_imm8 (")
end = source.index("\n}", start) + 2
literal_start = source.index("static gboolean\narm_patch_literal (")
literal_end = source.index("\n}", literal_start) + 2
output = root / "build/arm-immediate-check"
output.mkdir(exist_ok=True, parents=True)
test = r'''
#include <stdint.h>
#include <assert.h>
#include <stdio.h>
typedef uint32_t guint32;
typedef int gint;
typedef int gboolean;
#define TRUE 1
#define FALSE 0
HELPER
static uint32_t decode(uint32_t byte, unsigned rotation) {
    return rotation ? (byte >> rotation) | (byte << (32 - rotation)) : byte;
}
int main(void) {
    for (unsigned rotation = 0; rotation < 32; rotation += 2) {
        for (unsigned byte = 0; byte < 256; byte++) {
            uint32_t value = decode(byte, rotation);
            int result_rotation = -1;
            int result = mono_arm_is_rotated_imm8(value, &result_rotation);
            assert(result >= 0 && result <= 255);
            assert(result_rotation >= 0 && result_rotation < 32);
            assert(decode((unsigned)result, result_rotation) == value);
        }
    }
    int rotation;
    assert(mono_arm_is_rotated_imm8(0x12345678, &rotation) == -1);
    for (unsigned reg = 0; reg < 16; reg++) {
        uint32_t opcode = 0xe59f0000 | (reg << 12);
        uint32_t words[4] = { opcode, 0xea000000, 0xdeadbeef, 0xe12fff3c };
        assert(arm_patch_literal(words, 0x10a));
        assert(words[0] == opcode && words[1] == 0xea000000 && words[2] == 0x10a && words[3] == 0xe12fff3c);
    }
    uint32_t invalid[3] = { 0xe59f0004, 0, 123 };
    assert(!arm_patch_literal(invalid, 456) && invalid[2] == 123);
    puts("PASS: 4096 ARM immediate encodings, with undefined-behavior sanitizer");
    puts("PASS: literal patch for all 16 destination registers; other offsets rejected");
    return 0;
}
'''.replace("HELPER", source[start:end] + "\n" + source[literal_start:literal_end])
(output / "check.c").write_text(test)
subprocess.run(["/usr/bin/clang", "-O2", "-fsanitize=undefined", "-fno-sanitize-recover=all",
                str(output / "check.c"), "-o", str(output / "check")], check=True)
subprocess.run([str(output / "check")], check=True)
