#include <math.h>
// Shared vertex offsets make adjacent slider quads meet without translucent
// overlapping discs. Limit sharp joins so a cusp cannot produce a huge spike.
static void legacy_gpu_stroke_offset(const double *p,int count,int i,float half,float *ox,float *oy) {
    float ax=0,ay=0,bx=0,by=0;
    for(int j=i-1;j>=0;j--){ax=p[i*2]-p[j*2];ay=p[i*2+1]-p[j*2+1];if(ax*ax+ay*ay>1e-10)break;}
    for(int j=i+1;j<count;j++){bx=p[j*2]-p[i*2];by=p[j*2+1]-p[i*2+1];if(bx*bx+by*by>1e-10)break;}
    float al=sqrtf(ax*ax+ay*ay),bl=sqrtf(bx*bx+by*by);
    if(al>1e-5){ax/=al;ay/=al;}if(bl>1e-5){bx/=bl;by/=bl;}
    if(al<=1e-5){ax=bx;ay=by;}if(bl<=1e-5){bx=ax;by=ay;}
    float nx=-(ay+by),ny=ax+bx,length=sqrtf(nx*nx+ny*ny);
    if(length<1e-5){*ox=-ay*half;*oy=ax*half;return;}
    nx/=length;ny/=length;float dot=nx*(-by)+ny*bx;
    float amount=dot>0.5f?half/dot:half*2;
    *ox=nx*amount;*oy=ny*amount;
}
