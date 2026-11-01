#include <stdio.h>
#include <sys/utsname.h>

int main(void)
{
    struct utsname info;
    if (uname(&info) != 0) return 1;
    printf("machine=%s system=%s release=%s pointer_bytes=%lu\n",
           info.machine, info.sysname, info.release,
           (unsigned long)sizeof(void *));
    return sizeof(void *) == 4 ? 0 : 2;
}
