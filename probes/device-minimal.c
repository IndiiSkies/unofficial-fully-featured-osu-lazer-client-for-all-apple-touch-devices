/* Minimal Darwin ABI declarations, no SDK headers. These must be verified
 * against the real device; a successful link alone proves no OS compatibility.
 */
typedef unsigned long probe_size_t;
extern int printf(const char *, ...);
extern int sysctlbyname(const char *, void *, probe_size_t *, void *, probe_size_t);

int main(void)
{
    char machine[256] = {0};
    char release[256] = {0};
    probe_size_t machine_size = sizeof(machine);
    probe_size_t release_size = sizeof(release);
    int a = sysctlbyname("hw.machine", machine, &machine_size, 0, 0);
    int b = sysctlbyname("kern.osrelease", release, &release_size, 0, 0);
    printf("ARMv6 probe: hw.machine=%s kern.osrelease=%s pointer_bytes=%lu sysctl_status=%d,%d\n",
           machine, release, (unsigned long)sizeof(void *), a, b);
    return a || b || sizeof(void *) != 4;
}
