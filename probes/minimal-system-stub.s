// Link-time symbol inventory only. Never copy this library onto the iPod.
// The executable resolves these symbols from the device's real libSystem.
.syntax unified
.arm
.text
.globl _printf
.globl _sysctlbyname
.globl _exit
.globl _memset
.globl dyld_stub_binder
_printf:
_sysctlbyname:
_exit:
_memset:
dyld_stub_binder:
    bx lr
