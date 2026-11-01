.syntax unified
.arm
.text
.align 2
.globl _start
_start:
    // No SDK crt1.o: this probe needs no argc/argv or C++ initializers.
    bl _main
    bl _exit
1:  b 1b
