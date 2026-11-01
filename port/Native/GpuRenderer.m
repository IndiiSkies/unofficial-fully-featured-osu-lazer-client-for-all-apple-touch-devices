#include "GpuLayerOrder.h"
#import "GpuRenderer.h"
#import <QuartzCore/QuartzCore.h>
#import <OpenGLES/EAGL.h>
#import <OpenGLES/ES1/gl.h>
#import <OpenGLES/ES1/glext.h>
#include <math.h>
#include <stddef.h>
#include <stdint.h>
#include <stdlib.h>
#include "GpuStroke.h"
#include "GpuTexture.h"
#include "GpuGlyphLayout.h"
#include "DebugMetrics.h"
#include "GpuTransform.h"
#include "GpuCommands.h"
#include "GpuMeshBudget.h"
#include "GpuCacheOrder.h"

// MBX Lite has a fixed-function ES 1.1 pipeline. Keep ordered triangles in
// one client-array batch and flush only on texture changes or capacity.
#define BATCH_VERTICES 8190
#define CIRCLE_SEGMENTS 96
#define TEXTURE_BUDGET (6u*1024u*1024u)
typedef struct {GLfloat x,y,u,v,r,g,b,a;} Vertex;
static Vertex vertices[BATCH_VERTICES];
static Vertex *meshVertices;static unsigned meshCount,meshCapacity;static BOOL meshOverflow;
typedef struct {GLuint buffer;int identity,sourceCount;unsigned vertices,bytes,stamp;float parameters[8];} SliderMesh;
static SliderMesh sliderMeshes[32];static unsigned sliderMeshBytes,sliderMeshStamp;
static unsigned vertexCount;
static GLuint batchTexture;
static LegacyGpuTransform modelTransform,transformStack[32];static unsigned transformDepth;
static float drawOpacity=1,hudOpacityStack[32];static unsigned hudDepth;
static GLfloat unitX[CIRCLE_SEGMENTS+1],unitY[CIRCLE_SEGMENTS+1];
static BOOL active,failed,suspended,clearPending;
static BOOL debugDrawn;
static EAGLContext *context;
static GLint backingWidth,backingHeight;
static NSMutableDictionary *textures;
static NSMutableArray *textureOrder;
static unsigned textureBytes;
static unsigned textureUploads,textureEvictions;static double textureUploadCost,textureStatsAt;
static unsigned profileFrames,profileDraws,profileVertices;
static double profileAt,profileBuild,profilePresent,surfaceStarted;
static int profileScene=-999;
static unsigned presentedSerial;
unsigned legacy_presented_serial(void){return presentedSerial;}
void legacy_software_frame_completed(void){presentedSerial++;}
void legacy_profile_scene(int scene){if(profileScene==scene)return;profileScene=scene;const char *name="intro";const char *ui[]={"home","play","edit","songs","mods","settings","notice","music","results","skin editor","beatmap editor","beatmap listing"};const char *modes[]={"osu gameplay","taiko gameplay","catch gameplay","mania gameplay"};if(scene>=0 && scene<12)name=ui[scene];else if(scene>=100 && scene<104)name=modes[scene-100];NSLog(@"OSU scene: %s",name);profileAt=CACurrentMediaTime();profileFrames=profileDraws=profileVertices=0;profileBuild=profilePresent=0;}
static BOOL cacheEnabled=YES,capturingUI,uiReady,composing;
static GLuint uiFramebuffer,overlayFramebuffer;static BOOL capturingOverlay,overlayReady,overlayFallback;static unsigned overlayVertexStart;
extern void legacy_menu_effects_draw_background(void);
extern void legacy_menu_effects_draw_foreground(void);
extern int legacy_menu_effects_behind_ui(void);
extern void legacy_song_list_draw(void);
extern void legacy_draw_gpu_background(void);
extern int legacy_video_visible(void);
extern void legacy_gpu_background_layers(int opaque);

@interface LegacyGpuView : UIView {
@public GLuint framebuffer,renderbuffer;GLint width,height;
}
@end
@implementation LegacyGpuView
+ (Class)layerClass{return [CAEAGLLayer class];}
- (void)dealloc{if(context && !suspended){[EAGLContext setCurrentContext:context];if(framebuffer)glDeleteFramebuffersOES(1,&framebuffer);if(renderbuffer)glDeleteRenderbuffersOES(1,&renderbuffer);}[super dealloc];}
@end
static LegacyGpuView *gpuView;
static LegacyGpuView *currentSurface;
@interface LegacyGpuTexture : NSObject {
@public GLuint name;int width,height,tw,th;unsigned bytes;
    GLint filter;
    float glyphAdvances[96],fontSize;
    unsigned short glyphX[96],glyphY[96],glyphW[96],glyphH;
}
@end
static LegacyGpuTexture *cachedUI,*cachedOverlay;
static NSString *glyphKeys[257];
@implementation LegacyGpuTexture
- (void)dealloc{if(name)glDeleteTextures(1,&name);[super dealloc];}
@end

