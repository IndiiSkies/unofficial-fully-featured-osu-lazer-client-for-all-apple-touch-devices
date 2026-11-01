#include <assert.h>
#include <math.h>
#include <stdio.h>
#include <string.h>
#include "../Native/GpuStroke.h"
#define CIRCLE_SEGMENTS 96
typedef struct {float x,y,u,v,r,g,b,a;} Vertex;
static float unitX[97],unitY[97];
static Vertex mesh[100000];static int vertexCount;
static void useTexture(unsigned texture){assert(texture==0);}
static Vertex vertex(float x,float y,float r,float g,float b,float a){Vertex v={x,y,0,0,r*a,g*a,b*a,a};return v;}
static void triangle(Vertex a,Vertex b,Vertex c){assert(vertexCount+3<100000);mesh[vertexCount++]=a;mesh[vertexCount++]=b;mesh[vertexCount++]=c;}
static void quad(Vertex a,Vertex b,Vertex c,Vertex d){triangle(a,b,c);triangle(c,b,d);}
#include "../Native/GpuShapes.h"
#include "../Native/GpuHitCircle.h"
// Sample the production triangle mesh with the same interpolated alpha used
// by the fixed-function GPU. No extra screen-resolution rendering is needed.
static float coverage(float x,float y) {
    for(int i=0;i<vertexCount;i+=3){Vertex a=mesh[i],b=mesh[i+1],c=mesh[i+2];
        float det=(b.y-c.y)*(a.x-c.x)+(c.x-b.x)*(a.y-c.y);if(fabsf(det)<1e-7)continue;
        float u=((b.y-c.y)*(x-c.x)+(c.x-b.x)*(y-c.y))/det,v=((c.y-a.y)*(x-c.x)+(a.x-c.x)*(y-c.y))/det,w=1-u-v;
        if(u>=-1e-6 && v>=-1e-6 && w>=-1e-6)return u*a.a+v*b.a+w*c.a;
    }
    return 0;
}
static void near(float actual,float expected){assert(fabsf(actual-expected)<.06f);}
int main(void) {
    for(int i=0;i<=96;i++){unitX[i]=cos(i*2*M_PI/96);unitY[i]=sin(i*2*M_PI/96);}
    Vertex expected[6000];
    for(int reduced=0;reduced<2;reduced++)for(int sample=0;sample<8;sample++){
        double radius=4.25+sample*14.0;vertexCount=0;
        if(reduced){legacy_gpu_circle(128,84,radius,.7*.4,.3*.4,.8*.4,1,1);legacy_gpu_circle(128,84,radius,1,1,1,1,0);}
        else {legacy_gpu_circle(128,84,radius,.7*.2,.3*.2,.8*.2,1,1);legacy_gpu_circle(128,84,radius*.86,.7,.3,.8,1,1);legacy_gpu_circle(128,84,radius*.69,.7*.65,.3*.65,.8*.65,1,1);legacy_gpu_circle(128,84,radius*.48,.7*.2,.3*.2,.8*.2,1,1);legacy_gpu_arc(128,84,radius,1,radius*.069,1,1,1,1);}
        int count=vertexCount;assert(count<=CIRCLE_SEGMENTS*54);memcpy(expected,mesh,count*sizeof(Vertex));vertexCount=0;legacy_gpu_hitcircle_geometry(128,84,radius,.7,.3,.8,reduced);assert(vertexCount==count && !memcmp(expected,mesh,count*sizeof(Vertex)));
    }
    vertexCount=0;
    legacy_gpu_circle(20,20,16,1,.5,.25,1,1);
    near(coverage(20,20),1);
    for(int i=0;i<3;i++){float radius=15.75f+i*.25f;near(coverage(20+cos(.4488)*radius,20+sin(.4488)*radius),.75f-i*.25f);}
    near(coverage(38,20),0);
    vertexCount=0;legacy_gpu_circle(20,20,16,1,1,1,1,0);
    near(coverage(20,20),0);near(coverage(35.9,21),1);near(coverage(37,20),.5f);
    double line[]={5,20,45,20};vertexCount=0;legacy_gpu_path(line,2,0,0,1,10,1,1,1,1);
    near(coverage(25,20),1);near(coverage(25,24.75),.75f);near(coverage(25,25),.5f);near(coverage(25,25.25),.25f);near(coverage(25,26),0);
    near(coverage(0,20),.5f);near(coverage(50,20),.5f);
    vertexCount=0;legacy_gpu_path(line,2,0,0,1,1.5,1,1,1,1);
    assert(vertexCount<100);near(coverage(25,20.75),.5f);
    // Geometry must remain bounded for a long slider, including both caps.
    double longPath[2000];for(int i=0;i<1000;i++){longPath[i*2]=i*.4;longPath[i*2+1]=sin(i*.02)*20;}
    vertexCount=0;legacy_gpu_path(longPath,1000,0,0,1,20,.3,.6,.9,.85);
    assert(vertexCount<19000);
    for(int i=0;i<vertexCount;i++){Vertex v=mesh[i];assert(isfinite(v.x)&&isfinite(v.y)&&v.a>=0&&v.a<=1);assert(v.r<=v.a && v.g<=v.a && v.b<=v.a);}
    puts("PASS: GPU circle, ring, slider-edge/cap coverage and bounded long-slider geometry");return 0;
}
