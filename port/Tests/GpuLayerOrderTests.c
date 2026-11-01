#include "../Native/GpuLayerOrder.h"
#include <assert.h>
#include <stdio.h>
static int stages[8],count;
static void record(int stage,void *state){(void)state;stages[count++]=stage;}
static int indexOf(int stage){for(int i=0;i<count;i++)if(stages[i]==stage)return i;return -1;}
int main(void){for(int behind=0;behind<2;behind++)for(int overlay=0;overlay<2;overlay++){count=0;legacy_gpu_compose_layers(behind,overlay,record,NULL);assert(count==5+overlay);assert(indexOf(LEGACY_LAYER_BACKGROUND)==0);assert(indexOf(LEGACY_LAYER_SONGS)<indexOf(LEGACY_LAYER_UI));assert((indexOf(LEGACY_LAYER_LOGO)<indexOf(LEGACY_LAYER_UI))==behind);assert(indexOf(LEGACY_LAYER_DEBUG)>indexOf(LEGACY_LAYER_LOGO));assert(indexOf(LEGACY_LAYER_DEBUG)>indexOf(LEGACY_LAYER_UI));assert(overlay?indexOf(LEGACY_LAYER_NOTIFICATIONS)==count-1:indexOf(LEGACY_LAYER_NOTIFICATIONS)==-1);}puts("PASS: production GPU layer order keeps menu below logo and notifications above logo, UI and debug; settings retains explicit ordering");return 0;}
