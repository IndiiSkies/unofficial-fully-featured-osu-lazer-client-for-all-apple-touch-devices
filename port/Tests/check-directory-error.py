"""Exercise the actual legacy FindNext icall, including a native error."""
from pathlib import Path
import subprocess
root=Path(__file__).resolve().parents[2]
source=(root/'mono-legacy/mono/metadata/file-io.c').read_text()
start=source.index('MonoString *\nves_icall_System_IO_MonoIO_FindNext (')
end=source.index('\nint\nves_icall_System_IO_MonoIO_FindClose',start)
function=source[start:end]
test=r'''
#include <assert.h>
#include <stddef.h>
typedef int gint32;typedef void *gpointer;typedef char MonoString;
typedef struct {int dwFileAttributes;} WIN32_FIND_DATA;
typedef struct {void *find_handle;} IncrementalFind;
#define ERROR_SUCCESS 0
#define ERROR_NO_MORE_FILES 18
#define FALSE 0
static int succeeds,last_error;
static MonoString text;
static int FindNextFile(void *handle,WIN32_FIND_DATA *data){assert(handle==(void *)123);data->dwFileAttributes=7;return succeeds;}
static int GetLastError(void){return last_error;}
static int incremental_find_check_match(IncrementalFind *handle,WIN32_FIND_DATA *data,MonoString **result){(void)handle;(void)data;*result=&text;return 1;}
FUNCTION
int main(void){
 IncrementalFind handle={(void *)123};gint32 attr=-1,error=456;
 succeeds=1;assert(ves_icall_System_IO_MonoIO_FindNext(&handle,&attr,&error)==&text && error==0 && attr==7);
 succeeds=0;last_error=ERROR_NO_MORE_FILES;error=456;assert(!ves_icall_System_IO_MonoIO_FindNext(&handle,&attr,&error) && error==0);
 last_error=5;error=456;assert(!ves_icall_System_IO_MonoIO_FindNext(&handle,&attr,&error) && error==5);
 return 0;
}
'''.replace('FUNCTION',function)
out=root/'build/directory-error-check';out.mkdir(exist_ok=True)
(out/'check.c').write_text(test)
subprocess.run(['clang','-Wall','-Wextra','-Werror','-fsanitize=undefined',str(out/'check.c'),'-o',str(out/'check')],check=True,timeout=20)
subprocess.run([str(out/'check')],check=True,timeout=10)
print('PASS: production Mono FindNext success, exhaustion and native-error pointer paths')
