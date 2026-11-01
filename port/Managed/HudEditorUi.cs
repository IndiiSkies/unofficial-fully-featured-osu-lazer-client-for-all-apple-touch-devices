using System;using System.IO;
namespace OsuLegacy {
 public sealed partial class LazerUi {
  public HudSkin SkinLayout=new HudSkin();public string HudSkinPath;public Action<bool> HudEditorActiveChanged;
  private HudSkin hudDraft;private HudEditHistory hudHistory;private string hudSaved;private UiScreen hudReturn;
  private int hudMode,hudSelected,hudAnchor;private bool hudGrid=true,hudAdd,hudConfirmClose,hudDragged;
  private double hudDownX,hudDownY,hudOldX,hudOldY,hudOldScale,hudResizeDistance;private bool hudPointer,hudResize,hudDirty;
  private readonly Session[] hudPreviews=new Session[4];private readonly GameplayView hudPreviewView=new GameplayView();
  public bool HudEditorDirty {get{return hudDirty;}}
  public int HudEditorMode {get{return hudMode;}}public HudSkin HudDraft {get{return hudDraft;}}
  public void OpenHudEditor(int ruleset){
   if(Screen==UiScreen.SkinEditor)return;hudReturn=Screen;hudMode=Math.Max(0,Math.Min(3,ruleset));hudDraft=SkinLayout.Copy();hudSaved=hudDraft.Serialize();hudDirty=false;hudHistory=new HudEditHistory();hudSelected=hudDraft.Layouts[hudMode].Items[0].Id;hudConfirmClose=hudAdd=hudPointer=false;
   for(int i=0;i<4;i++){var map=new Beatmap {Mode=i,Title="HUD layout preview",ManiaColumns=4};map.Circles.Add(new Circle {X=256,Y=192,Time=1000,Column=1});map.Circles.Add(new Circle {X=256,Y=192,Time=30000,Column=2});map.Breaks.Add(new double[]{10000,20000});hudPreviews[i]=new Session(map){Great=432,Miss=3,Combo=128,MaxCombo=160,Health=.75};}
   hudPreviewView.Competition=new SelfCompetition(new LocalScore {Score=280000,Accuracy=98.5,MaxCombo=160});Screen=UiScreen.SkinEditor;CloseNotifications();if(HudEditorActiveChanged!=null)HudEditorActiveChanged(true);
  }
  public bool SaveHudEditor(bool close){
   try{if(String.IsNullOrEmpty(HudSkinPath))throw new IOException("HUD layout path is unavailable.");hudDraft.Save(HudSkinPath);SkinLayout=hudDraft.Copy();hudSaved=hudDraft.Serialize();hudDirty=false;Notifications.Post("hud-save","Skin saved","Your HUD layouts are saved for all four rulesets.",NotificationKind.Success,false,null);if(close)FinishHudEditor();return true;}
   catch(Exception error){Notifications.Post("hud-save-error","Skin could not be saved",error.Message,NotificationKind.Error,false,null);return false;}
  }
  private void FinishHudEditor(){hudPointer=hudAdd=hudConfirmClose=false;Screen=hudReturn;if(HudEditorActiveChanged!=null)HudEditorActiveChanged(false);}
  private void RequestHudClose(){if(HudEditorDirty)hudConfirmClose=true;else FinishHudEditor();}
  private HudItem HudSelected {get{return hudDraft.Layouts[hudMode].Find(hudSelected);}}
  private void HudChange(Action<HudItem> change){var item=HudSelected;if(item==null)return;hudHistory.Before(hudDraft);change(item);hudDraft.Layouts[hudMode].KeepPauseReachable(item,ReducedGameplayEffects);hudDirty=hudDraft.Serialize()!=hudSaved;}
  private void HudUndo(){hudDraft=hudHistory.Undo(hudDraft);hudPointer=false;hudDirty=hudDraft.Serialize()!=hudSaved;}
  private void HudRedo(){hudDraft=hudHistory.Redo(hudDraft);hudPointer=false;hudDirty=hudDraft.Serialize()!=hudSaved;}
  private void CycleHud(int direction){var items=hudDraft.Layouts[hudMode].Items;if(items.Count==0)return;int index=items.FindIndex(delegate(HudItem item){return item.Id==hudSelected;});index=(index+direction+items.Count)%items.Count;hudSelected=items[index].Id;}
  public void AddHudComponent(string kind){
   if(!HudLayout.Known(kind))return;var layout=hudDraft.Layouts[hudMode];if(layout.Items.Count>=48){Notifications.Post("hud-limit","Component limit","A ruleset layout supports up to 48 components.",NotificationKind.Warning,false,null);return;}
   hudHistory.Before(hudDraft);var item=new HudItem {Id=hudDraft.NextId(),Kind=kind,X=8,Y=8};layout.Items.Add(item);hudSelected=item.Id;hudAdd=false;hudDirty=true;
  }
  private void DuplicateHud(){var selected=HudSelected;if(selected==null)return;var layout=hudDraft.Layouts[hudMode];if(layout.Items.Count>=48)return;hudHistory.Before(hudDraft);var item=selected.Copy();item.Id=hudDraft.NextId();item.X+=8;item.Y+=8;layout.KeepPauseReachable(item,ReducedGameplayEffects);layout.Items.Add(item);hudSelected=item.Id;hudDirty=true;}
  private bool HudMayRemove(){var selected=HudSelected;if(selected==null)return false;if(hudDraft.Layouts[hudMode].CanRemove(selected))return true;Notifications.Post("hud-pause","Keep a pause button","This touch device needs at least one visible pause button.",NotificationKind.Warning,false,null);return false;}
  private void DeleteHud(){if(!HudMayRemove())return;hudHistory.Before(hudDraft);hudDraft.Layouts[hudMode].Items.Remove(HudSelected);hudSelected=0;hudDirty=hudDraft.Serialize()!=hudSaved;}
  private void ResetHudItem(HudItem item){item.X=item.Y=item.Angle=0;item.Scale=item.Opacity=1;item.Visible=true;}
  private void AlignHud(){HudChange(delegate(HudItem item){var layout=hudDraft.Layouts[hudMode];var b=layout.DisplayBounds(item,ReducedGameplayEffects);hudAnchor=(hudAnchor+1)%9;item.X+=(480-b.W)*(hudAnchor%3)/2-b.X;item.Y+=(320-b.H)*(hudAnchor/3)/2-b.Y;});}
  private void HudEditorPointer(int phase,double x,double y){
   if(hudConfirmClose || hudAdd){if(phase==0)Tap(x,y);return;}
   if(phase==0){
    if(x>=360 || y<28 || y>=268){Tap(x,y);return;}
    double px=x/.75,py=(y-28)/.75;var layout=hudDraft.Layouts[hudMode];HudItem item=null;hudResize=false;
    if(HudSelected!=null){var b=layout.DisplayBounds(HudSelected,ReducedGameplayEffects);if(Math.Abs(px-b.X-b.W)<8 && Math.Abs(py-b.Y-b.H)<8){item=HudSelected;hudResize=true;var origin=HudLayout.Bounds(item.Kind,hudMode,ReducedGameplayEffects);hudResizeDistance=Math.Max(1,Math.Sqrt(Math.Pow(px-origin.X-item.X,2)+Math.Pow(py-origin.Y-item.Y,2)));hudOldScale=item.Scale;}}
    if(item==null)for(int i=layout.Items.Count-1;i>=0;i--)if(layout.Available(layout.Items[i].Kind,ReducedGameplayEffects) && layout.Hit(layout.Items[i],px,py,ReducedGameplayEffects,3)){item=layout.Items[i];break;}
    if(item==null){hudSelected=0;return;}hudSelected=item.Id;hudPointer=true;hudDragged=false;hudDownX=px;hudDownY=py;hudOldX=item.X;hudOldY=item.Y;
   }else if(phase==1 && hudPointer && HudSelected!=null){double dx=x/.75-hudDownX,dy=(y-28)/.75-hudDownY;if(!hudDragged && Math.Abs(dx)+Math.Abs(dy)>1){hudHistory.Before(hudDraft);hudDragged=true;hudDirty=true;}if(hudDragged){if(hudResize){var origin=HudLayout.Bounds(HudSelected.Kind,hudMode,ReducedGameplayEffects);double distance=Math.Sqrt(Math.Pow(x/.75-origin.X-HudSelected.X,2)+Math.Pow((y-28)/.75-origin.Y-HudSelected.Y,2));HudSelected.Scale=Math.Max(.25,Math.Min(3,hudOldScale*distance/hudResizeDistance));if(hudGrid)HudSelected.Scale=Math.Round(HudSelected.Scale*20)/20;}else {HudSelected.X=Math.Max(-1000,Math.Min(1000,hudOldX+dx));HudSelected.Y=Math.Max(-1000,Math.Min(1000,hudOldY+dy));if(hudGrid){HudSelected.X=Math.Round(HudSelected.X/4)*4;HudSelected.Y=Math.Round(HudSelected.Y/4)*4;}}hudDraft.Layouts[hudMode].KeepPauseReachable(HudSelected,ReducedGameplayEffects);}}
   else if(phase==2 || phase==3){hudPointer=false;hudDirty=hudDraft.Serialize()!=hudSaved;}
  }
  private void HudSmall(double x,double y,double w,string title,Action action){Button(x,y,w,21,title,.18,.13,.25,action,0);}
  private void DrawHudEditor(){
   IMenuLogoSurface logo=surface as IMenuLogoSurface;if(logo!=null)logo.MenuLogo(0,0,0,false);
   surface.Background(null,false,.3);Rect(0,0,480,320,.025,.023,.04,1);Rect(0,28,360,240,.06,.055,.09,1);
   if(hudGrid)for(int x=0;x<=360;x+=12)Rect(x,28,1,240,.3,.28,.4,.15);if(hudGrid)for(int y=28;y<=268;y+=12)Rect(0,y,360,1,.3,.28,.4,.15);
   IHudDrawingSurface transforms=surface as IHudDrawingSurface;
   if(transforms!=null){transforms.PushHud(0,28,.75,0,1);try{hudPreviewView.Layout=hudDraft.Layouts[hudMode];hudPreviewView.ReducedEffects=ReducedGameplayEffects;hudPreviewView.ProfileIcon=ProfileIcon;hudPreviewView.DrawHudPreview(surface,hudPreviews[hudMode],HudSelected!=null && HudSelected.Kind=="break"?12000:5000);}finally{transforms.PopHud();}}
   var layout=hudDraft.Layouts[hudMode];var selected=HudSelected;
   foreach(var item in layout.Items){if(!layout.Available(item.Kind,ReducedGameplayEffects) || !item.Visible)continue;var b=layout.DisplayBounds(item,ReducedGameplayEffects);double bx=b.X*.75,by=28+b.Y*.75,w=b.W*.75,h=b.H*.75;if(bx<0 || by<28 || bx+w>360 || by+h>268)continue;double alpha=item==selected?1:.22;surface.Polyline(new double[]{bx,by,bx+w,by,bx+w,by+h,bx,by+h,bx,by},5,0,0,1,item==selected?1.5:.7,.8,.6,1,alpha);if(item==selected)surface.Circle(bx+w,by+h,3,1,.75,.3,1,true);}
   // Chrome is drawn after the preview so it remains usable over off-canvas items.
   Rect(0,0,480,28,.1,.075,.15,1);Text(5,8,"skin editor",12);
   for(int i=0;i<4;i++){int m=i;double x=83+i*26;surface.Image("toolbar-ruleset-"+new string[]{"osu","taiko","catch","mania"}[i]+".png",x+4,5,18,18,1);if(m==hudMode)Rect(x+3,25,20,2,1,.45,.72,1);hits.Add(new Hit {X=x,Y=0,Width=26,Height=28,Action=delegate {hudMode=m;hudSelected=0;}});}
   HudSmall(190,3,43,"undo",HudUndo);HudSmall(235,3,43,"redo",HudRedo);
   HudSmall(280,3,52,"reset",delegate {hudHistory.Before(hudDraft);hudDraft.Layouts[hudMode]=new HudLayout(hudMode,true);hudSelected=0;hudDirty=hudDraft.Serialize()!=hudSaved;});
   HudSmall(334,3,67,HudEditorDirty?"save *":"save",delegate {SaveHudEditor(false);});HudSmall(403,3,73,"close",RequestHudClose);
   Rect(362,28,118,292,.10,.075,.15,1);Text(368,35,"ITEM",10);HudSmall(420,30,24,"<",delegate {CycleHud(-1);});HudSmall(446,30,24,">",delegate {CycleHud(1);});
   if(selected==null)Muted(368,55,"Tap a HUD item",9);
   else {
    Text(368,54,HudLayout.Name(selected.Kind),9);var sourceBounds=HudLayout.Bounds(selected.Kind,hudMode,ReducedGameplayEffects);Muted(368,70,"X "+Math.Round(sourceBounds.X+selected.X)+"  Y "+Math.Round(sourceBounds.Y+selected.Y),8);
    HudSmall(368,85,24,"<",delegate {HudChange(delegate(HudItem item){item.X-=hudGrid?4:1;});});HudSmall(394,85,24,">",delegate {HudChange(delegate(HudItem item){item.X+=hudGrid?4:1;});});HudSmall(420,85,24,"^",delegate {HudChange(delegate(HudItem item){item.Y-=hudGrid?4:1;});});HudSmall(446,85,24,"v",delegate {HudChange(delegate(HudItem item){item.Y+=hudGrid?4:1;});});
    Muted(368,113,"Scale "+selected.Scale.ToString("F2"),9);HudSmall(420,110,24,"-",delegate {HudChange(delegate(HudItem item){item.Scale=Math.Max(.25,item.Scale-.1);});});HudSmall(446,110,24,"+",delegate {HudChange(delegate(HudItem item){item.Scale=Math.Min(3,item.Scale+.1);});});
    Muted(368,138,"Rotate "+selected.Angle,8);HudSmall(420,135,24,"-",delegate {HudChange(delegate(HudItem item){item.Angle=item.Angle<=-180?180:item.Angle-15;});});HudSmall(446,135,24,"+",delegate {HudChange(delegate(HudItem item){item.Angle=item.Angle>=180?-180:item.Angle+15;});});
    Muted(368,163,"Alpha "+Math.Round(selected.Opacity*100),8);HudSmall(420,160,24,"-",delegate {HudChange(delegate(HudItem item){item.Opacity=Math.Max(.1,item.Opacity-.1);});});HudSmall(446,160,24,"+",delegate {HudChange(delegate(HudItem item){item.Opacity=Math.Min(1,item.Opacity+.1);});});
    HudSmall(368,186,102,selected.Visible?"hide":"show",delegate {if(selected.Visible && !HudMayRemove())return;HudChange(delegate(HudItem item){item.Visible=!item.Visible;});});
    HudSmall(368,211,49,"align",AlignHud);HudSmall(420,211,50,"default",delegate {HudChange(ResetHudItem);});
    HudSmall(368,236,49,"copy",DuplicateHud);HudSmall(420,236,50,"delete",DeleteHud);
    if(!layout.Available(selected.Kind,ReducedGameplayEffects))Muted(6,303,"Hidden by current ruleset/effects setting.",9);
   }
   HudSmall(368,270,102,"+ component",delegate {hudAdd=true;});HudSmall(368,296,102,hudGrid?"snap: 4px":"snap: off",delegate {hudGrid=!hudGrid;});
   Rect(0,270,360,50,.08,.06,.12,1);Text(7,278,"Drag to move. Corner handle resizes.",10);Muted(7,295,selected!=null && !layout.Available(selected.Kind,ReducedGameplayEffects)?"Not shown with this ruleset/effects setting.":"Per-ruleset layouts. Preview values only.",9);
   if(hudAdd){Rect(0,28,360,240,.04,.03,.07,.97);Text(10,39,"Add HUD component",15);for(int i=0;i<HudLayout.Kinds.Length;i++){string kind=HudLayout.Kinds[i];Button(8+(i%3)*116,69+(i/3)*42,110,35,HudLayout.Names[i],.24,.16,.32,delegate {AddHudComponent(kind);},0);}HudSmall(250,239,102,"cancel",delegate {hudAdd=false;});}
   if(hudConfirmClose){hits.Clear();Rect(0,0,480,320,0,0,0,.8);Text(100,91,"Save HUD changes?",22);Button(88,139,304,35,"save and close",.25,.55,.3,delegate {SaveHudEditor(true);},0);Button(88,181,304,35,"discard changes",.65,.18,.25,FinishHudEditor,0);Button(88,223,304,35,"keep editing",.25,.17,.35,delegate {hudConfirmClose=false;},0);}
  }
 }
}
