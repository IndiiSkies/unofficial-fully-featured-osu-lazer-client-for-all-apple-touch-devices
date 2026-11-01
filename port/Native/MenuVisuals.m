#import <UIKit/UIKit.h>
#import <QuartzCore/QuartzCore.h>
#import "GpuRenderer.h"
#include <pthread.h>
#include <fcntl.h>
#include <unistd.h>
#include <sys/stat.h>
#include <math.h>
#include <string.h>
#include "MenuSpectrum.h"
#include "MenuAudioWindow.h"
#include "ParticleMath.h"
extern int legacy_menu_audio_state(double *,double *);
extern CGImageRef legacy_decode_image(const char *,int);
extern NSString *legacy_cached_analysis_pcm(NSString *);
extern void legacy_menu_triangles_draw(void);
#define RING_BARS 64
#define STARS 24
typedef struct {double x,size,phase,duration,alpha;} LogoParticle;
typedef struct {double x,vx,vy,size,duration;} FountainParticle;
@interface LegacyMenuVisuals : NSObject {
    UIImage *logoImage,*starImage;
    BOOL visible,moving,suspended,workerStarted,dirty;
@public BOOL behindUI;
@private
    float levels[RING_BARS],bassMean,previousBass,rmsSmooth;
    double lastTime,lastBeat,lastBurst,lastTick,lastStats;
    double elapsed,pulseAt,pulseStrength,sideAt[2],sideStrength[2],burstAt;
    LogoParticle triangles[14];FountainParticle stars[STARS];
    unsigned beatCount,offset,consumed;
    int reportedFrame;
    double logoX,logoY,logoSize;
    pthread_mutex_t lock;
    pthread_cond_t work;
    int pending,analysisEnabled;
    NSString *audioSource;
    unsigned audioEpoch;
    double requested;
    MenuSpectrumFrame result;
    unsigned produced,analysed;
    double totalCost,maxCost;
}
- (void)attach:(UIView *)window canvas:(UIView *)canvas;
- (void)settingsAboveLogo:(int)active;
- (void)configureX:(double)x y:(double)y diameter:(double)diameter animate:(int)animate;
- (void)suspend:(int)value;
- (void)analyse;
- (void)source:(NSString *)path;
- (int)cancelled:(unsigned)epoch;
- (void)tick:(CADisplayLink *)link;
- (BOOL)needsFrame;
- (void)invalidate;
- (void)drawBackground;
- (void)drawForeground;
@end
static LegacyMenuVisuals *visuals;
static BOOL deferPresentation;
static unsigned randomSeed=421;
static float ringX[RING_BARS],ringY[RING_BARS];
static double random_unit(void){randomSeed=randomSeed*1664525u+1013904223u;return (randomSeed>>8)/16777216.0;}
@implementation LegacyMenuVisuals
- (void)invalidate {dirty=YES;}
- (id)init {if((self=[super init])){pthread_mutex_init(&lock,NULL);pthread_cond_init(&work,NULL);lastBeat=lastBurst=pulseAt=burstAt=sideAt[0]=sideAt[1]=-100;lastTime=-1;}return self;}
- (void)settingsAboveLogo:(int)active {
    behindUI=active!=0;
}
- (void)attach:(UIView *)window canvas:(UIView *)canvas {
    NSString *bundle=[[NSBundle mainBundle] bundlePath];
    CGImageRef image=legacy_decode_image([[bundle stringByAppendingPathComponent:@"Assets/logo.png"] fileSystemRepresentation],256);
    if(image){logoImage=[[UIImage imageWithCGImage:image] retain];CGImageRelease(image);}
    image=legacy_decode_image([[bundle stringByAppendingPathComponent:@"Assets/fountain-star.png"] fileSystemRepresentation],16);
    if(image){starImage=[[UIImage imageWithCGImage:image] retain];CGImageRelease(image);}
    for(int i=0;i<14;i++){LogoParticle *p=&triangles[i];p->size=18+random_unit()*24;p->x=random_unit()*176.2-88.1;p->alpha=.25+random_unit()*.35;p->duration=12+random_unit()*12;p->phase=random_unit();}
    for(int i=0;i<RING_BARS;i++){float angle=-M_PI_2+2*M_PI*i/RING_BARS;ringX[i]=cosf(angle);ringY[i]=sinf(angle);}
    // The app's single display link owns both scene updates and presentation.
}
- (void)updateState {
    dirty=YES;lastTick=0;
    pthread_mutex_lock(&lock);analysisEnabled=visible && moving && !suspended;pthread_mutex_unlock(&lock);
    if(!visible || !moving || suspended){lastTime=-1;lastBeat=lastBurst=-100;previousBass=bassMean=rmsSmooth=0;memset(levels,0,sizeof(levels));pulseAt=burstAt=sideAt[0]=sideAt[1]=-100;}
}
- (void)configureX:(double)x y:(double)y diameter:(double)diameter animate:(int)animate {
    BOOL show=diameter>0;if(visible==show && moving==(animate!=0) && (!show || (logoX==x && logoY==y && logoSize==diameter)))return;
    BOOL stateChanged=visible!=show || moving!=(animate!=0);visible=show;moving=animate!=0;dirty=YES;if(show){logoX=x;logoY=y;logoSize=diameter;
        if(!workerStarted){workerStarted=YES;[NSThread detachNewThreadSelector:@selector(analyse) toTarget:self withObject:nil];}}
    if(stateChanged)[self updateState];
}
- (void)suspend:(int)value {suspended=value!=0;[self updateState];}
- (void)source:(NSString *)path {
    pthread_mutex_lock(&lock);
    if(![path isEqualToString:audioSource]){[audioSource release];audioSource=[path copy];audioEpoch++;produced=consumed=0;reportedFrame=0;}
    pthread_mutex_unlock(&lock);
}
- (int)cancelled:(unsigned)epoch {
    pthread_mutex_lock(&lock);int cancel=epoch!=audioEpoch || !analysisEnabled;pthread_mutex_unlock(&lock);return cancel;
}
- (void)analyse {
    NSAutoreleasePool *pool=[[NSAutoreleasePool alloc] init];[NSThread setThreadPriority:.2];
    MenuSpectrum fft;if(!menu_spectrum_init(&fft)){NSLog(@"OSU FFT: unavailable");[pool drain];return;}
    int fd=-1;long long samples=0;unsigned localEpoch=0;MenuAudioWindow stream;memset(&stream,0,sizeof(stream));
    for(;;) {
        pthread_mutex_lock(&lock);while(!pending)pthread_cond_wait(&work,&lock);double time=requested;pending=0;
        unsigned epoch=audioEpoch;NSString *source=[audioSource copy];pthread_mutex_unlock(&lock);
        if(epoch!=localEpoch) {
            double setupStarted=CACurrentMediaTime();
            if(fd>=0)close(fd);fd=-1;samples=0;menu_audio_window_close(&stream);
            NSAutoreleasePool *decodePool=[[NSAutoreleasePool alloc] init];
            NSString *pcm=source?legacy_cached_analysis_pcm(source):nil;struct stat info;
            if(pcm){fd=open([pcm fileSystemRepresentation],O_RDONLY);if(fd>=0 && !fstat(fd,&info))samples=info.st_size/2;}
            if(fd>=0 && samples<MENU_FFT_SIZE){close(fd);fd=-1;}
            if(fd<0 && source)menu_audio_window_open(&stream,[source fileSystemRepresentation]);
            localEpoch=[self cancelled:epoch]?0:epoch;
            pthread_mutex_lock(&lock);if(pending && epoch==audioEpoch){time=requested;pending=0;}pthread_mutex_unlock(&lock);
            NSLog(@"OSU FFT: source %@, %@, setup %.2f ms",[source lastPathComponent],fd>=0?@"cached windows":stream.file?@"streamed windows":@"decoder unavailable",(CACurrentMediaTime()-setupStarted)*1000);[decodePool drain];
        }
        [source release];if((fd<0 && !stream.file) || [self cancelled:epoch])continue;
        double start=CACurrentMediaTime();int16_t pcm[MENU_FFT_SIZE];memset(pcm,0,sizeof(pcm));
        if(fd>=0 && samples>=MENU_FFT_SIZE) {
            long long first=(long long)llround(time*MENU_PCM_RATE)-MENU_FFT_SIZE/2;first%=samples;if(first<0)first+=samples;
            size_t count=(size_t)fmin(MENU_FFT_SIZE,samples-first);ssize_t got=pread(fd,pcm,count*2,(off_t)(first*2));
            if(got==(ssize_t)(count*2) && count<MENU_FFT_SIZE)pread(fd,pcm+count,(MENU_FFT_SIZE-count)*2,0);
        } else if(!menu_audio_window_read(&stream,time,pcm,MENU_FFT_SIZE))continue;
        MenuSpectrumFrame frame;frame.time=time;menu_spectrum_analyse(&fft,pcm,&frame);double analysisCost=CACurrentMediaTime()-start;
        pthread_mutex_lock(&lock);
        if(epoch==audioEpoch){result=frame;produced++;analysed++;totalCost+=analysisCost;if(analysisCost>maxCost)maxCost=analysisCost;}
        pthread_mutex_unlock(&lock);
    }
}


