#include "../Native/VideoTimeline.h"
#include <assert.h>
#include <stdio.h>
int main(void){double target;
 LegacyVideoClock clock={0,0};assert(legacy_video_clock_update(&clock,-1000,0,100));
 for(int i=-999;i<0;i++)assert(!legacy_video_clock_update(&clock,i,0,100));
 assert(legacy_video_clock_update(&clock,0,0,100));
 for(int i=1;i<1000;i++)assert(!legacy_video_clock_update(&clock,i,0,100));
 assert(legacy_video_clock_update(&clock,2000,0,100));assert(legacy_video_clock_update(&clock,1000,0,100));
 clock.milliseconds=99999;assert(legacy_video_clock_update(&clock,100000,0,100));
 clock.received=0;assert(legacy_video_clock_update(&clock,100000,0,100));
 assert(!legacy_video_target(0,375,100,&target));
 assert(legacy_video_target(.375,375,100,&target)&&target==0);
 assert(legacy_video_target(60,375,100,&target)&&fabs(target-59.625)<1e-8);
 assert(legacy_video_target(0,-1000,100,&target)&&target==1);
 assert(!legacy_video_target(100.375,375,100,&target));
 assert(legacy_video_target(4,0,NAN,&target)&&target==4);
 assert(!legacy_video_target(NAN,0,100,&target));
 assert(legacy_video_seek_needed(0,0,0,0,60));
 assert(!legacy_video_seek_needed(1,1,0,59.9,60));
 assert(!legacy_video_seek_needed(1,.5,0,0,60));
 assert(legacy_video_seek_needed(1,1,0,0,60));
 assert(legacy_video_seek_needed(1,2,0,NAN,60));
 puts("PASS: video offsets, preview seek, end/image fallback, drift throttling and cheap continuous render-clock publication");return 0;
}
