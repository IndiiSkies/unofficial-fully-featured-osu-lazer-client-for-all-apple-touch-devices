#include <stdio.h>
#include <stdint.h>
#include <stdlib.h>
#include <string.h>
#include <libimobiledevice/libimobiledevice.h>
#include <libimobiledevice/lockdown.h>
#include <libimobiledevice/house_arrest.h>
#include <libimobiledevice/afc.h>
static afc_client_t afc;
static int get(const char *remote,FILE *out){uint64_t handle;if(afc_file_open(afc,remote,AFC_FOPEN_RDONLY,&handle))return 1;char data[65536];uint32_t count;int error=0;for(;;){if(afc_file_read(afc,handle,data,sizeof(data),&count)){error=1;break;}if(!count)break;if(fwrite(data,1,count,out)!=count){error=1;break;}}afc_file_close(afc,handle);return error;}
static int videos(const char *root){char **directories=NULL;if(afc_read_directory(afc,root,&directories))return 1;
 for(int i=0;directories[i];i++){if(directories[i][0]=='.')continue;char folder[4096];snprintf(folder,sizeof(folder),"%s/%s",root,directories[i]);char **files=NULL;if(afc_read_directory(afc,folder,&files))continue;
  for(int j=0;files[j];j++){char *ext=strrchr(files[j],'.');if(!ext)continue;if(strcasecmp(ext,".mp4")&&strcasecmp(ext,".m4v")&&strcasecmp(ext,".avi")&&strcasecmp(ext,".webm")&&strcasecmp(ext,".flv")&&strcasecmp(ext,".wmv")&&strcasecmp(ext,".mov")&&strcasecmp(ext,".mkv")&&strcasecmp(ext,".mpg")&&strcasecmp(ext,".mpeg"))continue;if(strstr(files[j],".ipod.mp4"))continue;printf("%s/%s\n",folder,files[j]);}
  afc_dictionary_free(files);
 }afc_dictionary_free(directories);return 0;}
int main(int argc,char **argv){if(argc<4)return 2;
 idevice_t device=NULL;lockdownd_client_t lockdown=NULL;lockdownd_service_descriptor_t service=NULL;house_arrest_client_t house=NULL;plist_t result=NULL;int rc=1;
 if(idevice_new_with_options(&device,argv[1],IDEVICE_LOOKUP_USBMUX))goto done;
 if(lockdownd_client_new_with_handshake(device,&lockdown,"osu-video-sync"))goto done;
 if(lockdownd_start_service(lockdown,"com.apple.mobile.house_arrest",&service))goto done;
 if(house_arrest_client_new(device,service,&house))goto done;
 if(house_arrest_send_command(house,"VendDocuments","org.osu.legacy.experimental"))goto done;
 if(house_arrest_get_result(house,&result) || plist_dict_get_item(result,"Error"))goto done;
 if(afc_client_new_from_house_arrest_client(house,&afc))goto done;
 if(!strcmp(argv[2],"ls")){char **items=NULL;if(afc_read_directory(afc,argv[3],&items))goto done;for(int i=0;items[i];i++)puts(items[i]);afc_dictionary_free(items);rc=0;}
 else if(!strcmp(argv[2],"videos"))rc=videos(argv[3]);
 else if(!strcmp(argv[2],"get") && argc==5){FILE *out=fopen(argv[4],"wb");if(!out)goto done;rc=get(argv[3],out);fclose(out);}
 else if(!strcmp(argv[2],"put") && argc==5){FILE *in=fopen(argv[3],"rb");if(!in)goto done;uint64_t handle;if(afc_file_open(afc,argv[4],AFC_FOPEN_WRONLY,&handle)){fclose(in);goto done;}char data[65536];size_t n;rc=0;
  while((n=fread(data,1,sizeof(data),in))){uint32_t sent=0;while(sent<n){uint32_t count=0;if(afc_file_write(afc,handle,data+sent,(uint32_t)n-sent,&count)||!count){rc=1;break;}sent+=count;}if(rc)break;}
  if(ferror(in))rc=1;fclose(in);afc_file_close(afc,handle);
 }else if(!strcmp(argv[2],"rename") && argc==5)rc=afc_rename_path(afc,argv[3],argv[4])!=AFC_E_SUCCESS;
done:
 if(rc)fprintf(stderr,"device documents operation failed: %s\n",argv[2]);
 if(afc)afc_client_free(afc);if(result)plist_free(result);if(house)house_arrest_client_free(house);if(service)lockdownd_service_descriptor_free(service);if(lockdown)lockdownd_client_free(lockdown);if(device)idevice_free(device);return rc;
}
