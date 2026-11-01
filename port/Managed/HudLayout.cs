using System;using System.IO;using System.Text;using System.Globalization;using System.Collections.Generic;
namespace OsuLegacy {
 public struct HudBounds {
  public double X,Y,W,H;public HudBounds(double x,double y,double w,double h){X=x;Y=y;W=w;H=h;}
  public bool Contains(double x,double y){return x>=X && x<=X+W && y>=Y && y<=Y+H;}
 }
 public sealed class HudItem {
  public int Id;public string Kind;public double X,Y,Scale=1,Angle,Opacity=1;public bool Visible=true;
  public HudItem Copy(){return new HudItem {Id=Id,Kind=Kind,X=X,Y=Y,Scale=Scale,Angle=Angle,Opacity=Opacity,Visible=Visible};}
 }
 public sealed class HudLayout {
  public static readonly string[] Kinds={"health","avatar","title","score","accuracy","combo","progress","pause","timing","leaderboard","break"};
  public static readonly string[] Names={"Health bar","Player avatar","Song title","Score","Accuracy","Combo","Song progress","Pause button","Hit timing","Self competition","Break display"};
  public readonly List<HudItem> Items=new List<HudItem>();public readonly int Mode;
  public HudLayout(int mode,bool defaults){Mode=mode;if(defaults)for(int i=0;i<Kinds.Length;i++)Items.Add(new HudItem {Id=mode*100+i+1,Kind=Kinds[i]});}
  public static bool Known(string kind){return Array.IndexOf(Kinds,kind)>=0;}
  public static string Name(string kind){int i=Array.IndexOf(Kinds,kind);return i<0?kind:Names[i];}
  public HudItem Find(int id){return Items.Find(delegate(HudItem item){return item.Id==id;});}
  public bool Available(string kind,bool reduced){return !(kind=="avatar" && reduced) && !(kind=="timing" && (reduced || (Mode!=0 && Mode!=3))) && !(kind=="leaderboard" && Mode!=0);}
  public static HudBounds Bounds(string kind,int mode,bool reduced){
   switch(kind){case "health":return reduced?new HudBounds(10,8,150,7):new HudBounds(69,7,140,26);case "avatar":return new HudBounds(40,8,28,28);case "title":return new HudBounds(reduced?10:76,reduced?17:33,210,14);case "score":return new HudBounds(364,5,116,22);case "accuracy":return new HudBounds(392,25,88,15);case "combo":return mode==1?new HudBounds(80,180,130,51):mode==2?new HudBounds(180,203,120,25):mode==3?new HudBounds(170,100,140,42):new HudBounds(10,263,160,52);case "progress":return new HudBounds(0,298,480,22);case "pause":return new HudBounds(0,0,34,34);case "timing":return mode==3?new HudBounds(195,231,90,9):new HudBounds(454,83,26,165);case "leaderboard":return new HudBounds(0,105,113,108);default:return new HudBounds(45,130,390,93);}
  }
  public HudBounds DisplayBounds(HudItem item,bool reduced){
   HudBounds b=Bounds(item.Kind,Mode,reduced);double a=item.Angle*Math.PI/180,co=Math.Cos(a)*item.Scale,si=Math.Sin(a)*item.Scale;
   double x=b.X+item.X,y=b.Y+item.Y,minX=x,maxX=x,minY=y,maxY=y;
   for(int i=1;i<4;i++){double px=(i==1 || i==2)?b.W:0,py=i>=2?b.H:0,xx=x+px*co-py*si,yy=y+px*si+py*co;minX=Math.Min(minX,xx);maxX=Math.Max(maxX,xx);minY=Math.Min(minY,yy);maxY=Math.Max(maxY,yy);}return new HudBounds(minX,minY,maxX-minX,maxY-minY);
  }
  public bool Hit(HudItem item,double x,double y,bool reduced,double margin){
   if(!item.Visible)return false;HudBounds b=Bounds(item.Kind,Mode,reduced);double a=-item.Angle*Math.PI/180,dx=x-b.X-item.X,dy=y-b.Y-item.Y;
   double px=(dx*Math.Cos(a)-dy*Math.Sin(a))/item.Scale,py=(dx*Math.Sin(a)+dy*Math.Cos(a))/item.Scale;
   return px>=-margin && py>=-margin && px<=b.W+margin && py<=b.H+margin;
  }
  public bool Hit(string kind,double x,double y,bool reduced,double margin){foreach(var item in Items)if(item.Kind==kind && Hit(item,x,y,reduced,margin))return true;return false;}
  public bool CanRemove(HudItem item){if(item.Kind!="pause" || !item.Visible)return true;foreach(var other in Items)if(other!=item && other.Kind=="pause" && other.Visible)return true;return false;}
  public void KeepPauseReachable(HudItem item,bool reduced){if(item.Kind!="pause")return;item.Opacity=Math.Max(.1,item.Opacity);var b=DisplayBounds(item,reduced);if(b.X<-.000001)item.X-=b.X;if(b.X+b.W>480.000001)item.X-=b.X+b.W-480;if(b.Y<-.000001)item.Y-=b.Y;if(b.Y+b.H>320.000001)item.Y-=b.Y+b.H-320;}
 }
 public sealed class HudSkin {
  public readonly HudLayout[] Layouts=new HudLayout[4];public HudSkin(){for(int i=0;i<4;i++)Layouts[i]=new HudLayout(i,true);}
  private static string N(double n){return n.ToString("R",CultureInfo.InvariantCulture);}
  public string Serialize(){var text=new StringBuilder("osu-legacy-hud-v1\n");for(int m=0;m<4;m++)foreach(var item in Layouts[m].Items)text.Append(m).Append('\t').Append(item.Id).Append('\t').Append(item.Kind).Append('\t').Append(N(item.X)).Append('\t').Append(N(item.Y)).Append('\t').Append(N(item.Scale)).Append('\t').Append(N(item.Angle)).Append('\t').Append(N(item.Opacity)).Append('\t').Append(item.Visible?"1":"0").Append('\n');return text.ToString();}
  private static double Number(string text,double min,double max){double n;if(!double.TryParse(text,NumberStyles.Float,CultureInfo.InvariantCulture,out n) || Double.IsNaN(n) || Double.IsInfinity(n) || n<min || n>max)throw new FormatException("Invalid HUD component value");return n;}
  public static HudSkin Parse(string text){
   var skin=new HudSkin();var seen=new bool[4];using(var reader=new StringReader(text)){if(reader.ReadLine()!="osu-legacy-hud-v1")throw new FormatException("Unrecognised HUD layout file");string line;while((line=reader.ReadLine())!=null){if(line.Length==0)continue;string[] f=line.Split('\t');if(f.Length!=9)throw new FormatException("Malformed HUD component");int mode,id;if(!int.TryParse(f[0],out mode) || mode<0 || mode>3 || !int.TryParse(f[1],out id) || id<1)throw new FormatException("Invalid HUD component identity");if(!HudLayout.Known(f[2]))continue;if(!seen[mode]){skin.Layouts[mode].Items.Clear();seen[mode]=true;}var layout=skin.Layouts[mode];if(layout.Items.Count>=48 || layout.Find(id)!=null)throw new FormatException("Too many or duplicate HUD components");if(f[8]!="0" && f[8]!="1")throw new FormatException("Invalid visibility");var item=new HudItem {Id=id,Kind=f[2],X=Number(f[3],-2000,2000),Y=Number(f[4],-2000,2000),Scale=Number(f[5],.25,3),Angle=Number(f[6],-360,360),Opacity=Number(f[7],0,1),Visible=f[8]=="1"};layout.KeepPauseReachable(item,false);layout.Items.Add(item);}}
   for(int m=0;m<4;m++){bool pause=false;foreach(var item in skin.Layouts[m].Items)pause|=item.Kind=="pause" && item.Visible;if(!pause)skin.Layouts[m].Items.Add(new HudItem {Id=skin.NextId(),Kind="pause"});}return skin;
  }
  public int NextId(){int id=0;foreach(var layout in Layouts)foreach(var item in layout.Items)id=Math.Max(id,item.Id);return checked(id+1);}
  public HudSkin Copy(){return Parse(Serialize());}
  public static HudSkin Load(string path){return File.Exists(path)?Parse(File.ReadAllText(path)):new HudSkin();}
  public void Save(string path){string value=Serialize();Parse(value);string stage=path+".tmp",backup=path+".bak";Directory.CreateDirectory(Path.GetDirectoryName(path));File.WriteAllText(stage,value);if(File.Exists(backup))File.Delete(backup);bool moved=false;try{if(File.Exists(path)){File.Move(path,backup);moved=true;}File.Move(stage,path);}catch{if(moved && !File.Exists(path))File.Move(backup,path);throw;}}
 }
 public sealed class HudEditHistory {
  private readonly List<string> undo=new List<string>(),redo=new List<string>();
  public bool CanUndo {get{return undo.Count>0;}}public bool CanRedo {get{return redo.Count>0;}}
  public void Before(HudSkin skin){undo.Add(skin.Serialize());if(undo.Count>32)undo.RemoveAt(0);redo.Clear();}
  public HudSkin Undo(HudSkin current){if(!CanUndo)return current;redo.Add(current.Serialize());string value=undo[undo.Count-1];undo.RemoveAt(undo.Count-1);return HudSkin.Parse(value);}
  public HudSkin Redo(HudSkin current){if(!CanRedo)return current;undo.Add(current.Serialize());string value=redo[redo.Count-1];redo.RemoveAt(redo.Count-1);return HudSkin.Parse(value);}
 }
}
