#ifndef LEGACY_GPU_TRANSFORM_H
#define LEGACY_GPU_TRANSFORM_H
#include <math.h>
// Apply the former GL model-view transform while generating vertices. This
// preserves ordering and geometry without ending a batch at each push/pop.
typedef struct {float a,b,c,d,x,y;int identity;} LegacyGpuTransform;
static LegacyGpuTransform legacy_gpu_identity(void){LegacyGpuTransform t={1,0,0,1,0,0,1};return t;}
static LegacyGpuTransform legacy_gpu_transform_push(LegacyGpuTransform p,float x,float y,float scale,float angle){
    float co=cosf(angle)*scale,si=sinf(angle)*scale;
    LegacyGpuTransform t={p.a*co+p.c*si,p.b*co+p.d*si,-p.a*si+p.c*co,-p.b*si+p.d*co,p.a*x+p.c*y+p.x,p.b*x+p.d*y+p.y,0};return t;
}
static void legacy_gpu_transform_point(LegacyGpuTransform t,float *x,float *y){if(t.identity)return;float px=*x,py=*y;*x=t.a*px+t.c*py+t.x;*y=t.b*px+t.d*py+t.y;}
#endif
