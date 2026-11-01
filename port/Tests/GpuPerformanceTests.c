#include "../Native/GpuTransform.h"
#include "../Native/GpuCommands.h"
#include "../Native/GpuMeshBudget.h"
#include <assert.h>
#include <stdio.h>
int main(void){
    assert(legacy_slider_mesh_capacity(1)==864);assert(legacy_slider_mesh_capacity(0)==0);assert(legacy_slider_mesh_capacity(100000)==0);assert(legacy_slider_mesh_capacity(1000)*32<=LEGACY_SLIDER_CACHE_BYTES);
    for(int i=0;i<10000;i++){
        float angle=i*.013f,x=(i%512)-256,y=(i%384)-192,scale=.2f+(i%23)*.1f;
        LegacyGpuTransform identity=legacy_gpu_identity(),first=legacy_gpu_transform_push(identity,123,87,scale,angle);
        float px=x,py=y;legacy_gpu_transform_point(first,&px,&py);
        double co=cos((double)angle),si=sin((double)angle);
        assert(fabs(px-(123+scale*(co*x-si*y)))<.001 && fabs(py-(87+scale*(si*x+co*y)))<.001);
        LegacyGpuTransform child=legacy_gpu_transform_push(first,-32,18,.7f,-.2f);
        float cx=x,cy=y;legacy_gpu_transform_point(child,&cx,&cy);
        float lx=x,ly=y;legacy_gpu_transform_point(legacy_gpu_transform_push(identity,-32,18,.7f,-.2f),&lx,&ly);legacy_gpu_transform_point(first,&lx,&ly);
        assert(fabsf(cx-lx)<.001 && fabsf(cy-ly)<.001);
        // Arc recurrence retains the same tessellation; bound error far below a pixel.
        int count=48;float progress=(i%999+1)/1000.f,delta=progress*2*M_PI/count,a=cosf(delta),b=sinf(delta),rx=0,ry=-1;
        for(int j=1;j<=count;j++){float nx=rx*a-ry*b;ry=rx*b+ry*a;rx=nx;assert(fabs(rx-cos(-M_PI_2+delta*j))*256<.002 && fabs(ry-sin(-M_PI_2+delta*j))*256<.002);}
    }
    float commands[]={5,120,70,.2,2,0,0,8,1,1,1,.8,1,6};
    assert(legacy_gpu_commands_valid(commands,14));
    for(int i=1;i<14;i++)if(i!=4 && i!=13)assert(!legacy_gpu_commands_valid(commands,i));
    commands[5]=NAN;assert(!legacy_gpu_commands_valid(commands,14));commands[5]=0;commands[13]=99;assert(!legacy_gpu_commands_valid(commands,14));
    puts("PASS: 10000 GPU transforms, nested composition and subpixel arc comparisons; bounded ordered command stream");return 0;
}
