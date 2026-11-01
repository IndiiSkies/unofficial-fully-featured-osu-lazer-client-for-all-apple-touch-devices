#ifndef LEGACY_VIDEO_TIMELINE_H
#define LEGACY_VIDEO_TIMELINE_H
#include <math.h>
typedef struct {double milliseconds;int received;} LegacyVideoClock;
// Continuous playback is driven by AVPlayer and the 100ms control timer.
// Render frames only publish the clock; seeks and timeline boundaries remain immediate.
static int legacy_video_clock_update(LegacyVideoClock *clock,double milliseconds,double offset,double duration){
    double previous=clock->milliseconds,end=offset+duration*1000;
    int immediate=!clock->received || milliseconds<previous || milliseconds-previous>250 || (previous<offset && milliseconds>=offset) || (isfinite(duration) && duration>0 && previous<end && milliseconds>=end);
    clock->milliseconds=milliseconds;clock->received=1;return immediate;
}
static int legacy_video_target(double audioSeconds,double offsetMilliseconds,double duration,double *target){
    *target=audioSeconds-offsetMilliseconds/1000.0;
    return isfinite(*target) && *target>=0 && !(isfinite(duration) && duration>0 && *target>=duration);
}
static int legacy_video_seek_needed(int started,double now,double lastSeek,double position,double target){
    return !started || (now-lastSeek>=1 && (!isfinite(position) || fabs(position-target)>.25));
}
#endif
