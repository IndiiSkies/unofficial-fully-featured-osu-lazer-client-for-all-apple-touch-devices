#ifndef LEGACY_DEBUG_METRICS_MATH_H
#define LEGACY_DEBUG_METRICS_MATH_H
#include <math.h>
static double legacy_cpu_percent(double previousCPU,double currentCPU,double wall){
    if(!isfinite(wall) || wall<=0 || !isfinite(currentCPU) || currentCPU<previousCPU)return -1;
    return (currentCPU-previousCPU)*100/wall;
}
static double legacy_gpu_percent(double value){return isfinite(value) && value>=0 && value<=100?value:-1;}
static double legacy_presented_fps(unsigned long long previous,unsigned long long current,double wall){
    return wall>0 && isfinite(wall) && current>=previous?(current-previous)/wall:-1;
}
#endif
