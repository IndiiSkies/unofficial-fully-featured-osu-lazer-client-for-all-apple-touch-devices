#import <UIKit/UIKit.h>
#import <QuartzCore/QuartzCore.h>
#import "GpuRenderer.h"
#include "ParticleMath.h"
#include <stdint.h>
extern void legacy_menu_effects_invalidate(void);
extern double legacy_background_dim(void);
typedef struct {double x,size,duration,phase,alpha;} RisingTriangle;
static RisingTriangle particles[24];
static int visible,moving=1,suspended;static double elapsed,lastTick;
static uint32_t seed=421;
static double next_random(void){seed=seed*1664525u+1013904223u;return (seed>>8)/16777216.0;}
void legacy_triangles_attach(UIView *window,UIView *background){
    for(int i=0;i<24;i++){RisingTriangle *p=&particles[i];p->size=36+next_random()*24;p->x=next_random()*480;
        double speed=fmax(.5,.5+.16*sqrt(-2*log(1-next_random()))*sin(2*M_PI*(1-next_random())))*25;
        p->duration=(320+2*p->size*.866)/speed;p->alpha=.18+next_random()*.22;p->phase=next_random();}
}
void legacy_menu_triangles_draw(void){
    if(!visible || suspended)return;double now=CACurrentMediaTime();if(moving && lastTick)elapsed+=fmin(.1,now-lastTick);lastTick=now;
    // Share the visualizer's background render pass. Darkening the particle
    // colour matches drawing the triangles below the existing dim layer.
    double colour=1-legacy_background_dim();
    for(int i=0;i<24;i++){RisingTriangle p=particles[i];double height=p.size*.866,y=legacy_rising_particle(elapsed,p.phase,p.duration,-height,320+height);
        double points[]={p.x,y-height/2,p.x-p.size/2,y+height/2,p.x+p.size/2,y+height/2,p.x,y-height/2};
        legacy_gpu_path(points,4,0,0,1,1.5,colour,colour,colour,p.alpha);}
}
void legacy_menu_triangles(int show,int animate){show=show!=0;animate=animate!=0;if(visible==show && moving==animate)return;visible=show;moving=animate;lastTick=0;legacy_menu_effects_invalidate();}
void legacy_menu_triangles_suspend(int value){suspended=value!=0;lastTick=0;legacy_menu_effects_invalidate();}
