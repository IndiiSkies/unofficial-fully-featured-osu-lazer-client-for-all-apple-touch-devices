#include "../Native/GpuStroke.h"
#include <assert.h>
#include <stdio.h>
int main(void) {
    double straight[]={0,0,10,0,20,0},corner[]={0,0,10,0,10,10},cusp[]={0,0,10,0,0,0},duplicate[]={0,0,0,0,10,0};float x,y;
    for(int i=0;i<3;i++){legacy_gpu_stroke_offset(straight,3,i,2,&x,&y);assert(fabs(x)<1e-5 && fabs(y-2)<1e-5);}
    legacy_gpu_stroke_offset(corner,3,1,2,&x,&y);assert(fabs(x+2)<1e-5 && fabs(y-2)<1e-5);
    legacy_gpu_stroke_offset(cusp,3,1,2,&x,&y);assert(isfinite(x)&&isfinite(y)&&hypot(x,y)<=4.001);
    for(int i=0;i<3;i++){legacy_gpu_stroke_offset(duplicate,3,i,2,&x,&y);assert(fabs(x)<1e-5 && fabs(y-2)<1e-5);}
    puts("PASS: GPU slider joins, caps, duplicate points and cusp bounds");return 0;
}
