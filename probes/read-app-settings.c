#include <stdio.h>
#include <stdint.h>
#include <stdlib.h>
#include <libimobiledevice/libimobiledevice.h>
#include <libimobiledevice/lockdown.h>
#include <libimobiledevice/house_arrest.h>
#include <libimobiledevice/afc.h>
int main(int argc,char **argv) {
 if(argc!=2)return 2;
 idevice_t device=NULL;lockdownd_client_t lockdown=NULL;lockdownd_service_descriptor_t service=NULL;house_arrest_client_t house=NULL;afc_client_t afc=NULL;plist_t result=NULL;
 int rc=idevice_new_with_options(&device,argv[1],IDEVICE_LOOKUP_USBMUX);if(rc){printf("device %d\n",rc);return 1;}
 rc=lockdownd_client_new_with_handshake(device,&lockdown,"osu-settings-debug");if(rc){printf("handshake %d\n",rc);return 1;}
 rc=lockdownd_start_service(lockdown,"com.apple.mobile.house_arrest",&service);if(rc){printf("service %d\n",rc);return 1;}
 rc=house_arrest_client_new(device,service,&house);if(rc)return 1;
 rc=house_arrest_send_command(house,"VendDocuments","org.osu.legacy.experimental");if(rc)return 1;
 rc=house_arrest_get_result(house,&result);if(rc)return 1;
 plist_t error=plist_dict_get_item(result,"Error");if(error){char *value=NULL;plist_get_string_val(error,&value);printf("house arrest: %s\n",value);free(value);return 1;}
 rc=afc_client_new_from_house_arrest_client(house,&afc);if(rc)return 1;
 uint64_t file;rc=afc_file_open(afc,"Documents/legacy-ui.cfg",AFC_FOPEN_RDONLY,&file);
 if(rc)rc=afc_file_open(afc,"legacy-ui.cfg",AFC_FOPEN_RDONLY,&file);
 if(rc){printf("settings open %d\n",rc);return 1;}
 char data[2049]={0};uint32_t count=0;rc=afc_file_read(afc,file,data,2048,&count);if(rc)return 1;fwrite(data,1,count,stdout);putchar('\n');
 afc_file_close(afc,file);afc_client_free(afc);plist_free(result);house_arrest_client_free(house);lockdownd_service_descriptor_free(service);lockdownd_client_free(lockdown);idevice_free(device);return 0;
}
