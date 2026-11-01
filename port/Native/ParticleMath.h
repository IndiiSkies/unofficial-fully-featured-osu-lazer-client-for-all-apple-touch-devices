#ifndef LEGACY_PARTICLE_MATH_H
#define LEGACY_PARTICLE_MATH_H
#include <math.h>
// Clip an outline segment to the logo's circular interior. Only coordinate
// math runs on the CPU; the resulting anti-aliased strokes are rasterized by GL.
static int legacy_clip_circle(double *ax,double *ay,double *bx,double *by,double radius){
    double dx=*bx-*ax,dy=*by-*ay,a=dx*dx+dy*dy;
    if(a<1e-10)return 0;
    double b=2*(*ax*dx+*ay*dy),c=(*ax)*(*ax)+(*ay)*(*ay)-radius*radius,disc=b*b-4*a*c;
    if(disc<0)return 0;double root=sqrt(disc),lo=fmax(0,(-b-root)/(2*a)),hi=fmin(1,(-b+root)/(2*a));
    if(lo>=hi)return 0;double x=*ax,y=*ay;*ax=x+dx*lo;*ay=y+dy*lo;*bx=x+dx*hi;*by=y+dy*hi;return 1;
}
static double legacy_rising_particle(double time,double phase,double duration,double top,double bottom){
    double p=fmod(time/duration+phase,1);if(p<0)p+=1;return bottom+(top-bottom)*p;
}
#endif
