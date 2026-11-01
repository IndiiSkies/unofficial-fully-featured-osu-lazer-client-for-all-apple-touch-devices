#include "../Native/ParticleMath.h"
#include <assert.h>
#include <stdio.h>
int main(void){int checks=0;
 for(int i=0;i<360;i++){double angle=i*M_PI/180,dx=cos(angle),dy=sin(angle),ax=-200*dx,ay=-200*dy,bx=200*dx,by=200*dy;
  assert(legacy_clip_circle(&ax,&ay,&bx,&by,87.1));assert(fabs(hypot(ax,ay)-87.1)<1e-8 && fabs(hypot(bx,by)-87.1)<1e-8);checks+=3;
  ax=0;ay=0;bx=200*dx;by=200*dy;assert(legacy_clip_circle(&ax,&ay,&bx,&by,87.1));assert(hypot(ax,ay)<1e-8 && fabs(hypot(bx,by)-87.1)<1e-8);checks+=3;
 }
 double ax=100,ay=100,bx=120,by=100;assert(!legacy_clip_circle(&ax,&ay,&bx,&by,87.1));
 ax=10;ay=20;bx=30;by=40;assert(legacy_clip_circle(&ax,&ay,&bx,&by,87.1));assert(ax==10 && ay==20 && bx==30 && by==40);
 for(int i=-100;i<=100;i++){double t=i*.25,y=legacy_rising_particle(t,.37,12,-42,362);assert(y>=-42 && y<=362);assert(fabs(y-legacy_rising_particle(t+12,.37,12,-42,362))<1e-8);checks+=2;}
 printf("PASS: %d GPU particle bounds, circular clipping and periodic-motion checks\n",checks+3);return 0;
}
