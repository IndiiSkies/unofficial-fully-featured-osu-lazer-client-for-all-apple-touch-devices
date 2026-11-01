#include <errno.h>
#include <stdio.h>
#include <sys/mman.h>
#include <unistd.h>
#include <libkern/OSCacheControl.h>

/* Run separately before Mono: permission failure or a crash here is useful
 * evidence about whether this jailbreak can execute generated ARM code.
 */
int main(void)
{
    size_t length = (size_t)getpagesize();
    void *memory = mmap(0, length, PROT_READ | PROT_WRITE,
                        MAP_PRIVATE | MAP_ANON, -1, 0);
    if (memory == MAP_FAILED) {
        printf("mmap failed: errno=%d\n", errno);
        return 1;
    }
    /* ARM mode: mov r0, #42; bx lr. ARMv6, no VFP/NEON required. */
    ((volatile unsigned int *)memory)[0] = 0xe3a0002a;
    ((volatile unsigned int *)memory)[1] = 0xe12fff1e;
    if (mprotect(memory, length, PROT_READ | PROT_EXEC) != 0) {
        printf("mprotect RX failed: errno=%d\n", errno);
        munmap(memory, length);
        return 2;
    }
    sys_icache_invalidate(memory, 8);
    puts("Executing generated ARMv6 code...");
    fflush(stdout);
    int result = ((int (*)(void))memory)();
    printf("Generated code returned %d (expected 42)\n", result);
    munmap(memory, length);
    return result == 42 ? 0 : 3;
}