static void flush(void) {
    if(!vertexCount)return;
    if(batchTexture){glEnable(GL_TEXTURE_2D);glBindTexture(GL_TEXTURE_2D,batchTexture);glEnableClientState(GL_TEXTURE_COORD_ARRAY);}
    else {glDisable(GL_TEXTURE_2D);glDisableClientState(GL_TEXTURE_COORD_ARRAY);}
    glVertexPointer(2,GL_FLOAT,sizeof(Vertex),&vertices[0].x);
    glColorPointer(4,GL_FLOAT,sizeof(Vertex),&vertices[0].r);
    if(batchTexture)glTexCoordPointer(2,GL_FLOAT,sizeof(Vertex),&vertices[0].u);
    glDrawArrays(GL_TRIANGLES,0,vertexCount);profileDraws++;profileVertices+=vertexCount;vertexCount=0;
}
static void useTexture(GLuint texture){if(batchTexture!=texture){flush();batchTexture=texture;}}
static Vertex vertex(float x,float y,float r,float g,float b,float a){a*=drawOpacity;legacy_gpu_transform_point(modelTransform,&x,&y);Vertex v={x,y,0,0,r*a,g*a,b*a,a};return v;}
static void triangle(Vertex a,Vertex b,Vertex c){if(meshVertices){if(meshCount+3>meshCapacity){meshOverflow=YES;return;}meshVertices[meshCount++]=a;meshVertices[meshCount++]=b;meshVertices[meshCount++]=c;return;}if(vertexCount+3>BATCH_VERTICES)flush();vertices[vertexCount++]=a;vertices[vertexCount++]=b;vertices[vertexCount++]=c;}
static void quad(Vertex a,Vertex b,Vertex c,Vertex d){triangle(a,b,c);triangle(c,b,d);}
static unsigned powerOfTwo(unsigned n){unsigned result=1;while(result<n && result<1024)result<<=1;return result;}
static LegacyGpuTexture *upload(UIImage *image) {
    double started=CACurrentMediaTime();
    CGImageRef cg=image.CGImage;if(!cg)return nil;
    unsigned w=CGImageGetWidth(cg),h=CGImageGetHeight(cg);if(!w || !h || w>1024 || h>1024)return nil;
    LegacyGpuTexture *texture=[[[LegacyGpuTexture alloc] init] autorelease];
    texture->width=w;texture->height=h;texture->tw=powerOfTwo(w);texture->th=powerOfTwo(h);texture->bytes=texture->tw*texture->th*4;
    void *pixels=legacy_gpu_texture_pixels(cg,texture->tw,texture->th);if(!pixels)return nil;
    flush();glGenTextures(1,&texture->name);glBindTexture(GL_TEXTURE_2D,texture->name);
    glTexParameteri(GL_TEXTURE_2D,GL_TEXTURE_MIN_FILTER,GL_LINEAR);glTexParameteri(GL_TEXTURE_2D,GL_TEXTURE_MAG_FILTER,GL_LINEAR);
    texture->filter=GL_LINEAR;
    glTexParameteri(GL_TEXTURE_2D,GL_TEXTURE_WRAP_S,GL_CLAMP_TO_EDGE);glTexParameteri(GL_TEXTURE_2D,GL_TEXTURE_WRAP_T,GL_CLAMP_TO_EDGE);
    glTexImage2D(GL_TEXTURE_2D,0,GL_RGBA,texture->tw,texture->th,0,GL_RGBA,GL_UNSIGNED_BYTE,pixels);free(pixels);
    if(glGetError()!=GL_NO_ERROR)return nil;textureUploads++;textureUploadCost+=CACurrentMediaTime()-started;return texture;
}
static LegacyGpuTexture *cachedTexture(UIImage *image,NSString *key) {
    LegacyGpuTexture *texture=[textures objectForKey:key];
    if(!texture) {
        texture=upload(image);if(!texture)return nil;
        while(textureBytes+texture->bytes>TEXTURE_BUDGET && [textureOrder count]) {
            NSString *old=legacy_gpu_cache_victim(textureOrder);LegacyGpuTexture *victim=[textures objectForKey:old];
            flush();if(batchTexture==victim->name)batchTexture=0;textureBytes-=victim->bytes;textureEvictions++;legacy_gpu_cache_remove(textures,textureOrder,old);[old release];
        }
        [textures setObject:texture forKey:key];textureBytes+=texture->bytes;
    }
    [textureOrder removeObject:key];[textureOrder addObject:key];return texture;
}
void legacy_gpu_loader_text(double x,double y,const char *text,double size,double r,double g,double b) {
    LegacyGpuTransform saved=modelTransform;
    modelTransform.c=saved.c-.2f*saved.a;modelTransform.d=saved.d-.2f*saved.b;
    modelTransform.x=saved.x+saved.a*.2f*(y+size);modelTransform.y=saved.y+saved.b*.2f*(y+size);modelTransform.identity=0;
    legacy_gpu_text(x,y,text,size,r,g,b);modelTransform=saved;
}
static void texturedQuad(LegacyGpuTexture *texture,float x,float y,float w,float h,float u0,float v0,float u1,float v1,float r,float g,float b,float a) {
    useTexture(texture->name);Vertex tl=vertex(x,y,r,g,b,a),tr=vertex(x+w,y,r,g,b,a),bl=vertex(x,y+h,r,g,b,a),br=vertex(x+w,y+h,r,g,b,a);
    tl.u=bl.u=u0;tr.u=br.u=u1;tl.v=tr.v=v0;bl.v=br.v=v1;quad(tl,tr,bl,br);
}
UIView *legacy_gpu_create_surface(UIView *canvas) {
    LegacyGpuView *view=[[[LegacyGpuView alloc] initWithFrame:CGRectMake(0,0,480,320)] autorelease];view.center=CGPointMake(canvas.bounds.size.width/2,canvas.bounds.size.height/2);
    if(canvas.bounds.size.height>canvas.bounds.size.width)view.transform=CGAffineTransformMakeRotation(M_PI_2);
    view.opaque=NO;view.userInteractionEnabled=NO;
    CAEAGLLayer *layer=(CAEAGLLayer *)view.layer;layer.opaque=NO;
    layer.drawableProperties=[NSDictionary dictionaryWithObjectsAndKeys:[NSNumber numberWithBool:NO],kEAGLDrawablePropertyRetainedBacking,kEAGLColorFormatRGBA8,kEAGLDrawablePropertyColorFormat,nil];
    [canvas addSubview:view];return view;
}
void legacy_gpu_attach(UIView *canvas) {gpuView=(LegacyGpuView *)legacy_gpu_create_surface(canvas);gpuView.hidden=YES;gpuView.opaque=YES;((CAEAGLLayer *)gpuView.layer).opaque=YES;}
static BOOL initialize(void) {
    if(context)return YES;if(failed || !gpuView)return NO;
    context=[[EAGLContext alloc] initWithAPI:kEAGLRenderingAPIOpenGLES1];
    if(!context || ![EAGLContext setCurrentContext:context])goto failure;
    textures=[[NSMutableDictionary alloc] init];textureOrder=[[NSMutableArray alloc] init];
    for(int i=0;i<=CIRCLE_SEGMENTS;i++){unitX[i]=cos(i*2*M_PI/CIRCLE_SEGMENTS);unitY[i]=sin(i*2*M_PI/CIRCLE_SEGMENTS);}
    NSLog(@"OSU GPU: %s / %s, shared ES 1.1 surfaces",glGetString(GL_RENDERER),glGetString(GL_VERSION));return YES;
failure:
    NSLog(@"OSU GPU: initialization failed, using Core Graphics");failed=YES;
    [EAGLContext setCurrentContext:nil];[context release];context=nil;return NO;
}
int legacy_gpu_begin_surface(UIView *surface) {
    if(active || suspended || !surface || !initialize())return 0;
    [EAGLContext setCurrentContext:context];if(clearPending){clearPending=NO;legacy_gpu_clear_cache();}
    LegacyGpuView *view=(LegacyGpuView *)surface;
    if(!view->framebuffer){
        glGenFramebuffersOES(1,&view->framebuffer);glGenRenderbuffersOES(1,&view->renderbuffer);
        glBindFramebufferOES(GL_FRAMEBUFFER_OES,view->framebuffer);glBindRenderbufferOES(GL_RENDERBUFFER_OES,view->renderbuffer);
        if(![context renderbufferStorage:GL_RENDERBUFFER_OES fromDrawable:(CAEAGLLayer *)view.layer])goto surface_failure;
        glGetRenderbufferParameterivOES(GL_RENDERBUFFER_OES,GL_RENDERBUFFER_WIDTH_OES,&view->width);glGetRenderbufferParameterivOES(GL_RENDERBUFFER_OES,GL_RENDERBUFFER_HEIGHT_OES,&view->height);
        glFramebufferRenderbufferOES(GL_FRAMEBUFFER_OES,GL_COLOR_ATTACHMENT0_OES,GL_RENDERBUFFER_OES,view->renderbuffer);
        if(glCheckFramebufferStatusOES(GL_FRAMEBUFFER_OES)!=GL_FRAMEBUFFER_COMPLETE_OES)goto surface_failure;
    }
    currentSurface=view;backingWidth=view->width;backingHeight=view->height;
    surfaceStarted=CACurrentMediaTime();
    active=YES;debugDrawn=NO;vertexCount=0;batchTexture=0;modelTransform=legacy_gpu_identity();transformDepth=0;drawOpacity=1;hudDepth=0;
    glBindFramebufferOES(GL_FRAMEBUFFER_OES,view->framebuffer);glViewport(0,0,backingWidth,backingHeight);
    glMatrixMode(GL_PROJECTION);glLoadIdentity();glOrthof(0,480,320,0,-1,1);glMatrixMode(GL_MODELVIEW);glLoadIdentity();
    glDisable(GL_DEPTH_TEST);glDisable(GL_CULL_FACE);glDisable(GL_LIGHTING);glDisable(GL_DITHER);glDisable(GL_SCISSOR_TEST);
    glEnable(GL_BLEND);glBlendFunc(GL_ONE,GL_ONE_MINUS_SRC_ALPHA);glEnableClientState(GL_VERTEX_ARRAY);glEnableClientState(GL_COLOR_ARRAY);
    glTexEnvi(GL_TEXTURE_ENV,GL_TEXTURE_ENV_MODE,GL_MODULATE);if(view==gpuView && !legacy_video_visible())glClearColor(.035,.045,.075,1);else glClearColor(0,0,0,0);glClear(GL_COLOR_BUFFER_BIT);return 1;
surface_failure:
    if(view->framebuffer)glDeleteFramebuffersOES(1,&view->framebuffer);if(view->renderbuffer)glDeleteRenderbuffersOES(1,&view->renderbuffer);view->framebuffer=view->renderbuffer=0;
    NSLog(@"OSU GPU: drawable allocation failed");return 0;
}
static int capture_ui(void){
    flush();capturingOverlay=overlayReady=overlayFallback=NO;
    if(!uiFramebuffer){
        cachedUI=[[LegacyGpuTexture alloc] init];cachedUI->width=480;cachedUI->height=320;cachedUI->tw=cachedUI->th=512;
        glGenTextures(1,&cachedUI->name);glBindTexture(GL_TEXTURE_2D,cachedUI->name);
        glTexParameteri(GL_TEXTURE_2D,GL_TEXTURE_MIN_FILTER,GL_LINEAR);glTexParameteri(GL_TEXTURE_2D,GL_TEXTURE_MAG_FILTER,GL_LINEAR);
        glTexParameteri(GL_TEXTURE_2D,GL_TEXTURE_WRAP_S,GL_CLAMP_TO_EDGE);glTexParameteri(GL_TEXTURE_2D,GL_TEXTURE_WRAP_T,GL_CLAMP_TO_EDGE);
        glTexImage2D(GL_TEXTURE_2D,0,GL_RGBA,512,512,0,GL_RGBA,GL_UNSIGNED_BYTE,NULL);
        glGenFramebuffersOES(1,&uiFramebuffer);glBindFramebufferOES(GL_FRAMEBUFFER_OES,uiFramebuffer);
        glFramebufferTexture2DOES(GL_FRAMEBUFFER_OES,GL_COLOR_ATTACHMENT0_OES,GL_TEXTURE_2D,cachedUI->name,0);
        if(glCheckFramebufferStatusOES(GL_FRAMEBUFFER_OES)!=GL_FRAMEBUFFER_COMPLETE_OES){glDeleteFramebuffersOES(1,&uiFramebuffer);uiFramebuffer=0;[cachedUI release];cachedUI=nil;glBindFramebufferOES(GL_FRAMEBUFFER_OES,gpuView->framebuffer);NSLog(@"OSU GPU: cached UI framebuffer unavailable");return 0;}
    }else glBindFramebufferOES(GL_FRAMEBUFFER_OES,uiFramebuffer);
    glViewport(0,0,480,320);glDisable(GL_SCISSOR_TEST);glClearColor(0,0,0,0);glClear(GL_COLOR_BUFFER_BIT);capturingUI=YES;return 1;
}
void legacy_gpu_cache_ui(int enabled){
    cacheEnabled=enabled!=0;
    if(!active || composing || currentSurface!=gpuView)return;
    if(cacheEnabled && !capturingUI){if(!capture_ui())cacheEnabled=NO;}
    else if(!cacheEnabled && capturingUI){flush();capturingUI=NO;glBindFramebufferOES(GL_FRAMEBUFFER_OES,gpuView->framebuffer);glViewport(0,0,gpuView->width,gpuView->height);glClearColor(0,0,0,0);glClear(GL_COLOR_BUFFER_BIT);}
}
int legacy_gpu_begin(void){int result=legacy_gpu_begin_surface(gpuView);if(result){gpuView.hidden=NO;if(cacheEnabled && !capture_ui())cacheEnabled=NO;}return result;}
static void compose_layer(int layer,void *unused){
    (void)unused;LegacyGpuUV uv=legacy_gpu_framebuffer_region(480,320,512,512);
    switch(layer){
      case LEGACY_LAYER_BACKGROUND:legacy_draw_gpu_background();legacy_menu_effects_draw_background();break;
      case LEGACY_LAYER_SONGS:legacy_song_list_draw();break;
      case LEGACY_LAYER_LOGO:legacy_menu_effects_draw_foreground();break;
      case LEGACY_LAYER_UI:texturedQuad(cachedUI,0,0,480,320,uv.left,uv.top,uv.right,uv.bottom,1,1,1,1);break;
      case LEGACY_LAYER_DEBUG:legacy_debug_draw();debugDrawn=YES;break;
      case LEGACY_LAYER_NOTIFICATIONS:texturedQuad(cachedOverlay,0,0,480,320,uv.left,uv.top,uv.right,uv.bottom,1,1,1,1);break;
    }
}
void legacy_gpu_present_cached(void){
    if(!cacheEnabled || !uiReady || composing || !legacy_gpu_begin_surface(gpuView))return;composing=YES;
    legacy_gpu_compose_layers(legacy_menu_effects_behind_ui() || overlayFallback,overlayReady,compose_layer,NULL);
    legacy_gpu_end();composing=NO;
}
void legacy_gpu_top_overlay(void){
    if(!active || currentSurface!=gpuView)return;
    if(!capturingUI){if(!debugDrawn){legacy_debug_draw();debugDrawn=YES;}return;}
    if(capturingOverlay || overlayFallback)return;
    flush();
    if(!overlayFramebuffer){
        cachedOverlay=[[LegacyGpuTexture alloc] init];cachedOverlay->width=480;cachedOverlay->height=320;cachedOverlay->tw=cachedOverlay->th=512;
        glGenTextures(1,&cachedOverlay->name);glBindTexture(GL_TEXTURE_2D,cachedOverlay->name);
        glTexParameteri(GL_TEXTURE_2D,GL_TEXTURE_MIN_FILTER,GL_LINEAR);glTexParameteri(GL_TEXTURE_2D,GL_TEXTURE_MAG_FILTER,GL_LINEAR);
        glTexParameteri(GL_TEXTURE_2D,GL_TEXTURE_WRAP_S,GL_CLAMP_TO_EDGE);glTexParameteri(GL_TEXTURE_2D,GL_TEXTURE_WRAP_T,GL_CLAMP_TO_EDGE);
        glTexImage2D(GL_TEXTURE_2D,0,GL_RGBA,512,512,0,GL_RGBA,GL_UNSIGNED_BYTE,NULL);
        glGenFramebuffersOES(1,&overlayFramebuffer);glBindFramebufferOES(GL_FRAMEBUFFER_OES,overlayFramebuffer);
        glFramebufferTexture2DOES(GL_FRAMEBUFFER_OES,GL_COLOR_ATTACHMENT0_OES,GL_TEXTURE_2D,cachedOverlay->name,0);
        if(glCheckFramebufferStatusOES(GL_FRAMEBUFFER_OES)!=GL_FRAMEBUFFER_COMPLETE_OES){glDeleteFramebuffersOES(1,&overlayFramebuffer);overlayFramebuffer=0;[cachedOverlay release];cachedOverlay=nil;overlayFallback=YES;glBindFramebufferOES(GL_FRAMEBUFFER_OES,uiFramebuffer);NSLog(@"OSU GPU: top overlay framebuffer unavailable; preserving overlay visibility");return;}
    }else glBindFramebufferOES(GL_FRAMEBUFFER_OES,overlayFramebuffer);
    glViewport(0,0,480,320);glDisable(GL_SCISSOR_TEST);glClearColor(0,0,0,0);glClear(GL_COLOR_BUFFER_BIT);
    capturingOverlay=YES;overlayVertexStart=profileVertices;
}
void legacy_gpu_end(void){if(!active)return;BOOL screen=currentSurface==gpuView;if(screen && !capturingUI && !debugDrawn)legacy_debug_draw();flush();
    double built=CACurrentMediaTime();if(screen)profileBuild+=built-surfaceStarted;
    if(capturingUI){overlayReady=capturingOverlay && profileVertices!=overlayVertexStart;capturingOverlay=NO;capturingUI=NO;uiReady=YES;active=NO;currentSurface=nil;legacy_gpu_present_cached();return;}
    glBindRenderbufferOES(GL_RENDERBUFFER_OES,currentSurface->renderbuffer);BOOL presented=[context presentRenderbuffer:GL_RENDERBUFFER_OES];
    double ended=CACurrentMediaTime();if(presented && screen){presentedSerial++;legacy_debug_frame_presented();profileFrames++;profilePresent+=ended-built;
        if(ended-built>.1 || built-surfaceStarted>.15)NSLog(@"OSU stall: GPU scene %d build %.1f ms present %.1f ms",profileScene,(built-surfaceStarted)*1000,(ended-built)*1000);
        if(!profileAt)profileAt=ended;
        if(ended-profileAt>=5 && profileFrames>=30){NSLog(@"OSU frame: %.1f fps, build %.2f ms, present %.2f ms, %.1f draws/frame, %.0f vertices/frame",profileFrames/(ended-profileAt),profileBuild*1000/profileFrames,profilePresent*1000/profileFrames,(double)profileDraws/profileFrames,(double)profileVertices/profileFrames);profileAt=ended;profileFrames=profileDraws=profileVertices=0;profileBuild=profilePresent=0;}}
    active=NO;currentSurface=nil;
    double now=CACurrentMediaTime();if(now-textureStatsAt>=5){if(textureUploads || textureEvictions)NSLog(@"OSU GPU cache: %u uploads, %u evictions, %.1f ms upload work, %u KiB cached",textureUploads,textureEvictions,textureUploadCost*1000,textureBytes/1024);textureStatsAt=now;textureUploads=textureEvictions=0;textureUploadCost=0;}}
