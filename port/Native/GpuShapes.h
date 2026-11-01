void legacy_gpu_rect(double x,double y,double w,double h,double r,double g,double b,double a,double slant) {
    if(w<=0 || h<=0 || a<=0)return;useTexture(0);quad(vertex(x+slant,y,r,g,b,a),vertex(x+w,y,r,g,b,a),vertex(x,y+h,r,g,b,a),vertex(x+w-slant,y+h,r,g,b,a));
    // Axis-aligned UI rectangles already land on pixel boundaries. Smooth the
    // diagonal sides of the parallelogram buttons with a one-pixel fringe.
    if(slant!=0){float length=hypotf(h,slant),nx=h/length,ny=slant/length;
        quad(vertex(x+slant,y,r,g,b,a),vertex(x,y+h,r,g,b,a),vertex(x+slant-nx,y-ny,r,g,b,0),vertex(x-nx,y+h-ny,r,g,b,0));
        quad(vertex(x+w,y,r,g,b,a),vertex(x+w-slant,y+h,r,g,b,a),vertex(x+w+nx,y+ny,r,g,b,0),vertex(x+w-slant+nx,y+h+ny,r,g,b,0));}
}
static void disc(float x,float y,float radius,float r,float g,float b,float a) {
    if(radius<=0)return;
    float inner=fmaxf(0,radius-.5f),outer=radius+.5f;int step=radius<64?2:1;
    Vertex centre=vertex(x,y,r,g,b,a);
    for(int i=0;i<CIRCLE_SEGMENTS;i+=step){int j=i+step;
        Vertex ai=vertex(x+unitX[i]*inner,y+unitY[i]*inner,r,g,b,a),bi=vertex(x+unitX[j]*inner,y+unitY[j]*inner,r,g,b,a);
        triangle(centre,ai,bi);
        quad(ai,bi,vertex(x+unitX[i]*outer,y+unitY[i]*outer,r,g,b,0),vertex(x+unitX[j]*outer,y+unitY[j]*outer,r,g,b,0));}
}
void legacy_gpu_ellipse(double x,double y,double rx,double ry,double r,double g,double b,double a){
    if(rx<=0 || ry<=0 || a<=0)return;useTexture(0);Vertex centre=vertex(x,y,r,g,b,a);
    float ix=fmax(0,rx-.5),iy=fmax(0,ry-.5);
    for(int i=0;i<CIRCLE_SEGMENTS;i+=2){int j=i+2;
        Vertex ai=vertex(x+unitX[i]*ix,y+unitY[i]*iy,r,g,b,a),bi=vertex(x+unitX[j]*ix,y+unitY[j]*iy,r,g,b,a);
        triangle(centre,ai,bi);quad(ai,bi,vertex(x+unitX[i]*(rx+.5),y+unitY[i]*(ry+.5),r,g,b,0),vertex(x+unitX[j]*(rx+.5),y+unitY[j]*(ry+.5),r,g,b,0));
    }
}
static void gpu_arc_float(float x,float y,float radius,float progress,float width,float r,float g,float b,float a) {
    if(radius<=0 || width<=0 || progress<=0 || a<=0)return;useTexture(0);
    int resolution=radius<64?CIRCLE_SEGMENTS/2:CIRCLE_SEGMENTS;
    int segments=fmaxf(2,ceilf(fminf(1,progress)*resolution));float inner=fmaxf(0,radius-width/2),outer=radius+width/2;
    float innerCore=fminf(radius,inner+.5f),outerCore=fmaxf(radius,outer-.5f),innerEdge=fmaxf(0,inner-.5f),outerEdge=outer+.5f;
    float coreAlpha=a*fminf(1,width);
    float px=0,py=-1,co=0,si=0;if(progress<1){float delta=progress*(float)(2*M_PI)/segments;co=cosf(delta);si=sinf(delta);}
    for(int i=1;i<=segments;i++) {
        float nx,ny;
        if(progress>=1){int index=i*(CIRCLE_SEGMENTS/resolution);nx=unitY[index];ny=-unitX[index];}else {nx=px*co-py*si;ny=px*si+py*co;}
        Vertex pi=vertex(x+px*innerCore,y+py*innerCore,r,g,b,coreAlpha),po=vertex(x+px*outerCore,y+py*outerCore,r,g,b,coreAlpha);
        Vertex ni=vertex(x+nx*innerCore,y+ny*innerCore,r,g,b,coreAlpha),no=vertex(x+nx*outerCore,y+ny*outerCore,r,g,b,coreAlpha);
        quad(pi,po,ni,no);
        quad(po,vertex(x+px*outerEdge,y+py*outerEdge,r,g,b,0),no,vertex(x+nx*outerEdge,y+ny*outerEdge,r,g,b,0));
        if(inner>0)quad(vertex(x+px*innerEdge,y+py*innerEdge,r,g,b,0),pi,vertex(x+nx*innerEdge,y+ny*innerEdge,r,g,b,0),ni);
        px=nx;py=ny;
    }
}
void legacy_gpu_arc(double x,double y,double radius,double progress,double width,double r,double g,double b,double a){gpu_arc_float(x,y,radius,progress,width,r,g,b,a);}
void legacy_gpu_circle(double x,double y,double radius,double r,double g,double b,double a,int fill) {
    useTexture(0);if(fill)disc(x,y,radius,r,g,b,a);else gpu_arc_float(x,y,radius,1,2,r,g,b,a);
}
void legacy_gpu_path(const double *points,int count,double x,double y,double scale,double width,double r,double g,double b,double a) {
    if(!points || count<1 || width<=0 || scale<=0 || a<=0)return;useTexture(0);float half=width*scale/2,anx,any;
    float core=fmax(0,half-.5f)/half,edge=(half+.5f)/half,alpha=a*fmin(1,width*scale);
    legacy_gpu_stroke_offset(points,count,0,half,&anx,&any);
    for(int i=1;i<count;i++) {
        float ax=x+points[(i-1)*2]*scale,ay=y+points[(i-1)*2+1]*scale,bx=x+points[i*2]*scale,by=y+points[i*2+1]*scale;
        float bnx,bny;legacy_gpu_stroke_offset(points,count,i,half,&bnx,&bny);
        Vertex ap=vertex(ax+anx*core,ay+any*core,r,g,b,alpha),am=vertex(ax-anx*core,ay-any*core,r,g,b,alpha);
        Vertex bp=vertex(bx+bnx*core,by+bny*core,r,g,b,alpha),bm=vertex(bx-bnx*core,by-bny*core,r,g,b,alpha);
        quad(ap,am,bp,bm);
        quad(ap,bp,vertex(ax+anx*edge,ay+any*edge,r,g,b,0),vertex(bx+bnx*edge,by+bny*edge,r,g,b,0));
        quad(am,bm,vertex(ax-anx*edge,ay-any*edge,r,g,b,0),vertex(bx-bnx*edge,by-bny*edge,r,g,b,0));anx=bnx;any=bny;
    }
    if(count==1){disc(x+points[0]*scale,y+points[1]*scale,half,r,g,b,a);return;}
    // Tiny outline particles need only a few cap segments: their silhouette
    // error remains below the alpha fringe, avoiding thousands of tiny faces.
    int capSegments=half<2?4:half<8?8:24,capStep=CIRCLE_SEGMENTS/(2*capSegments);
    for(int end=0;end<2;end++) {
        int i=end?count-1:0;float nx,ny;legacy_gpu_stroke_offset(points,count,i,half,&nx,&ny);
        float cx=x+points[i*2]*scale,cy=y+points[i*2+1]*scale;if(end){nx=-nx;ny=-ny;}
        for(int j=0;j<capSegments;j++){int aa=j*capStep,bb=(j+1)*capStep;
            float ax=nx*unitX[aa]-ny*unitY[aa],ay=ny*unitX[aa]+nx*unitY[aa],bx=nx*unitX[bb]-ny*unitY[bb],by=ny*unitX[bb]+nx*unitY[bb];
            Vertex ai=vertex(cx+ax*core,cy+ay*core,r,g,b,alpha),bi=vertex(cx+bx*core,cy+by*core,r,g,b,alpha);
            triangle(vertex(cx,cy,r,g,b,alpha),ai,bi);
            quad(ai,bi,vertex(cx+ax*edge,cy+ay*edge,r,g,b,0),vertex(cx+bx*edge,cy+by*edge,r,g,b,0));}
    }
}
