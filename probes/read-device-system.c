#include <stdio.h>
#include <stdint.h>
#include <string.h>
#include <libimobiledevice/libimobiledevice.h>
#include <libimobiledevice/lockdown.h>
#include <libimobiledevice/afc.h>

/* Read-only diagnostic transfer through an existing jailbreak AFC2 service. */
int main(int argc, char **argv)
{
    if (argc != 4) return 2;
    idevice_t device = NULL;
    lockdownd_client_t lockdown = NULL;
    lockdownd_service_descriptor_t service = NULL;
    afc_client_t afc = NULL;
    uint64_t handle;
    int status = idevice_new_with_options(&device, argv[1], IDEVICE_LOOKUP_USBMUX);
    if (status) { printf("device: %d\n", status); return 1; }
    status = lockdownd_client_new_with_handshake(device, &lockdown, "osu-runtime-debug");
    if (status) { printf("handshake: %d\n", status); return 1; }
    status = lockdownd_start_service(lockdown, "com.apple.afc2", &service);
    if (status) { printf("AFC2 service unavailable: %d\n", status); return 1; }
    status = afc_client_new(device, service, &afc);
    if (status) { printf("AFC2 client: %d\n", status); return 1; }
    if(!strcmp(argv[2],"--list")) {
        char **entries=NULL;status=afc_read_directory(afc,argv[3],&entries);
        if(status){printf("system directory: %d\n",status);return 1;}
        for(int i=0;entries[i];i++)puts(entries[i]);afc_dictionary_free(entries);
        afc_client_free(afc);lockdownd_service_descriptor_free(service);lockdownd_client_free(lockdown);idevice_free(device);return 0;
    }
    status = afc_file_open(afc, argv[2], AFC_FOPEN_RDONLY, &handle);
    if (status) { printf("system file open: %d\n", status); return 1; }
    FILE *output = fopen(argv[3], "wb");
    if (!output) return 1;
    char bytes[65536];
    uint32_t received;
    size_t total = 0;
    do {
        status = afc_file_read(afc, handle, bytes, sizeof(bytes), &received);
        if (status) { printf("read: %d\n", status); return 1; }
        if (fwrite(bytes, 1, received, output) != received) return 1;
        total += received;
    } while (received);
    fclose(output);
    afc_file_close(afc, handle);
    afc_client_free(afc);
    lockdownd_service_descriptor_free(service);
    lockdownd_client_free(lockdown);
    idevice_free(device);
    printf("Copied %zu bytes of system diagnostic data\n", total);
    return 0;
}
