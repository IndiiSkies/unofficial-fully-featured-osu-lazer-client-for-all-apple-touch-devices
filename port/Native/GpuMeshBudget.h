#ifndef LEGACY_GPU_MESH_BUDGET_H
#define LEGACY_GPU_MESH_BUDGET_H
#define LEGACY_SLIDER_CACHE_BYTES (2u*1024u*1024u)
static unsigned legacy_slider_mesh_capacity(int points){
    if(points<1 || points>100000)return 0;
    unsigned count=2u*((unsigned)(points-1)*18u+432u);
    return count<=LEGACY_SLIDER_CACHE_BYTES/32u?count:0;
}
#endif
