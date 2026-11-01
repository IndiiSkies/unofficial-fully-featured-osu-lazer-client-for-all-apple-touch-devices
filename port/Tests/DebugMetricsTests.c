#include "../Native/DebugMetricsMath.h"
#include <assert.h>
#include <stdio.h>
int main(void){
 assert(fabs(legacy_cpu_percent(10,10.25,1)-25)<1e-9);
 assert(fabs(legacy_cpu_percent(10,10.25,.5)-50)<1e-9);
 assert(legacy_cpu_percent(10,11,1)==100);
 assert(legacy_cpu_percent(10,10,1)==0);
 assert(legacy_cpu_percent(10,9,1)<0 && legacy_cpu_percent(10,11,0)<0);
 assert(legacy_presented_fps(100,130,1)==30);
 assert(legacy_presented_fps(100,115,.5)==30);
 assert(legacy_presented_fps(100,100,1)==0);
 assert(legacy_presented_fps(100,1,1)<0 && legacy_presented_fps(0,1,0)<0);
 assert(legacy_gpu_percent(0)==0 && legacy_gpu_percent(100)==100 && legacy_gpu_percent(39.5)==39.5);
 assert(legacy_gpu_percent(-1)<0 && legacy_gpu_percent(101)<0 && legacy_gpu_percent(NAN)<0);
 puts("PASS: performance counter intervals, frame counts, idle samples and unavailable GPU values");return 0;
}
