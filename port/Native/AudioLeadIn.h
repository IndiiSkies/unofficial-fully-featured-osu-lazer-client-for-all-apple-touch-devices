#include <math.h>
typedef struct {double duration,deadline,remaining;int waiting,paused;} LegacyAudioLeadIn;
static void legacy_leadin_start(LegacyAudioLeadIn *clock,double milliseconds,double now){clock->duration=fmax(0,milliseconds);clock->remaining=clock->duration;clock->deadline=now+clock->duration;clock->waiting=clock->duration>0;clock->paused=0;}
static double legacy_leadin_time(LegacyAudioLeadIn *clock,double now){return clock->waiting?-(clock->paused?clock->remaining:fmax(0,clock->deadline-now)):0;}
static void legacy_leadin_pause(LegacyAudioLeadIn *clock,double now){if(clock->waiting && !clock->paused){clock->remaining=fmax(0,clock->deadline-now);clock->paused=1;}}
static void legacy_leadin_resume(LegacyAudioLeadIn *clock,double now){if(clock->waiting && clock->paused){clock->deadline=now+clock->remaining;clock->paused=0;}}
static int legacy_leadin_due(LegacyAudioLeadIn *clock,double now){if(!clock->waiting || clock->paused || now<clock->deadline)return 0;clock->waiting=0;return 1;}
