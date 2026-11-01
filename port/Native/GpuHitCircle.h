// Exact existing layered hit-circle geometry, suitable for immutable VBOs.
static void legacy_gpu_hitcircle_geometry(double x,double y,double radius,double r,double g,double b,int reduced){
    if(reduced){legacy_gpu_circle(x,y,radius,r*.4,g*.4,b*.4,1,1);legacy_gpu_circle(x,y,radius,1,1,1,1,0);return;}
    legacy_gpu_circle(x,y,radius,r*.2,g*.2,b*.2,1,1);
    legacy_gpu_circle(x,y,radius*.86,r,g,b,1,1);
    legacy_gpu_circle(x,y,radius*.69,r*.65,g*.65,b*.65,1,1);
    legacy_gpu_circle(x,y,radius*.48,r*.2,g*.2,b*.2,1,1);
    legacy_gpu_arc(x,y,radius,1,radius*.069,1,1,1,1);
}
