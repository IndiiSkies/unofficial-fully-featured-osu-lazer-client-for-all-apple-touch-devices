#import <UIKit/UIKit.h>
#import <QuartzCore/QuartzCore.h>
#import "GpuRenderer.h"
#include <math.h>
extern UIView *legacy_canvas_view(void);
extern UIImage *legacy_song_thumbnail(NSString *);
extern void legacy_request_frame(void);
typedef void (*SongSelection)(int);
static CGRect notificationBlock;
@interface SongListHost : UIView
@end
@implementation SongListHost
- (UIView *)hitTest:(CGPoint)point withEvent:(UIEvent *)event {if(CGRectContainsPoint(notificationBlock,point))return nil;UIView *hit=[super hitTest:point withEvent:event];return hit==self?nil:hit;}
@end
@interface LegacySongList : NSObject <UIScrollViewDelegate> {
@public SongListHost *host;UIScrollView *scroll;
    NSArray *rows;NSString *data;int selected;SongSelection selection;BOOL dirty;
}
- (void)tick:(CADisplayLink *)link;
- (void)draw;
@end
static LegacySongList *browser;
@implementation LegacySongList
- (id)init {
    if((self=[super init])){
        UIView *canvas=legacy_canvas_view();host=[[SongListHost alloc] initWithFrame:CGRectMake(0,0,480,320)];host.backgroundColor=[UIColor clearColor];
        host.center=CGPointMake(canvas.bounds.size.width/2,canvas.bounds.size.height/2);
        if(canvas.bounds.size.height>canvas.bounds.size.width)host.transform=CGAffineTransformMakeRotation(M_PI_2);
        // UIKit supplies only touch tracking, momentum and scroll physics.
        // There are no cells, labels, image views or drawn scroll indicators.
        scroll=[[UIScrollView alloc] initWithFrame:CGRectMake(190,60,280,176)];scroll.backgroundColor=[UIColor clearColor];scroll.opaque=NO;
        scroll.delegate=self;scroll.showsVerticalScrollIndicator=NO;scroll.alwaysBounceVertical=YES;
        UITapGestureRecognizer *tap=[[[UITapGestureRecognizer alloc] initWithTarget:self action:@selector(tap:)] autorelease];[scroll addGestureRecognizer:tap];
        [host addSubview:scroll];[canvas addSubview:host];host.hidden=YES;
    }return self;
}
- (void)scrollViewDidScroll:(UIScrollView *)view {dirty=YES;}
- (void)tap:(UITapGestureRecognizer *)tap {
    if(tap.state!=UIGestureRecognizerStateRecognized)return;double y=[tap locationInView:scroll].y,top=0;
    for(NSArray *row in rows){double height=[[row objectAtIndex:1] intValue]?32:45;
        if(y>=top && y<top+height){if(selection)selection([[row objectAtIndex:0] intValue]);legacy_request_frame();return;}top+=height;}
}
- (void)tick:(CADisplayLink *)link {
    if(!host.hidden && dirty)legacy_gpu_present_cached();
}
- (void)draw {
    if(host.hidden)return;
    legacy_gpu_clip(190,60,280,176);double y=60-scroll.contentOffset.y;
    for(NSArray *row in rows){BOOL difficulty=[[row objectAtIndex:1] intValue]!=0,active=[[row objectAtIndex:0] intValue]==selected;double height=difficulty?32:45;
        if(y+height>=60 && y<=236){double x=difficulty?208:190;
            legacy_gpu_rect(x,y,470-x,height-2,active?.40:.18,active?.267:.12,active?.80:.25,.96,0);
            NSString *title=[row objectAtIndex:2];if([title length]>42)title=[[title substringToIndex:39] stringByAppendingString:@"..."];
            if(!difficulty){NSString *art=[row objectAtIndex:4];UIImage *image=[art length]?legacy_song_thumbnail(art):nil;
                if(image)legacy_gpu_image(image,[@"song-cover:" stringByAppendingString:art],195,y+4,44,36,1);
                legacy_gpu_text(245,y+4,[title UTF8String],13,1,1,1);
                NSString *subtitle=[row objectAtIndex:3];if([subtitle length]>45)subtitle=[[subtitle substringToIndex:42] stringByAppendingString:@"..."];
                legacy_gpu_text(245,y+25,[subtitle UTF8String],9,.75,.75,.75);
            }else legacy_gpu_text(224,y+7,[title UTF8String],12,1,1,1);
        }
        y+=height;if(y>236)break;
    }
    if(scroll.contentSize.height>176){double height=fmax(14,176*176/scroll.contentSize.height),offset=fmax(0,fmin(1,scroll.contentOffset.y/(scroll.contentSize.height-176)));
        legacy_gpu_rect(466,60+(176-height)*offset,3,height,1,1,1,.5,0);}
    legacy_gpu_clip(0,0,0,0);dirty=NO;
}
@end
void legacy_song_list_draw(void){[browser draw];}
void legacy_song_list_notification_region(double x,double y,double width,double height){notificationBlock=CGRectMake(x,y,width,height);if(browser)browser->scroll.scrollEnabled=!(width>=480 && height>=320);}
int legacy_song_list_tick(void){if(browser && !browser->host.hidden && browser->dirty){[browser tick:nil];return 1;}return 0;}
void legacy_song_list_visible(int visible){if(browser){browser->host.hidden=!visible;if(visible)browser->dirty=YES;}}
void legacy_song_list_set(const char *serialized,int selected,SongSelection callback){
    if(!browser)browser=[[LegacySongList alloc] init];NSString *value=serialized?[NSString stringWithUTF8String:serialized]:@"";
    BOOL changed=![value isEqualToString:browser->data],selectionChanged=browser->selected!=selected;browser->selected=selected;browser->selection=callback;
    if(changed || selectionChanged){
        NSMutableArray *parsed=[NSMutableArray array];double height=0,selectionTop=-1,selectionHeight=0;
        for(NSString *line in [value componentsSeparatedByString:@"\n"]){NSArray *fields=[line componentsSeparatedByString:@"\t"];if([fields count]!=5)continue;[parsed addObject:fields];double rowHeight=[[fields objectAtIndex:1] intValue]?32:45;
            if([[fields objectAtIndex:0] intValue]==selected && [[fields objectAtIndex:1] intValue]){selectionTop=height;selectionHeight=rowHeight;}height+=rowHeight;}
        [browser->rows release];browser->rows=[parsed copy];[browser->data release];browser->data=[value copy];
        CGPoint offset=browser->scroll.contentOffset;browser->scroll.contentSize=CGSizeMake(280,height);
        offset.y=fmax(0,fmin(offset.y,fmax(0,height-176)));
        if(selectionChanged && selectionTop>=0){if(selectionTop<offset.y)offset.y=selectionTop;else if(selectionTop+selectionHeight>offset.y+176)offset.y=selectionTop+selectionHeight-176;}
        browser->scroll.contentOffset=offset;browser->dirty=YES;
    }
    browser->host.hidden=NO;browser->dirty=YES;
}