int legacy_gpu_active(void){return active;}
void legacy_gpu_clear_cache(void){if(suspended){clearPending=YES;return;}if(!context)return;[EAGLContext setCurrentContext:context];flush();[textures removeAllObjects];[textureOrder removeAllObjects];textureBytes=0;batchTexture=0;legacy_gpu_clear_paths();}
void legacy_gpu_suspend(int value){if(value && context && !suspended){[EAGLContext setCurrentContext:context];glFinish();}suspended=value!=0;legacy_debug_suspend(value);}
#include "GpuShapes.h"
#include "GpuHitCircle.h"
static void discard_slider(SliderMesh *mesh){if(mesh->buffer){glDeleteBuffers(1,&mesh->buffer);sliderMeshBytes-=mesh->bytes;}memset(mesh,0,sizeof(*mesh));}
void legacy_gpu_clear_paths(void){if(!context || suspended)return;flush();for(int i=0;i<32;i++)discard_slider(&sliderMeshes[i]);}
static SliderMesh *oldest_slider(int allowEmpty){SliderMesh *old=NULL;for(int i=0;i<32;i++){SliderMesh *m=&sliderMeshes[i];if(!m->buffer){if(allowEmpty)return m;continue;}if(!old || m->stamp<old->stamp)old=m;}return old;}
static void cached_body(int identity,const double *points,int count,double x,double y,double scale,double outer,double inner,double r,double g,double b){
    if(!active || (count!=-1 && (!points || count<1)))return;
    float parameters[]={x,y,scale,outer,inner,r,g,b};SliderMesh *mesh=NULL;
    for(int i=0;i<32;i++)if(sliderMeshes[i].buffer && sliderMeshes[i].identity==identity && sliderMeshes[i].sourceCount==count && !memcmp(sliderMeshes[i].parameters,parameters,sizeof(parameters))){mesh=&sliderMeshes[i];break;}
    if(!mesh && identity>0 && modelTransform.identity){unsigned capacity=count==-1?CIRCLE_SEGMENTS*54:legacy_slider_mesh_capacity(count);
        if(capacity){
            useTexture(0);flush();meshVertices=malloc(capacity*sizeof(Vertex));meshCount=0;meshCapacity=capacity;meshOverflow=NO;
            if(meshVertices){
                if(count==-1)legacy_gpu_hitcircle_geometry(x,y,scale,r,g,b,outer!=0);
                else {legacy_gpu_path(points,count,x,y,scale,outer,r,g,b,.85);legacy_gpu_path(points,count,x,y,scale,inner,r*.18,g*.18,b*.18,.96);}
                Vertex *built=meshVertices;meshVertices=NULL;unsigned bytes=meshCount*sizeof(Vertex);
                if(!meshOverflow){
                    mesh=oldest_slider(1);discard_slider(mesh);
                    while(sliderMeshBytes+bytes>LEGACY_SLIDER_CACHE_BYTES)discard_slider(oldest_slider(0));
                    GLuint buffer=0;glGenBuffers(1,&buffer);glBindBuffer(GL_ARRAY_BUFFER,buffer);glBufferData(GL_ARRAY_BUFFER,bytes,built,GL_STATIC_DRAW);GLenum error=glGetError();glBindBuffer(GL_ARRAY_BUFFER,0);
                    if(buffer && error==GL_NO_ERROR){mesh->buffer=buffer;mesh->identity=identity;mesh->sourceCount=count;mesh->vertices=meshCount;mesh->bytes=bytes;memcpy(mesh->parameters,parameters,sizeof(parameters));sliderMeshBytes+=bytes;}
                    else {if(buffer)glDeleteBuffers(1,&buffer);mesh=NULL;}
                }free(built);
            }
        }
    }
    if(!mesh){if(count==-1)legacy_gpu_hitcircle_geometry(x,y,scale,r,g,b,outer!=0);else {legacy_gpu_path(points,count,x,y,scale,outer,r,g,b,.85);legacy_gpu_path(points,count,x,y,scale,inner,r*.18,g*.18,b*.18,.96);}return;}
    mesh->stamp=++sliderMeshStamp;useTexture(0);flush();
    glDisable(GL_TEXTURE_2D);glDisableClientState(GL_TEXTURE_COORD_ARRAY);glBindBuffer(GL_ARRAY_BUFFER,mesh->buffer);
    glVertexPointer(2,GL_FLOAT,sizeof(Vertex),(const void *)offsetof(Vertex,x));glColorPointer(4,GL_FLOAT,sizeof(Vertex),(const void *)offsetof(Vertex,r));
    glDrawArrays(GL_TRIANGLES,0,mesh->vertices);profileDraws++;profileVertices+=mesh->vertices;glBindBuffer(GL_ARRAY_BUFFER,0);
}
void legacy_gpu_slider(int identity,const double *points,int count,double x,double y,double scale,double outer,double inner,double r,double g,double b){cached_body(identity,points,count,x,y,scale,outer,inner,r,g,b);}
void legacy_gpu_hitcircle(int identity,double x,double y,double radius,double r,double g,double b,int reduced){cached_body(identity,NULL,-1,x,y,radius,reduced!=0,0,r,g,b);}
void legacy_gpu_commands(const float *data,int count){
    if(!active || !legacy_gpu_commands_valid(data,count))return;
    for(int i=0;i<count;){const float *p=data+i;int size=legacy_gpu_command_size(p[0]);
        switch((int)p[0]){
            case 1:legacy_gpu_rect(p[1],p[2],p[3],p[4],p[5],p[6],p[7],p[8],p[9]);break;
            case 2:legacy_gpu_circle(p[1],p[2],p[3],p[4],p[5],p[6],p[7],p[8]!=0);break;
            case 3:legacy_gpu_arc(p[1],p[2],p[3],p[4],p[5],p[6],p[7],p[8],p[9]);break;
            case 4:legacy_gpu_ellipse(p[1],p[2],p[3],p[4],p[5],p[6],p[7],p[8]);break;
            case 5:legacy_gpu_push_transform(p[1],p[2],1,p[3]);break;
            case 6:legacy_gpu_pop_transform();break;
        }i+=size;
    }
}
void legacy_gpu_push_transform(double x,double y,double scale,double angle){if(transformDepth>=32)return;transformStack[transformDepth++]=modelTransform;modelTransform=legacy_gpu_transform_push(modelTransform,x,y,scale,angle);}
void legacy_gpu_pop_transform(void){if(transformDepth)modelTransform=transformStack[--transformDepth];}
void legacy_gpu_push_hud(double x,double y,double scale,double angle,double opacity){if(hudDepth>=32 || transformDepth>=32)return;hudOpacityStack[hudDepth++]=drawOpacity;legacy_gpu_push_transform(x,y,scale,angle);drawOpacity*=fmax(0,fmin(1,opacity));}
void legacy_gpu_pop_hud(void){if(hudDepth){legacy_gpu_pop_transform();drawOpacity=hudOpacityStack[--hudDepth];}}
void legacy_gpu_clip(double x,double y,double w,double h){flush();if(w<=0 || h<=0){glDisable(GL_SCISSOR_TEST);return;}glEnable(GL_SCISSOR_TEST);glScissor(floor(x*backingWidth/480),floor((320-y-h)*backingHeight/320),ceil(w*backingWidth/480),ceil(h*backingHeight/320));}
void legacy_gpu_gradient_rect(double x,double y,double w,double h,double r,double g,double b,double leftAlpha,double rightAlpha){useTexture(0);quad(vertex(x,y,r,g,b,leftAlpha),vertex(x+w,y,r,g,b,rightAlpha),vertex(x,y+h,r,g,b,leftAlpha),vertex(x+w,y+h,r,g,b,rightAlpha));}
void legacy_gpu_rotated_rect(double x,double y,double w,double h,double angle,double r,double g,double b,double a){
    legacy_gpu_oriented_rect(x,y,w,h,cosf((float)angle),sinf((float)angle),r,g,b,a);
}
void legacy_gpu_oriented_rect(float x,float y,float w,float h,float ux,float uy,float r,float g,float b,float a){
    useTexture(0);float vx=-uy,vy=ux;
    quad(vertex(x-ux*w/2,y-uy*w/2,r,g,b,a),vertex(x+ux*w/2,y+uy*w/2,r,g,b,a),vertex(x-ux*w/2-vx*h,y-uy*w/2-vy*h,r,g,b,a),vertex(x+ux*w/2-vx*h,y+uy*w/2-vy*h,r,g,b,a));
}
void legacy_gpu_gradient_circle(double x,double y,double radius,double r1,double g1,double b1,double r2,double g2,double b2){
    useTexture(0);float inner=fmax(0,radius-.5),outer=radius+.5;
    Vertex centre=vertex(x,y,(r1+r2)/2,(g1+g2)/2,(b1+b2)/2,1);
    for(int i=0;i<CIRCLE_SEGMENTS;i++){
        float at=(unitY[i]+1)/2,bt=(unitY[i+1]+1)/2;
        Vertex a=vertex(x+unitX[i]*inner,y+unitY[i]*inner,r1+(r2-r1)*at,g1+(g2-g1)*at,b1+(b2-b1)*at,1);
        Vertex b=vertex(x+unitX[i+1]*inner,y+unitY[i+1]*inner,r1+(r2-r1)*bt,g1+(g2-g1)*bt,b1+(b2-b1)*bt,1);
        triangle(centre,a,b);quad(a,b,vertex(x+unitX[i]*outer,y+unitY[i]*outer,0,0,0,0),vertex(x+unitX[i+1]*outer,y+unitY[i+1]*outer,0,0,0,0));
    }
}
static LegacyGpuTexture *glyphAtlas(double requestedSize) {
    float size=fmin(64,fmax(1,round(requestedSize*4)/4));
    int slot=(int)roundf(size*4);NSString *key=glyphKeys[slot];if(!key){key=[[NSString stringWithFormat:@"glyphs:%.2f",size] retain];glyphKeys[slot]=key;}
    LegacyGpuTexture *atlas=[textures objectForKey:key];
    if(atlas){[textureOrder removeObject:key];[textureOrder addObject:key];return atlas;}
    UIFont *font=[UIFont systemFontOfSize:size];float advances[96];unsigned short xs[96],ys[96],widths[96];
    for(int i=0;i<96;i++)advances[i]=[[NSString stringWithFormat:@"%c",i+32] sizeWithFont:font].width;
    int cellHeight=(int)ceil([@"Ag" sizeWithFont:font].height)+4,atlasWidth=size<=16?256:512;
    int atlasHeight=legacy_gpu_glyph_layout(advances,cellHeight,atlasWidth,xs,ys,widths);if(!atlasHeight || atlasHeight>1024)return nil;
    UIGraphicsBeginImageContext(CGSizeMake(atlasWidth,atlasHeight));CGContextRef bitmap=UIGraphicsGetCurrentContext();
    CGContextSetShouldAntialias(bitmap,YES);CGContextSetAllowsAntialiasing(bitmap,YES);CGContextSetRGBFillColor(bitmap,1,1,1,1);
    for(int i=0;i<96;i++)[[NSString stringWithFormat:@"%c",i+32] drawAtPoint:CGPointMake(xs[i]+2,ys[i]+2) withFont:font];
    atlas=cachedTexture(UIGraphicsGetImageFromCurrentImageContext(),key);
    if(atlas){atlas->fontSize=size;atlas->glyphH=cellHeight;
        for(int i=0;i<96;i++){atlas->glyphAdvances[i]=advances[i];atlas->glyphX[i]=xs[i];atlas->glyphY[i]=ys[i];atlas->glyphW[i]=widths[i];}}
    UIGraphicsEndImageContext();return atlas;
}
void legacy_gpu_text(double x,double y,const char *utf8,double size,double r,double g,double b) {
    if(!utf8 || size<=0)return;
    size_t length=strlen(utf8);BOOL ascii=YES;for(size_t i=0;i<length;i++){unsigned char ch=utf8[i];if(ch<32 || ch>126){ascii=NO;break;}}
    if(ascii) {
        LegacyGpuTexture *atlas=glyphAtlas(size);if(!atlas)return;float factor=size/atlas->fontSize;
        for(size_t i=0;i<length;i++){int glyph=(unsigned char)utf8[i]-32;LegacyGpuUV uv=legacy_gpu_texture_region(atlas->glyphX[glyph],atlas->glyphY[glyph],atlas->glyphW[glyph],atlas->glyphH,atlas->tw,atlas->th);
            texturedQuad(atlas,round(x)-2*factor,round(y)-2*factor,atlas->glyphW[glyph]*factor,atlas->glyphH*factor,uv.left,uv.top,uv.right,uv.bottom,r,g,b,1);x+=atlas->glyphAdvances[glyph]*factor;}
    } else {
        NSString *text=[NSString stringWithUTF8String:utf8];if(!text)return;
        NSString *key=[NSString stringWithFormat:@"text:%.2f:%@",size,text];LegacyGpuTexture *texture=[textures objectForKey:key];
        if(texture){[textureOrder removeObject:key];[textureOrder addObject:key];}
        if(!texture){UIFont *font=[UIFont systemFontOfSize:size];CGSize bounds=[text sizeWithFont:font];bounds.width=fmin(480,ceil(bounds.width)+2);bounds.height=fmin(128,ceil(bounds.height)+2);
            UIGraphicsBeginImageContext(bounds);CGContextSetRGBFillColor(UIGraphicsGetCurrentContext(),1,1,1,1);[text drawAtPoint:CGPointZero withFont:font];texture=cachedTexture(UIGraphicsGetImageFromCurrentImageContext(),key);UIGraphicsEndImageContext();}
        if(texture){LegacyGpuUV uv=legacy_gpu_texture_region(0,0,texture->width,texture->height,texture->tw,texture->th);texturedQuad(texture,x,y,texture->width,texture->height,uv.left,uv.top,uv.right,uv.bottom,r,g,b,1);}
    }
}
void legacy_gpu_image(UIImage *image,NSString *key,double x,double y,double w,double h,double alpha) {
    if(!image || !key)return;LegacyGpuTexture *texture=cachedTexture(image,key);if(!texture)return;
    LegacyGpuUV uv=legacy_gpu_texture_region(0,0,texture->width,texture->height,texture->tw,texture->th);
    texturedQuad(texture,x,y,w,h,uv.left,uv.top,uv.right,uv.bottom,1,1,1,alpha);
}
void legacy_gpu_background(UIImage *image,NSString *key,double dim,int nearest){
    if(!active || capturingUI || currentSurface!=gpuView)return;
    BOOL opaque=!legacy_video_visible();
    legacy_gpu_background_layers(opaque);
    if(gpuView.opaque!=opaque){gpuView.opaque=opaque;((CAEAGLLayer *)gpuView.layer).opaque=opaque;}
    if(opaque){
        LegacyGpuTexture *texture=image?cachedTexture(image,key):nil;
        if(texture){GLint filter=nearest?GL_NEAREST:GL_LINEAR;if(texture->filter!=filter){flush();glBindTexture(GL_TEXTURE_2D,texture->name);glTexParameteri(GL_TEXTURE_2D,GL_TEXTURE_MIN_FILTER,filter);glTexParameteri(GL_TEXTURE_2D,GL_TEXTURE_MAG_FILTER,filter);texture->filter=filter;}
            LegacyGpuUV uv=legacy_gpu_texture_region(0,0,texture->width,texture->height,texture->tw,texture->th);texturedQuad(texture,0,0,480,320,uv.left,uv.top,uv.right,uv.bottom,1,1,1,1);
        }else legacy_gpu_rect(0,0,480,320,.035,.045,.075,1,0);
    }
    if(dim>0)legacy_gpu_rect(0,0,480,320,0,0,0,dim,0);
}
