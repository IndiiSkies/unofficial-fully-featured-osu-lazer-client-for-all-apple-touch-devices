#ifndef LEGACY_GPU_LAYER_ORDER_H
#define LEGACY_GPU_LAYER_ORDER_H
enum {LEGACY_LAYER_BACKGROUND,LEGACY_LAYER_SONGS,LEGACY_LAYER_LOGO,LEGACY_LAYER_UI,LEGACY_LAYER_DEBUG,LEGACY_LAYER_NOTIFICATIONS};
static inline void legacy_gpu_compose_layers(int logoBehindUI,int hasNotifications,void (*draw)(int,void *),void *state){
 draw(LEGACY_LAYER_BACKGROUND,state);draw(LEGACY_LAYER_SONGS,state);
 if(logoBehindUI)draw(LEGACY_LAYER_LOGO,state);
 draw(LEGACY_LAYER_UI,state);
 if(!logoBehindUI)draw(LEGACY_LAYER_LOGO,state);
 draw(LEGACY_LAYER_DEBUG,state);
 if(hasNotifications)draw(LEGACY_LAYER_NOTIFICATIONS,state);
}
#endif