- (void)burst {
    burstAt=CACurrentMediaTime();
    for(int i=0;i<STARS;i++){FountainParticle *p=&stars[i];int right=i>=STARS/2;p->x=right?480:0;p->duration=.65+random_unit()*.3;p->vx=(right?-1:1)*(90+random_unit()*150);p->vy=-(300+random_unit()*200);p->size=3+random_unit()*4;}
}
- (void)beat:(float)strength at:(double)time {
    beatCount++;lastBeat=time;pulseAt=CACurrentMediaTime();pulseStrength=strength;
    sideAt[beatCount%2]=pulseAt;sideStrength[beatCount%2]=.6*fmin(.65,.2+strength);
    if(beatCount%4==0 && time-lastBurst>.8){lastBurst=time;[self burst];}
}
- (void)drawBackground {
    if(!visible || suspended)return;double now=CACurrentMediaTime();
    double scale=logoSize/198.0,radius=95*scale;
        legacy_menu_triangles_draw();
        for(int side=0;side<2;side++){double alpha=sideStrength[side]*fmax(0,1-(now-sideAt[side])/.38);
            if(alpha>0)legacy_gpu_gradient_rect(side?430:0,0,50,320,.22,.60,1,side?0:alpha,side?alpha:0);}
        for(int i=0;i<RING_BARS;i++){
            legacy_gpu_oriented_rect(logoX+radius*ringX[i],logoY+radius*ringY[i],2.2*scale,(2+32*levels[i])*scale,-ringY[i],ringX[i],1,.80,.91,.6);}
        for(int i=0;i<STARS;i++){FountainParticle p=stars[i];double t=now-burstAt;if(t<0 || t>=p.duration)continue;
            double progress=t/p.duration,alpha=progress<.15?progress/.15:progress>.6?(1-progress)/.4:1,size=p.size*(.6+1.2*progress);
            legacy_gpu_image(starImage,@"@effect-star",p.x+p.vx*t-size/2,320+p.vy*t+325*t*t-size/2,size,size,alpha);}
}
- (void)drawForeground {
    if(!visible || suspended)return;double now=CACurrentMediaTime();
    double scale=logoSize/198.0,breath=1+fmin(.055,rmsSmooth*.15);
        double t=(now-pulseAt)/.25,pulse=t>=0 && t<1?sin(t*M_PI)*(.025+pulseStrength*.02):0;
        legacy_gpu_push_transform(logoX,logoY,scale*breath*(1+pulse),0);
        legacy_gpu_gradient_circle(0,0,88.1,1,.4,.67,.8,.32,.54);
        for(int i=0;i<14;i++){LogoParticle p=triangles[i];double height=p.size*.866,y=legacy_rising_particle(elapsed,p.phase,p.duration,-88.1-height,88.1+height);
            double points[]={p.x,y-height/2,p.x-p.size/2,y+height/2,p.x+p.size/2,y+height/2,p.x,y-height/2};
            for(int j=0;j<3;j++){double ax=points[j*2],ay=points[j*2+1],bx=points[j*2+2],by=points[j*2+3];
                if(legacy_clip_circle(&ax,&ay,&bx,&by,87.1)){double line[]={ax,ay,bx,by};legacy_gpu_path(line,2,0,0,1,1.5,.71,.20,.44,p.alpha);}}}
        legacy_gpu_image(logoImage,@"@effect-logo",-99,-99,198,198,1);
        legacy_gpu_pop_transform();
}
- (void)presentCached {
    if(deferPresentation)return;
    double start=CACurrentMediaTime();legacy_gpu_present_cached();
    static unsigned frames;static double cost,sampleStart;
    if(!sampleStart)sampleStart=start;frames++;cost+=CACurrentMediaTime()-start;
    if(start-sampleStart>=5){NSLog(@"OSU GPU effects: %.2f ms/frame, one presentation with cached UI",cost*1000/frames);frames=0;cost=0;sampleStart=start;}
}
- (void)tick:(CADisplayLink *)link {
    if(!visible || suspended || (!moving && !dirty))return;
    double now=CACurrentMediaTime(),dt=lastTick?fmin(.25,now-lastTick):.033;lastTick=now;
    if(!moving){[self presentCached];dirty=NO;return;}elapsed+=dt;
    double audioTime=0,gain=0;int playing=legacy_menu_audio_state(&audioTime,&gain);
    BOOL fresh=NO;MenuSpectrumFrame frame;
    if(pthread_mutex_trylock(&lock)==0) {
        if(playing && gain>.001){requested=audioTime+.025;pending=1;pthread_cond_signal(&work);}
        if(produced!=consumed){frame=result;consumed=produced;fresh=YES;}
        if(analysed>=40 && now-lastStats>10){NSLog(@"OSU FFT: %u windows, avg %.3f ms, max %.3f ms",analysed,totalCost*1000/analysed,maxCost*1000);lastStats=now;}
        pthread_mutex_unlock(&lock);
    }
    if(audioTime<lastTime || !playing){previousBass=bassMean=0;lastBeat=lastBurst=-100;}lastTime=audioTime;
    fresh=fresh && playing && gain>.001 && fabs(frame.time-audioTime)<.2;
    if(fresh && !reportedFrame){reportedFrame=1;NSLog(@"OSU GPU effects: first spectrum at %.3f, age %.1f ms",audioTime,(audioTime-frame.time)*1000);}
    float rms=fresh?frame.rms*gain:0,bass=fresh?frame.bass*gain:0;
    if(fresh){float flux=bass-previousBass;bassMean=.96*bassMean+.04*bass;
        if(bass>.012 && flux>fmaxf(.008,bassMean*.12) && bass>bassMean*1.15 && audioTime-lastBeat>.22)[self beat:fminf(1,bass*3) at:audioTime];
        previousBass=bass;offset=(offset+2)%MENU_BANDS;}
    rmsSmooth=rms>rmsSmooth?rms:rmsSmooth*(float)exp(-dt*7);
    float decay=expf((float)(-dt*5));
    for(int i=0;i<RING_BARS;i++){float amplitude=fresh?frame.bands[(i+offset)%MENU_BANDS]*(float)gain:0;levels[i]=fmaxf(amplitude,levels[i]*decay);}
    [self presentCached];dirty=NO;
}
- (BOOL)needsFrame {return visible && !suspended && (moving || dirty);}
@end
int legacy_menu_effects_tick(int present){BOOL needed=[visuals needsFrame];deferPresentation=!present;[visuals tick:nil];deferPresentation=NO;return needed && present;}
void legacy_menu_effects_attach(UIView *window,UIView *canvas){if(!visuals){visuals=[[LegacyMenuVisuals alloc] init];[visuals attach:window canvas:canvas];}}
void legacy_menu_logo(double x,double y,double diameter,int animate){[visuals configureX:x y:y diameter:diameter animate:animate];}
void legacy_menu_effects_suspend(int value){[visuals suspend:value];}
void legacy_settings_above_logo(int active){[visuals settingsAboveLogo:active];}
void legacy_menu_analysis_source(const char *path){[visuals source:path?[NSString stringWithUTF8String:path]:nil];}
void legacy_menu_effects_invalidate(void){[visuals invalidate];}
void legacy_menu_effects_draw_background(void){[visuals drawBackground];}
void legacy_menu_effects_draw_foreground(void){[visuals drawForeground];}
int legacy_menu_effects_behind_ui(void){return visuals && visuals->behindUI;}

