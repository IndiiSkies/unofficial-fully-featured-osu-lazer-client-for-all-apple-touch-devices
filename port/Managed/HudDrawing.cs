using System;
namespace OsuLegacy {
 public sealed class HudSurface : IUiSurface,ITextWidthSurface {
  private IUiSurface target;private IHudDrawingSurface transforms;private HudLayout layout;private bool reduced;
  private readonly HudItem[] active=new HudItem[48];private int count;private HudBounds bounds;private bool singleTransform,lastTransform;
  public void Setup(IUiSurface surface,HudLayout value,bool low){target=surface;transforms=surface as IHudDrawingSurface;layout=value;reduced=low;}
  private bool Identity(HudItem item){return item.X==0 && item.Y==0 && item.Scale==1 && item.Angle==0 && item.Opacity==1;}
  private void Push(HudItem item){transforms.PushHud(bounds.X+item.X,bounds.Y+item.Y,item.Scale,item.Angle*Math.PI/180,item.Opacity);}
  public void Begin(string kind){count=0;bounds=HudLayout.Bounds(kind,layout.Mode,reduced);foreach(var item in layout.Items)if(item.Kind==kind && item.Visible && item.Opacity>0 && count<active.Length)active[count++]=item;singleTransform=count==1 && !Identity(active[0]) && transforms!=null;if(singleTransform)Push(active[0]);}
  public void End(){if(singleTransform)transforms.PopHud();singleTransform=false;count=0;}
  private bool Start(int i){lastTransform=count>1 && transforms!=null && !Identity(active[i]);if(lastTransform)Push(active[i]);return singleTransform || lastTransform;}
  private void Finish(){if(lastTransform)transforms.PopHud();}
  public void Rect(double x,double y,double w,double h,double r,double g,double b,double a,double slant){for(int i=0;i<count;i++){bool shifted=Start(i);target.Rect(x-(shifted?bounds.X:0),y-(shifted?bounds.Y:0),w,h,r,g,b,a,slant);Finish();}}
  public void Circle(double x,double y,double radius,double r,double g,double b,double a,bool fill){for(int i=0;i<count;i++){bool s=Start(i);target.Circle(x-(s?bounds.X:0),y-(s?bounds.Y:0),radius,r,g,b,a,fill);Finish();}}
  public void Text(double x,double y,string text,double size,double r,double g,double b){for(int i=0;i<count;i++){bool s=Start(i);target.Text(x-(s?bounds.X:0),y-(s?bounds.Y:0),text,size,r,g,b);Finish();}}
  public void Image(string name,double x,double y,double w,double h,double a){for(int i=0;i<count;i++){bool s=Start(i);target.Image(name,x-(s?bounds.X:0),y-(s?bounds.Y:0),w,h,a);Finish();}}
  public bool Artwork(string path,double x,double y,double w,double h,double a){bool ok=false;for(int i=0;i<count;i++){bool s=Start(i);ok|=target.Artwork(path,x-(s?bounds.X:0),y-(s?bounds.Y:0),w,h,a);Finish();}return ok;}
  public bool Background(string path,bool bundled,double dim){return target.Background(path,bundled,dim);}
  public void Polyline(double[] points,int n,double x,double y,double scale,double width,double r,double g,double b,double a){for(int i=0;i<count;i++){bool s=Start(i);target.Polyline(points,n,x-(s?bounds.X:0),y-(s?bounds.Y:0),scale,width,r,g,b,a);Finish();}}
  public void Arc(double x,double y,double radius,double progress,double width,double r,double g,double b,double a){for(int i=0;i<count;i++){bool s=Start(i);target.Arc(x-(s?bounds.X:0),y-(s?bounds.Y:0),radius,progress,width,r,g,b,a);Finish();}}
  public double TextWidth(string text,double size){var measure=target as ITextWidthSurface;return measure==null?text.Length*size*.5:measure.TextWidth(text,size);}
 }
}
