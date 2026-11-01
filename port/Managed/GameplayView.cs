using System;

namespace OsuLegacy
{
    public sealed class GameplayView
    {
        public double Scale=1,OriginX,OriginY;
        public double BackgroundDim=.35;
        public double CursorX=240,CursorY=160;
        public bool CursorVisible;private Session activeSession;private double activeTime,barrelAngle;
        public bool ReducedEffects;
        public bool ShowCursor=true;
        public string ProfileIcon="avatar-guest.png";
        private static readonly ArgonHealthPath healthPath=new ArgonHealthPath();
        private readonly double[] healthScratch=new double[64];
        private ArgonHealthAnimation healthAnimation;private Session healthSession;private int healthMisses;
        private void DrawArgonHealth(Session session,double time){
            if(healthSession!=session){healthSession=session;healthAnimation=new ArgonHealthAnimation();healthMisses=session.Miss+session.SliderTicksMissed+session.Misclicks;}
            int misses=session.Miss+session.SliderTicksMissed+session.Misclicks;healthAnimation.Update(session.Health,misses>healthMisses,time);healthMisses=misses;
            const double x=76,y=7,scale=.43;
            draw.Rect(69,11,10,1.3,.9,.94,1,.65,0);
            draw.Polyline(healthPath.Points,healthPath.Points.Length/2,x,y,scale,9,.08,.10,.14,.9);
            draw.Polyline(healthPath.Points,healthPath.Points.Length/2,x,y,scale,7,.75,.84,.9,.32);
            if(healthAnimation.Damage){int trailCount=healthPath.CopyRange(healthAnimation.Value,healthAnimation.Trail,healthScratch);if(trailCount>1){draw.Polyline(healthScratch,trailCount,x,y,scale,16,1,.1,.15,.15);draw.Polyline(healthScratch,trailCount,x,y,scale,7,1,.37,.37,.9);}}
            int count=healthPath.CopyRange(0,healthAnimation.Value,healthScratch);
            if(count>1){draw.Polyline(healthScratch,count,x,y,scale,18,.49,.84,.99,.10);draw.Polyline(healthScratch,count,x,y,scale,8,.49,.84,.99,.5);draw.Polyline(healthScratch,count,x,y,scale,3.6,1,1,1,1);}
        }
        public SelfCompetition Competition;
        private Beatmap catchMap;
        private double catchTime,catchPreempt;
        private static readonly int[][] CatchColours={new int[]{241,116,0},new int[]{0,241,53},new int[]{0,82,241},new int[]{241,0,0},new int[]{232,235,0},new int[]{92,0,241}};
        private IUiSurface draw;
        private readonly double[] arrowScratch=new double[6],arcScratch=new double[50];
        private static readonly double[][] pulp=PreparePulp();
        private static readonly double[] spinnerX=new double[32],spinnerY=new double[32];
        private int hudScore=-1,hudCombo=-1,hudPercent=-1;private double hudAccuracy=-1;
        private int breakSeconds=-1;private string breakSecondsText;private double breakNumberWidth,breakAccuracy=-1;private string breakAccuracyText,breakGrade,breakGradeText;
        public void DrawBreak(Session session,double time,int width,int height){
            if(session.Mods.Has("CN"))return;double start,end;if(!session.BreakTimeline.TryGet(time,out start,out end))return;
            double alpha=BreakTimeline.Alpha(start,end,time);if(alpha<=0)return;
            IUiSurface breakOriginal=draw;if(Layout!=null){hudDrawing.Setup(draw,Layout,ReducedEffects);draw=hudDrawing;hudDrawing.Begin("break");}
            try {
            double cx=width/2.0,cy=height/2.0,enter=1-Math.Pow(1-Math.Min(1,(time-start)/BreakTimeline.FadeDuration),5);
            draw.Rect(cx-95,cy-30,190,93,0,0,0,.24*alpha,0);
            int seconds=BreakTimeline.Seconds(end,time);if(seconds!=breakSeconds){breakSeconds=seconds;breakSecondsText=seconds.ToString();ITextWidthSurface measure=draw as ITextWidthSurface;breakNumberWidth=measure==null?breakSecondsText.Length*10:measure.TextWidth(breakSecondsText,20);}
            double numberX=cx-breakNumberWidth/2-(1-enter)*20;
            draw.Text(numberX,cy-27,breakSecondsText,20,.95*alpha,.96*alpha,alpha);
            double bar=width*.3*enter*BreakTimeline.RemainingBar(start,end,time);
            if(bar>0){draw.Rect(cx-bar/2,cy-1.6,bar,3.2,1,1,1,alpha,0);draw.Circle(cx-bar/2,cy,1.6,1,1,1,alpha,true);draw.Circle(cx+bar/2,cy,1.6,1,1,1,alpha,true);}
            if(session.Accuracy!=breakAccuracy){breakAccuracy=session.Accuracy;breakAccuracyText="Accuracy  "+breakAccuracy.ToString("F2")+"%";}
            string rank=session.Mania!=null?session.Mania.Rank:session.Catch!=null?LocalScoreStore.CatchRank(session.Accuracy):LocalScoreStore.Rank(session.Accuracy,session.Miss);
            if(rank!=breakGrade){breakGrade=rank;breakGradeText="Grade  "+rank;}
            double infoX=cx+(1-enter)*20;
            draw.Text(infoX-43,cy+9,"CURRENT PROGRESS",8,.8*alpha,.8*alpha,.85*alpha);
            draw.Text(infoX-42,cy+23,breakAccuracyText,10,.95*alpha,.96*alpha,alpha);
            draw.Text(infoX-25,cy+37,breakGradeText,10,.95*alpha,.96*alpha,alpha);
            for(int side=0;side<2;side++){
                double direction=side==0?1:-1,x=cx+(side==0?-1:1)*width*(.6-(.6-.22)*enter),size=11;
                arrowScratch[0]=x-direction*size;arrowScratch[1]=cy-size;arrowScratch[2]=x;arrowScratch[3]=cy;arrowScratch[4]=x-direction*size;arrowScratch[5]=cy+size;
                draw.Polyline(arrowScratch,3,0,0,1,7,.65,.8,1,.12*alpha);draw.Polyline(arrowScratch,3,0,0,1,2.4,1,1,1,alpha);
            }
            }finally{if(Layout!=null)hudDrawing.End();draw=breakOriginal;}
        }
        private string hudScoreText,hudComboText,hudAccuracyText,hudProgressText;
        private readonly int[] competitionScores={-1,-1},competitionCombos={-1,-1};private readonly double[] competitionAccuracies={-1,-1};
        private readonly string[] competitionScoreText=new string[2],competitionAccuracyText=new string[2];private long competitionGap=long.MinValue;private string competitionGapText;
        static GameplayView(){for(int i=0;i<32;i++){double angle=i*Math.PI/16;spinnerX[i]=Math.Cos(angle);spinnerY[i]=Math.Sin(angle);}}
        private static double[][] PreparePulp(){var result=new double[4][];for(int type=0;type<4;type++){int count=type<2?3:4;result[type]=new double[count*2];for(int i=0;i<count;i++){double angle=((type==0?60:type==1?0:type==2?45:0)+i*360.0/count)*Math.PI/180;result[type][i*2]=Math.Sin(angle);result[type][i*2+1]=Math.Cos(angle);}}return result;}
        private void Text(double x,double y,string value,double size){draw.Text(x,y,value,size,.95,.96,1);}
        private void Circle(PathPoint p,double radius,double r,double g,double b,double alpha,bool fill){draw.Circle(OriginX+p.X*Scale,OriginY+p.Y*Scale,radius*Scale,r,g,b,alpha,fill);}
        public PathPoint ToPlayfield(double x,double y){double px=(x-OriginX)/Scale,py=(y-OriginY)/Scale;if(activeSession!=null && activeSession.Mods.Has("BR")){double dx=(px-256)/.75,dy=(py-192)/.75;px=256+dx*Math.Cos(barrelAngle)+dy*Math.Sin(barrelAngle);py=192-dx*Math.Sin(barrelAngle)+dy*Math.Cos(barrelAngle);}return new PathPoint(px,py);}
        private static void ComboColour(int index,out double r,out double g,out double b)
        {switch(index%4){case 0:r=1;g=.30;b=.58;break;case 1:r=.35;g=.75;b=1;break;case 2:r=.72;g=.42;b=1;break;default:r=.25;g=1;b=.68;break;}}
        public HudLayout Layout;
        private readonly HudSurface hudDrawing=new HudSurface();
        private void HudBegin(string kind){if(Layout!=null)hudDrawing.Begin(kind);}
        private void HudEnd(){if(Layout!=null)hudDrawing.End();}
        public void DrawHudPreview(IUiSurface surface,Session session,double time){draw=surface;DrawHud(session,time,480,320);DrawBreak(session,time,480,320);}
        private void DrawHud(Session session,double time,int width,int height)
        {
            IUiSurface original=draw;if(Layout!=null){hudDrawing.Setup(original,Layout,ReducedEffects);draw=hudDrawing;}
            try {
                int score=session.TotalScore;if(score!=hudScore){hudScore=score;hudScoreText=score.ToString("00000000");}
                if(session.Accuracy!=hudAccuracy){hudAccuracy=session.Accuracy;hudAccuracyText=hudAccuracy.ToString("F2")+"%";}
                if(session.Combo!=hudCombo){hudCombo=session.Combo;hudComboText=hudCombo+"x";}
                HudBegin("health");if(ReducedEffects){draw.Rect(10,8,150,7,.12,.10,.16,.9,0);draw.Rect(10,8,150*session.Health,7,.95,.28,.56,1,0);}else DrawArgonHealth(session,time);HudEnd();
                HudBegin("avatar");if(!ReducedEffects)draw.Image(ProfileIcon,40,8,28,28,1);HudEnd();
                HudBegin("title");draw.Text(ReducedEffects?10:76,ReducedEffects?17:33,session.Map.Title,9,.82,.84,.90);HudEnd();
                HudBegin("score");draw.Text(width-116,5,hudScoreText,18,1,1,1);HudEnd();
                HudBegin("accuracy");draw.Text(width-88,25,hudAccuracyText,11,.88,.90,1);HudEnd();
                HudBegin("combo");
                if(session.Map.Mode==2){if(session.Combo>0)draw.Text(88.2962962962963+session.Catch.X*.5925925925925926-hudComboText.Length*4.5,216.925925925926,hudComboText,18,1,1,1);}
                else if(session.Map.Mode==3){if(session.Combo>0){string combo=session.Combo.ToString();draw.Text(240-combo.Length*7,106,combo,28,1,1,1);}}
                else {draw.Text(session.Map.Mode==1?82:11,session.Map.Mode==1?185:height-57,hudComboText,34,1,1,1);draw.Text(session.Map.Mode==1?84:13,session.Map.Mode==1?219:height-23,"combo",9,.75,.78,.86);}HudEnd();
                HudBegin("progress");double progress=session.Progress(time);draw.Rect(0,height-5,width,5,.08,.06,.12,.95,0);draw.Rect(0,height-5,width*progress,5,.95,.28,.56,1,0);
                int percent=(int)Math.Round(progress*100);if(percent!=hudPercent){hudPercent=percent;hudProgressText=percent+"%";}
                draw.Circle(width*progress,height-3,3,1,1,1,1,true);draw.Text(width-29,height-22,hudProgressText,8,.75,.78,.86);HudEnd();
                HudBegin("pause");draw.Rect(0,0,34,34,.08,.06,.12,.55,0);draw.Text(9,7,"II",13,1,1,1);HudEnd();
                HudBegin("timing");if(!ReducedEffects){if(session.Map.Mode==0)DrawHitError(session,time,width,height);else if(session.Mania!=null){double extent=session.Mania.Windows[1];draw.Rect(195,233,90,4,.08,.06,.12,.7,0);draw.Rect(239,231,1,8,1,1,1,.7,0);foreach(Session.HitTiming hit in session.HitTimings){double age=time-hit.Time;if(age<0 || age>3000)continue;double px=240+Math.Max(-1,Math.Min(1,hit.Offset/extent))*45;draw.Rect(px,232,1,6,.45,.8,1,1-age/3000,0);}}}HudEnd();
                HudBegin("leaderboard");if(session.Map.Mode==0)DrawCompetition(session,time);HudEnd();
            }finally{if(Layout!=null)hudDrawing.End();draw=original;}
        }
        private void DrawCompetition(Session session,double time)
        {
            SelfCompetition c=Competition;if(c==null)return;c.Update(session.TotalScore,time);
            double move=c.Passed?Math.Min(1,Math.Max(0,(time-c.PassedAt)/500)):0;
            if(ReducedEffects && c.Passed)move=1;
            double eased=1-Math.Pow(1-move,5),liveY=c.HasBest?153-43*eased:110;
            if(c.HasBest)CompetitionRow(110+43*eased,c.Passed?2:1,"Personal best",c.BestScore,c.BestAccuracy,c.BestCombo,!c.Passed,false);
            CompetitionRow(liveY,c.Position,"Guest",session.TotalScore,session.Accuracy,session.MaxCombo,c.Position==1,true);
            if(c.HasBest){long gap=c.Gap(session.TotalScore);if(gap!=competitionGap){competitionGap=gap;competitionGapText=gap>0?"+"+gap.ToString("N0"):gap==0?"Tied score":gap.ToString("N0");}draw.Text(9,199,competitionGapText,10,c.Passed?.65:1,c.Passed?1:.72,c.Passed?.3:.3);}
            if(c.Passed && time-c.PassedAt<2500){
                double alpha=Math.Min(1,(2500-(time-c.PassedAt))/300);
                draw.Rect(133,42,214,29,.10,.19,.06,.88*alpha,5);
                draw.Text(148,49,"New personal best!",16,.7,1,.35);
            }
        }
        private void CompetitionRow(double y,int rank,string name,int score,double accuracy,int combo,bool first,bool tracked)
        {
            int slot=tracked?1:0;if(competitionScores[slot]!=score){competitionScores[slot]=score;competitionScoreText[slot]=score.ToString("N0");}
            if(competitionAccuracies[slot]!=accuracy || competitionCombos[slot]!=combo){competitionAccuracies[slot]=accuracy;competitionCombos[slot]=combo;competitionAccuracyText[slot]=accuracy.ToString("F2")+"%  "+combo+"x";}
            double r=first?.57:tracked?1:.25,g=first?.85:tracked?.58:.24,b=first?.18:tracked?.20:.3;
            draw.Rect(0,y,111,38,.055,.045,.075,.80,5);
            draw.Rect(0,y,3,38,r,g,b,1,0);
            draw.Text(7,y+3,rank==1?"#1":"#2",11,r,g,b);
            draw.Text(27,y+3,name,10,1,1,1);
            draw.Text(8,y+15,competitionScoreText[slot],11,1,1,1);
            draw.Text(8,y+28,competitionAccuracyText[slot],7,.82,.84,.90);
        }
        private void DrawHitError(Session session,double time,int width,int height)
        {
            double x=width-11,y=height/2.0,range=session.Windows.WindowFor(osu.Game.Rulesets.Scoring.HitResult.Meh);
            draw.Rect(x-3,y-62,6,124,1,.65,.2,.45,0);
            double good=session.Windows.WindowFor(osu.Game.Rulesets.Scoring.HitResult.Ok)/range*62;
            double great=session.Windows.WindowFor(osu.Game.Rulesets.Scoring.HitResult.Great)/range*62;
            draw.Rect(x-3,y-good,6,good*2,.35,.9,.4,.65,0);draw.Rect(x-3,y-great,6,great*2,.35,.7,1,.85,0);
            draw.Rect(x-7,y,14,1,1,1,1,.9,0);
            foreach(Session.HitTiming hit in session.HitTimings) {
                double age=time-hit.Time;if(age<0 || age>4000)continue;
                double position=y+Math.Max(-1,Math.Min(1,hit.Offset/range))*62;
                draw.Rect(x-6,position,12,2,1,1,1,1-age/4000,0);
            }
            draw.Text(x-17,y-77,"early",7,.8,.85,.95);draw.Text(x-13,y+66,"late",7,.8,.85,.95);
        }
        private void ArgonCircle(Circle obj,double radius,double r,double g,double b)
        {
            PathPoint point=new PathPoint(obj.X,obj.Y);IHitCircleSurface cached=draw as IHitCircleSurface;
            if(cached!=null){cached.HitCircle(obj,OriginX+point.X*Scale,OriginY+point.Y*Scale,radius*Scale,r,g,b,ReducedEffects);return;}
            if(ReducedEffects){Circle(point,radius,r*.4,g*.4,b*.4,1,true);Circle(point,radius,1,1,1,1,false);return;}
            Circle(point,radius,r*.2,g*.2,b*.2,1,true);
            Circle(point,radius*.86,r,g,b,1,true);
            Circle(point,radius*.69,r*.65,g*.65,b*.65,1,true);
            Circle(point,radius*.48,r*.2,g*.2,b*.2,1,true);
            draw.Arc(OriginX+point.X*Scale,OriginY+point.Y*Scale,radius*Scale,1,radius*Scale*.069,1,1,1,1);
        }
        public void DrawCursor(IUiSurface surface,bool orange)
        {
            double r=(orange?16:10)*(orange || activeSession==null?1:activeSession.Mods.CursorScale);
            double alpha=orange || activeSession==null?1:activeSession.Mods.CursorAlpha(activeTime);IHudDrawingSurface cursorTransform=surface as IHudDrawingSurface;if(cursorTransform!=null)cursorTransform.PushHud(0,0,1,0,alpha);try {
            surface.Circle(CursorX,CursorY,r,orange?1:1,orange?.5:.45,orange?0:.7,.5,true);
            surface.Arc(CursorX,CursorY,r,1,2,orange?1:1,orange?.55:1,orange?.08:1,1);
            surface.Circle(CursorX,CursorY,r*.35,orange?1:.13,orange?.65:.36,orange?.10:.8,1,true);
            surface.Circle(CursorX,CursorY,1.5,1,1,1,1,true);
            }finally{if(cursorTransform!=null)cursorTransform.PopHud();}
        }
        public void DrawResumePrompt(IUiSurface surface)
        {
            surface.Rect(0,0,480,320,0,0,0,.35,0);
            surface.Rect(76,46,328,30,.06,.04,.09,.9,0);
            surface.Text(91,53,"Click the orange cursor to resume.",15,1,1,1);
            DrawCursor(surface,true);
        }
        private void DrawJudgements(Session session,double time)
        {
            for(int i=session.Judgements.Count-1;i>=0;i--){GameplayJudgement j=session.Judgements[i];double age=time-j.Time;if(age<0||age>700)continue;
                double alpha=1-age/700,px=OriginX+j.X*Scale,py=OriginY+j.Y*Scale-age*.018;
                if(j.Result=="X"){double[] a={-10,-10,10,10},b={-10,10,10,-10};draw.Polyline(a,2,px,py,1,5,1,.18,.30,alpha);draw.Polyline(b,2,px,py,1,5,1,.18,.30,alpha);}
                else if(session.Mods.Has("BU")){double radius=session.Radius*Scale*1.9*(1+age/700);draw.Circle(px,py,radius,.7,.8,1,alpha,false);}
                else {double r=j.Result=="300"?.35:j.Result=="100"?.35:1,g=j.Result=="300"?1:j.Result=="100"?.70:.65,b=j.Result=="300"?.72:j.Result=="100"?1:.20;draw.Text(px-11,py-11,j.Result,15,r,g,b);}
            }
        }
        public void Draw(IUiSurface surface,Session session,double time,int width,int height)
        {
            draw=surface;activeSession=session;activeTime=time;surface.Background(session.Map.BackgroundPath,false,BackgroundDim);session.Advance(time);if(session.Mods.Has("CN"))return;
            if(session.Map.Mode==1){DrawTaiko(session,time,width,height);return;}
            if(session.Catch!=null){DrawCatch(session,time,width,height);return;}
            if(session.Mania!=null){DrawMania(session,time,width,height);return;}
            Scale=Math.Min((width-18.0)/512,(height-48.0)/384);OriginX=(width-512*Scale)/2;OriginY=28+(height-48-384*Scale)/2;
            IHudDrawingSurface transform=surface as IHudDrawingSurface;bool barrel=session.Mods.Has("BR") && transform!=null;barrelAngle=time/60000*Math.PI;if(barrel){transform.PushHud(OriginX+256*Scale,OriginY+192*Scale,.75,barrelAngle,1);transform.PushHud(-OriginX-256*Scale,-OriginY-192*Scale,1,0,1);}
            for(int i=session.VisibleEnd(time)-1;i>=session.FirstPending;i--)
            {
                Circle obj=session.Map.Objects[i];if(obj.Judged)continue;var visual=session.Mods.Object(obj,time);bool moved=transform!=null && session.Mods.Settings!=null && session.Mods.Settings.Summary!="None";if(moved){transform.PushHud(OriginX+visual.X*Scale,OriginY+visual.Y*Scale,visual.Scale,visual.Rotation,visual.Alpha);transform.PushHud(-OriginX-obj.X*Scale,-OriginY-obj.Y*Scale,1,0,1);}try {if(obj.Kind==ObjectKind.Spinner){DrawSpinner(session,obj,time);continue;}
                PathPoint head=new PathPoint(obj.X,obj.Y);double cr,cg,cb;ComboColour(obj.ComboColourIndex,out cr,out cg,out cb);
                if(session.Map.ComboColours.Count>0){double[] colour=session.Map.ComboColours[obj.ComboColourIndex%session.Map.ComboColours.Count];cr=colour[0];cg=colour[1];cb=colour[2];}
                if(session.Mods.Has("SY")){bool isKiai;double fraction=session.Map.BeatPosition(obj.Time,out isKiai)%1;ComboColour((int)Math.Round(fraction*4),out cr,out cg,out cb);}
                if(obj.Kind==ObjectKind.Slider)
                {
                    ISliderBodySurface bodies=surface as ISliderBodySurface;
                    if(bodies!=null)bodies.SliderBody(obj.Path.Coordinates,obj.Path.Points.Count,OriginX,OriginY,Scale,session.Radius*2+3,session.Radius*2-3,cr,cg,cb);
                    else {surface.Polyline(obj.Path.Coordinates,obj.Path.Points.Count,OriginX,OriginY,Scale,session.Radius*2+3,cr,cg,cb,.85);
                    surface.Polyline(obj.Path.Coordinates,obj.Path.Points.Count,OriginX,OriginY,Scale,session.Radius*2-3,cr*.18,cg*.18,cb*.18,.96);}
                    int span=obj.SpanDuration<=0?0:Math.Max(0,Math.Min(obj.SpanCount-1,(int)((time-obj.Time)/obj.SpanDuration)));
                    foreach(SliderEvent e in obj.Events)if(e.Span==span&&!e.Judged&&!e.Tail&&!e.Repeat)Circle(obj.Path.Position(e.Progress),4,1,1,1,.8,true);
                    if(time>=obj.Time&&time<=obj.EndTime){PathPoint ball=obj.PositionAt(time);bool tracking=session.Tracking(obj,time);Circle(ball,session.Radius*1.6,cr,cg,cb,tracking?.75:.3,false);Circle(ball,session.Radius*.86,1,1,1,1,true);Circle(ball,session.Radius*.65,cr,cg,cb,1,true);
                        PathPoint next=obj.PositionAt(Math.Min(obj.EndTime,time+10));double dx=next.X-ball.X,dy=next.Y-ball.Y,length=Math.Sqrt(dx*dx+dy*dy),size=session.Radius*.3;
                        if(length>0){dx/=length;dy/=length;}else {dx=1;dy=0;}
                        const double co=-.702713076773554,si=.711473352790844;
                        arrowScratch[0]=ball.X+(dx*co-dy*si)*size;arrowScratch[1]=ball.Y+(dy*co+dx*si)*size;arrowScratch[2]=ball.X;arrowScratch[3]=ball.Y;arrowScratch[4]=ball.X+(dx*co+dy*si)*size;arrowScratch[5]=ball.Y+(dy*co-dx*si)*size;surface.Polyline(arrowScratch,3,OriginX,OriginY,Scale,3,1,1,1,1);
                    }
                    if(obj.SpanDuration>0&&span<obj.SpanCount-1){double[] arrow=span%2==0?obj.Path.ReverseAtEnd:obj.Path.ReverseAtStart;if(arrow.Length>0)surface.Polyline(arrow,3,OriginX,OriginY,Scale,4,1,1,1,1);}
                }
                if(!obj.HeadJudged){if(!session.Mods.Has("TC"))ArgonCircle(obj,session.Radius,cr,cg,cb);double remaining=obj.Time-time;if(remaining>0 && visual.Approach>0)Circle(head,session.Radius*visual.Approach,cr,cg,cb,.72,false);if(!session.Mods.Has("TC")){string number=obj.ComboNumber.ToString();double size=session.Radius*Scale*.8;Text(OriginX+head.X*Scale-size*.28*number.Length,OriginY+head.Y*Scale-size*.6,number,size);}}
                }finally{if(moved){transform.PopHud();transform.PopHud();}}
            }
            DrawJudgements(session,time);if(barrel){transform.PopHud();transform.PopHud();}DrawObscurity(session,time,width,height);DrawHud(session,time,width,height);
            if(session.Mods.Has("AL") || session.Mods.Has("SG")){draw.Rect(10,280,215,30,.3,.2,.5,.7,3);draw.Rect(255,280,215,30,.3,.2,.5,.7,3);draw.Text(70,288,"LEFT KEY",10,1,1,1);draw.Text(315,288,"RIGHT KEY",10,1,1,1);}
            if((CursorVisible || session.Mods.Automation || session.Mods.Has("AP")) && ShowCursor){if(session.Mods.Automation || session.Mods.Has("AP")){CursorX=OriginX+session.Mods.AimX*Scale;CursorY=OriginY+session.Mods.AimY*Scale;}DrawCursor(surface,false);}
        }
        private void DrawObscurity(Session session,double time,int width,int height){
            if(session.BreakTimeline.Active(time))return;
            if(session.Mods.Has("BL")){double value=Math.Max(0,Math.Min(1,session.Health)),closed=.6*value*value+.4*value;double raw=512*Scale,start=OriginX-raw*.05,end=OriginX+raw*1.05,panel=(end-start)*.5*closed;draw.Rect(0,0,Math.Max(0,start+panel),height,0,0,0,1,0);draw.Rect(end-panel,0,Math.Max(0,width-end+panel),height,0,0,0,1,0);}
            if(session.Mods.Has("FL")){double cx=CursorVisible?CursorX:width/2,cy=CursorVisible?CursorY:height/2,radius=90*(session.Combo>=200?.6:session.Combo>=100?.8:1);if(session.Catch!=null){cx=88.2963+session.Catch.X*.5925926;cy=262;}if(session.Mania!=null){cx=240;cy=247;radius=105;}if(session.Map.Mode==1){cx=100;cy=145;radius=100;}for(int y=0;y<height;y+=8){double dy=y+4-cy,half=Math.Abs(dy)>=radius?0:Math.Sqrt(radius*radius-dy*dy);draw.Rect(0,y,Math.Max(0,cx-half),8,0,0,0,1,0);draw.Rect(Math.Min(width,cx+half),y,Math.Max(0,width-cx-half),8,0,0,0,1,0);}}
        }
        private void DrawCatch(Session session,double time,int width,int height)
        {
            CatchSession game=session.Catch;const double scale=.5925925925925926,origin=88.2962962962963,plateY=261.925925925926;
            catchMap=session.Map;catchTime=time;catchPreempt=game.Preempt;
            for(int side=0;side<2;side++){
                double x=side==0?0:408;bool held=game.Direction==(side==0?-1:1);
                draw.Rect(x,34,72,142,.25,.22,.32,held && game.Dashing?.45:.10,0);
                draw.Rect(x,176,72,139,.55,.53,.62,held && !game.Dashing?.4:.13,0);
                draw.Text(x+17,151,"DASH",10,.85,.85,.95);draw.Text(x+17,199,"WALK",10,.85,.85,.95);
                draw.Text(x+27,218,side==0?"<":">",24,1,1,1);
            }
            draw.Rect(origin,plateY,512*scale,1,1,1,1,.13,0);
            for(int i=game.Next;i<game.Objects.Count;i++){
                CatchObject obj=game.Objects[i];double ahead=obj.Time-time;if(ahead>game.Preempt)break;
                double y=plateY-440*scale*ahead/game.Preempt;
                if(y<0)continue;
                double alpha=session.Map.CatchHidden?Math.Max(0,Math.Min(1,(ahead-game.Preempt*.44)/(game.Preempt*.16))):1;
                DrawCatchFruit(obj,origin+obj.X*scale,y,game.Scale*scale,alpha,Math.Min(1,ahead/500));
            }
            double cx=origin+game.X*scale,body=106.75*game.Scale*2*scale;
            bool kiai;session.Map.BeatPosition(time,out kiai);
            string state=game.FailedState?"fail":kiai?"kiai":"idle";
            string texture="catch-fruit-catcher-"+state+(game.Facing<0?"-left":"")+".png";
            if(!ReducedEffects && game.Dashing){for(int i=3;i>=1;i--)draw.Image(texture,cx-body*.478-game.Facing*i*5,plateY-16*game.Scale*2*scale,body*.956,body,.10*(4-i)/3);}
            draw.Image(texture,cx-body*.478,plateY-16*game.Scale*2*scale,body*.956,body,session.Mods.CursorAlpha(time));
            for(int i=0;i<game.Plate.Count;i++){
                CatchObject obj=game.Plate[i];DrawCatchFruit(obj,cx+obj.StackX*scale,plateY+obj.StackY*scale,game.Scale*scale*.5,1,0);
            }
            if(!ReducedEffects)foreach(CatchSession.Burst burst in game.Bursts){
                double age=time-burst.Time;if(age<0 || age>750)continue;double t=age/750;
                double dy=burst.Explode?(age<250?-50*Math.Sin(age/250*Math.PI/2):-50+100*(1-Math.Cos((age-250)/500*Math.PI/2))):75*(1-Math.Cos(t*Math.PI/2));
                double dx=burst.Explode?burst.Object.StackX*6*age/1000:0;
                DrawCatchFruit(burst.Object,origin+(burst.X+dx)*scale,plateY+(burst.Y+dy)*scale,game.Scale*scale*.5,1-t,0);
            }
            if(!ReducedEffects)foreach(CatchObject obj in game.Recent){
                double age=time-obj.JudgedAt;if(age<0 || age>500)continue;double t=age/500;
                if(!obj.Caught){DrawCatchFruit(obj,origin+obj.X*scale,plateY+t*t*90,game.Scale*scale,1-t,0);}
                else {double r,g,b;CatchColour(obj,out r,out g,out b);draw.Arc(origin+obj.X*scale,plateY,3+t*18,1,2,r,g,b,(1-t)*.6);}
            }
            if(game.HyperMultiplier>1){draw.Arc(cx,plateY,body*.5,1,3,1,.15,.25,.8);draw.Text(cx-20,plateY-66,"HYPER",9,1,.35,.4);}
            DrawObscurity(session,time,width,height);DrawHud(session,time,width,height);
        }
        private void DrawCatchFruit(CatchObject obj,double x,double y,double scale,double alpha,double border)
        {
            if(alpha<=0)return;
            obj.PrepareVisual();
            double progress=(catchTime-obj.Time+catchPreempt)/catchPreempt,angle;
            if(obj.Kind==CatchKind.Banana){double p=obj.Caught?Math.Min(1,progress):progress;angle=180*((obj.VisualA*2-1)*(1-p)+(obj.VisualB*2-1)*p);scale*=.6+1.6*obj.VisualC*(1-p);}
            else if(obj.Kind==CatchKind.Fruit)angle=(obj.VisualA-.5)*40;
            else angle=obj.VisualA*20+720*(catchTime-obj.Time+catchPreempt)/(catchPreempt+2000);
            ICatchSurface transform=draw as ICatchSurface;
            if(transform!=null){transform.Push(x,y,angle*Math.PI/180);try{DrawCatchFruitShape(obj,0,0,scale,alpha,border);}finally{transform.Pop();}}
            else DrawCatchFruitShape(obj,x,y,scale,alpha,border);
        }
        private void CatchColour(CatchObject obj,out double r,out double g,out double b)
        {
            int index=obj.Index+1;if(catchMap!=null && activeSession!=null && activeSession.Mods.Has("SY")){bool isKiai;index=(int)Math.Round((catchMap.BeatPosition(obj.Time,out isKiai)%1)*4);}
            if(catchMap!=null && catchMap.ComboColours.Count>0){double[] c=catchMap.ComboColours[index%catchMap.ComboColours.Count];r=c[0];g=c[1];b=c[2];}
            else {int[] c=CatchColours[index%CatchColours.Length];r=c[0]/255.0;g=c[1]/255.0;b=c[2]/255.0;}
            if(obj.Kind==CatchKind.Banana){obj.PrepareVisual();int variant=obj.VisualVariant;r=variant==2?214/255.0:1;g=(variant==0?240:variant==1?192:221)/255.0;b=variant==2?28/255.0:0;}
        }
        private void DrawCatchFruitShape(CatchObject obj,double x,double y,double scale,double alpha,double border)
        {
            double r,g,b;CatchColour(obj,out r,out g,out b);
            if(obj.Kind==CatchKind.Tiny || obj.Kind==CatchKind.Droplet){
                double radius=(obj.Kind==CatchKind.Tiny?8:16)*scale;
                draw.Circle(x,y,radius+2,r,g,b,.2*alpha,true);draw.Circle(x,y,radius,1,1,1,.9*alpha,true);return;
            }
            if(obj.Kind==CatchKind.Banana){
                draw.Circle(x,y-38.4*scale,4.4*scale,1,1,1,alpha,true);
                ICatchSurface ellipse=draw as ICatchSurface;
                if(ellipse!=null){if(!ReducedEffects)ellipse.Ellipse(x,y+6.4*scale,11*scale,28*scale,r,g,b,alpha*.25);ellipse.Ellipse(x,y+6.4*scale,6.512*scale,20.35*scale,1,1,1,alpha*.9);}
                else draw.Circle(x,y,10*scale,r,g,b,alpha,true);
                if(border>0)draw.Arc(x,y,64*scale,1,6.6*scale,1,1,1,border*alpha);
                return;
            }
            int type=obj.Index%4,count=type<2?3:4;
            draw.Circle(x,y-42.24*scale,4.4*scale,1,1,1,.9*alpha,true);
            for(int i=0;i<count;i++){
                double distance=(count==3?.15:.15/.925)*128*scale;
                double px=x+pulp[type][i*2]*distance,py=y+pulp[type][i*2+1]*distance,radius=8.8*(count==3?1:.925)*scale;
                if(!ReducedEffects)draw.Circle(px,py,radius*1.8,r,g,b,.22*alpha,true);
                draw.Circle(px,py,radius,1,1,1,.9*alpha,true);
            }
            if(border>0)draw.Arc(x,y,64*scale,1,6.6*scale,1,1,1,border*alpha);
            if(obj.HyperTarget>=0)draw.Arc(x,y,64*scale,1,13.2*scale,1,.15,.25,alpha*.8);
        }
        public void DrawCatchResume(IUiSurface surface,double elapsed)
        {
            surface.Rect(0,0,480,320,0,0,0,.35,0);surface.Circle(240,160,52,.14,.06,.20,1,true);
            surface.Arc(240,160,43,Math.Max(0,Math.Min(1,elapsed/2)),4,.8,.6,1,1);
            surface.Text(231,139,Math.Max(1,3-(int)(elapsed/2*3)).ToString(),32,1,1,1);
        }
        private void DrawMania(Session session,double time,int width,int height)
        {
            ManiaSession game=session.Mania;int count=game.Columns.Length;double w=480.0/count,line=260,top=42,speed=(line-top)/game.ScrollTime;
            draw.Rect(0,top,480,272,.025,.02,.04,.92,0);
            for(int col=0;col<count;col++){
                int display=game.Mirror?count-1-col:col;double x=display*w;
                bool centre=count%2==1 && display==count/2;double r=centre?1:display%2==0?.4:.78,g=centre?.73:display%2==0?.72:.51,b=centre?.22:1;
                bool held=game.Held[col]>0;
                draw.Rect(x+1,top,w-2,272,r,g,b,held?.17:display%2==0?.025:.055,0);
                draw.Rect(x,top,1,272,.7,.65,.8,.18,0);
                if(!ReducedEffects){double glow=held?.32:Math.Max(0,.22-(time-game.PressedAt[col])/.9/1000);if(glow>0)for(int j=0;j<4;j++)draw.Rect(x+2,line-16-j*20,w-4,20,r,g,b,glow*(1-j*.22),0);}
                var notes=game.Columns[col];int lower=game.FirstInColumn(col),upper=notes.Count;
                int a=lower,z=upper;while(a<z){int mid=(a+z)/2;if(notes[mid].Source.Time<=time+game.ScrollTime+80)a=mid+1;else z=mid;}upper=a;
                for(int i=upper-1;i>=lower;i--){ManiaNote n=notes[i];if(n.Complete)continue;Circle o=n.Source;if(session.Mods.Has("HD") && o.Time-time<game.ScrollTime*.45 && !n.Holding)continue;
                    double head=line-(o.Time-time)*speed,tail=line-(o.EndTime-time)*speed;
                    if(n.Hold){bool active=n.Holding && n.HeadHit && !n.Broken;if(active && time>=o.Time)head=line;
                        double y=Math.Max(top,tail),bottom=Math.Min(310,head);double alpha=n.Broken?.25:.78;
                        if(bottom>y){draw.Rect(x+Math.Max(4,w*.2),y,w-Math.Max(8,w*.4),bottom-y,r,g,b,alpha,0);if(!ReducedEffects)draw.Rect(x+w*.44,y,w*.12,bottom-y,1,1,1,active?.6:.16,0);}
                        if(tail>=top && tail<310)draw.Rect(x+3,tail-3,w-6,6,r,g,b,n.Broken?.35:1,2);
                    }
                    if((!n.HeadJudged || n.Hold) && head>=top && head<310){draw.Rect(x+3,head-6,w-6,12,r,g,b,n.Broken?.4:1,3);draw.Rect(x+5,head-5,w-10,3,1,1,1,n.Broken?.2:.8,1);}
                }
                draw.Rect(x+2,line+9,w-4,42,r,g,b,held?.65:.12,3);draw.Rect(x+3,line-2,w-6,4,1,1,1,held?1:.65,1);
                if(count<=10)draw.Text(x+w*.5-3,line+24,(display+1).ToString(),10,r,g,b);
            }
            draw.Rect(0,line-1,480,1,1,1,1,.5,0);
            if(time-game.LastJudgementAt<650){string text=game.LastJudgement;double fade=Math.Min(1,(650-(time-game.LastJudgementAt))/180);draw.Rect(172,151,136,25,.035,.025,.06,.7*fade,5);draw.Text(240-text.Length*4,156,text,13,text=="MISS"||text=="HOLD BREAK"?1:.6,text=="MISS"||text=="HOLD BREAK"?.25:1,text=="MAX"?.95:.6);}
            draw.Text(182,47,count+"K"+(session.Map.ManiaConverted?"  converted":""),9,.8,.78,.88);
            DrawObscurity(session,time,width,height);DrawHud(session,time,width,height);
        }
        private void DrawTaiko(Session session,double time,int width,int height)
        {
            const double target=88,lane=132,speed=.22;
            double scrollNow=session.Map.TaikoScrollPosition(time);
            draw.Rect(0,78,width,108,.045,.035,.06,.9,0);
            draw.Rect(0,80,width,2,.7,.65,.75,.65,0);draw.Rect(0,184,width,2,.7,.65,.75,.65,0);
            draw.Circle(target,lane,32,.65,.65,.7,1,false);draw.Circle(target,lane,24,.22,.20,.28,1,true);
            draw.Rect(target-1,82,2,100,1,1,1,.35,0);
            for(int i=session.VisibleEnd(time)-1;i>=session.FirstPending;i--){
                Circle obj=session.Map.Objects[i];if(obj.Judged)continue;
                double x=target+(session.Map.TaikoScrollPosition(obj.Time)-scrollNow)*speed;
                double end=target+(session.Map.TaikoScrollPosition(obj.EndTime)-scrollNow)*speed;if(x>width+40 || end<0)continue;
                if(session.Mods.Has("HD") && x<target+180)continue;bool strong=(obj.HitSound&4)!=0;double radius=strong?27:21;
                if(obj.Kind==ObjectKind.Slider){double left=Math.Max(target,x),right=Math.Min(width+30,end);if(right>=left){draw.Rect(left,lane-17,right-left,34,1,.68,.12,1,0);draw.Circle(right,lane,17,1,.68,.12,1,true);}x=Math.Max(target,x);}
                if(obj.Kind==ObjectKind.Spinner){
                    if(time>=obj.Time){x=target;double progress=Math.Min(1,(double)obj.PartsHit/session.SwellRequired(obj));draw.Arc(x,lane,38,progress,5,1,.75,.18,1);draw.Text(140,196,"Alternate centre / rim: "+obj.PartsHit+" / "+session.SwellRequired(obj),12,1,.85,.35);}
                    draw.Circle(x,lane,26,.75,.4,.95,1,true);draw.Text(x-7,lane-12,"!",23,1,1,1);continue;
                }
                bool rim=(obj.HitSound&10)!=0;double r=obj.Kind==ObjectKind.Slider?1:rim?.133:.733,g=obj.Kind==ObjectKind.Slider?.68:rim?.6:.067,b=obj.Kind==ObjectKind.Slider?.12:rim?.733:.467;
                draw.Circle(x,lane,radius+2,.06,.04,.09,1,true);draw.Circle(x,lane,radius,r,g,b,1,true);draw.Arc(x,lane,radius-3,1,2,1,1,1,.9);
                if(rim){draw.Circle(x-5,lane,2,1,1,1,1,true);draw.Circle(x+5,lane,2,1,1,1,1,true);}else draw.Rect(x-6,lane-1,12,2,1,1,1,1,0);
                if(strong)draw.Arc(x,lane,radius-7,1,1,1,1,1,.8);
            }
            DrawTaikoDrum(session,time);
            if(!ReducedEffects)DrawTaikoBursts(session,time);
            string mascot=TaikoMascotTexture(session,time,ReducedEffects);
            draw.Image(mascot,8,183,65,60,1);
            for(int i=0;i<4;i++){
                bool rim=i==0 || i==3;double age=time-session.TaikoPadTimes[i],pulse=age>=0?Math.Max(0,1-age/790):0,x=8+i*118;
                draw.Rect(x,246,110,60,rim?.08:.38,rim?.28:.06,rim?.37:.23,.85,5);
                draw.Rect(x,246,110,60,rim?.49:1,rim?.84:.58,rim?.99:.78,pulse*.5,5);
                draw.Text(x+30,257,rim?"RIM":"CENTRE",rim?16:12,1,1,1);draw.Text(x+34,281,i<2?"left":"right",10,.8,.85,.9);
            }
            for(int i=session.Judgements.Count-1;i>=0;i--){GameplayJudgement j=session.Judgements[i];double age=time-j.Time;if(age<0 || age>450)continue;draw.Text(130,64,j.Result=="300"?"GREAT":j.Result=="150"?"GOOD":"MISS",14,j.Result=="X"?1:.65,j.Result=="X"?.25:1,.4);break;}
            DrawObscurity(session,time,width,height);DrawHud(session,time,width,height);
        }
        private static readonly string[] MascotIdle={"taiko-pippidonidle0.png","taiko-pippidonidle1.png"};
        private static readonly string[] MascotFail={"taiko-pippidonfail0.png","taiko-pippidonfail1.png","taiko-pippidonfail2.png"};
        private static readonly string[] MascotKiai={"taiko-pippidonkiai0.png","taiko-pippidonkiai1.png"};
        private static readonly string[] MascotClear={"taiko-pippidonclear0.png","taiko-pippidonclear1.png","taiko-pippidonclear2.png","taiko-pippidonclear3.png","taiko-pippidonclear4.png","taiko-pippidonclear5.png","taiko-pippidonclear6.png"};
        private static readonly int[] MascotClearSequence={0,1,2,3,4,5,6,5,6,5,4,3,2,1,0};
        public static string TaikoMascotTexture(Session session,double time,bool reduced)
        {
            if(reduced)return session.TaikoMascotFailed?MascotFail[0]:MascotIdle[0];
            double sinceClear=time-session.TaikoClearAt;
            if(sinceClear>=0 && sinceClear<1500)return MascotClear[MascotClearSequence[(int)(sinceClear/100)]];
            bool kiai;double beat=session.Map.BeatPosition(time,out kiai);
            string[] frames=session.TaikoMascotFailed?MascotFail:kiai?MascotKiai:MascotIdle;
            int index=(int)(Math.Max(0,Math.Floor(beat))%frames.Length);return frames[index];
        }
        private static double[] DrumHalf(bool right)
        {
            var points=new double[34];for(int i=0;i<=16;i++){double a=-Math.PI/2+(right?1:-1)*Math.PI*i/16;points[i*2]=Math.Cos(a);points[i*2+1]=Math.Sin(a);}return points;
        }
        private static readonly double[] DrumLeft=DrumHalf(false),DrumRight=DrumHalf(true);
        private void DrawTaikoDrum(Session session,double time)
        {
            draw.Circle(31,132,30,.025,.02,.04,1,true);draw.Circle(31,132,27,.20,.20,.23,1,true);
            draw.Circle(31,132,19,.30,.30,.33,1,true);draw.Arc(31,132,28,1,1,.75,.75,.8,1);
            for(int pad=0;pad<4;pad++){
                double age=time-session.TaikoPadTimes[pad];if(age<0 || age>790)continue;
                bool rim=pad==0 || pad==3,right=pad>=2;double pulse=Math.Pow(1-age/790,3);
                draw.Polyline(right?DrumRight:DrumLeft,17,31,132,rim?23:9,rim?.35:1.8,rim?.49:1,rim?.84:.58,rim?.99:.78,pulse);
            }
            draw.Rect(29,104,4,56,.08,.08,.10,1,0);
        }
        private void DrawTaikoBursts(Session session,double time)
        {
            foreach(Session.TaikoEffect hit in session.TaikoEffects){
                double age=time-hit.Time,duration=hit.Great?480:230;if(age<0 || age>duration)continue;
                double t=age/duration,alpha=(1-t)*(1-t),r=hit.Rim?.49:1,g=hit.Rim?.84:.58,b=hit.Rim?.99:.78;
                double radius=(hit.Strong?29:22)+t*25;
                draw.Circle(88,132,radius,r,g,b,alpha*.18,true);draw.Arc(88,132,radius,1,hit.Strong?4:2,r,g,b,alpha);
                // One small flying note per accepted hit; bounded to eight effects.
                double x=88+t*300,y=132-150*t+90*t*t;
                draw.Circle(x,y,hit.Strong?8:5,r,g,b,alpha,true);
                draw.Arc(x,y,hit.Strong?8:5,1,1,1,1,1,alpha);
            }
        }
        private void DrawSpinner(Session session,Circle spinner,double time)
        {
            double cx=OriginX+256*Scale,cy=OriginY+192*Scale,radius=164*Scale,progress=Math.Min(1,session.SpinnerProgress(spinner));
            draw.Circle(cx,cy,radius,.12,.07,.13,.2,true);
            ArcSegment(cx,cy,radius,-Math.PI*.81,-Math.PI*.19,3,1,1,1);ArcSegment(cx,cy,radius,Math.PI*.19,Math.PI*.81,3,1,1,1);
            ArcSegment(cx,cy,radius+6,-Math.PI*.18,Math.PI*(-.18+.36*progress),5,.99,.38,.56);
            ArcSegment(cx,cy,radius+6,Math.PI*.82,Math.PI*(.82+.36*progress),5,.99,.38,.56);
            if(!ReducedEffects){double co=Math.Cos(spinner.VisualRotation),si=Math.Sin(spinner.VisualRotation);for(int i=0;i<32;i++)draw.Circle(cx+(spinnerX[i]*co-spinnerY[i]*si)*(radius-9),cy+(spinnerY[i]*co+spinnerX[i]*si)*(radius-9),1.3,1,.65,.8,.5,true);}
            draw.Arc(cx,cy,18*Scale,1,4,1,1,1,1);draw.Arc(cx,cy,24*Scale,1,1,1,1,1,.7);
            if(time>spinner.Time){double rpm=spinner.Rotation/(Math.PI*2)/Math.Max(.1,(time-spinner.Time)/60000);Text(cx-17,cy+39,rpm.ToString("F0"),17);Text(cx-43,cy+60,"SPINS PER MINUTE",8);}
            if(session.BonusSpins(spinner)>0)Text(cx-23,cy-60,(session.BonusSpins(spinner)*1000).ToString(),20);
        }
        private void ArcSegment(double x,double y,double radius,double start,double end,double width,double r,double g,double b)
        {if(end<=start)return;int segments=ReducedEffects?8:24;double delta=(end-start)/segments,co=Math.Cos(delta),si=Math.Sin(delta),px=Math.Cos(start),py=Math.Sin(start);for(int i=0;i<=segments;i++){arcScratch[i*2]=px*radius;arcScratch[i*2+1]=py*radius;double nx=px*co-py*si;py=px*si+py*co;px=nx;}draw.Polyline(arcScratch,segments+1,x,y,1,width,r,g,b,1);}
        public void DrawPauseMenu(IUiSurface surface,int width,int height)
        {
            surface.Rect(0,0,width,height,.025,.018,.045,.72,0);surface.Rect(118,54,244,218,.09,.065,.14,.98,10);
            surface.Text(166,70,"GAME PAUSED",22,1,.72,.88);
            surface.Rect(145,111,190,39,.25,.65,.25,1,8);surface.Text(199,120,"CONTINUE",15,1,1,1);
            surface.Rect(145,160,190,39,.85,.65,.12,1,8);surface.Text(213,169,"RETRY",15,1,1,1);
            surface.Rect(145,209,190,39,.75,.18,.23,1,8);surface.Text(220,218,"QUIT",15,1,1,1);
            surface.Rect(350,278,116,28,.3,.18,.4,1,3);surface.Text(362,285,"edit HUD",12,1,1,1);
        }
        public void DrawFailedMenu(IUiSurface surface,int width,int height)
        {
            surface.Rect(0,0,width,height,.025,.012,.030,.78,0);surface.Rect(118,62,244,194,.12,.045,.09,.98,10);
            surface.Text(167,78,"LEVEL FAILED",23,1,.25,.42);
            surface.Text(172,108,"Your health reached zero",11,.88,.78,.84);
            surface.Rect(145,145,190,39,.85,.65,.12,1,8);surface.Text(213,154,"RETRY",15,1,1,1);
            surface.Rect(145,198,190,39,.75,.18,.23,1,8);surface.Text(220,207,"QUIT",15,1,1,1);
        }
    }
}
